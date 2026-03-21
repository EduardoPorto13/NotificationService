using NotificationService.Domain.Settings;
using NotificationService.Domain.Interfaces;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Interfaces;
using NotificationService.Domain.Settings;

namespace NotificationService.Infrastructure.Messaging.RabbitMq;

public class NotificationMessageConsumer : BackgroundService
{
    private readonly IAppLogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqSettings _rabbitSettings;
    
    private IConnection _connection;
    private IModel _channel;
    private string _queueName = "email_notifications_queue";
    private readonly SemaphoreSlim _processingSemaphore = new SemaphoreSlim(20);

    public NotificationMessageConsumer(
        IAppLogger logger, 
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqSettings> rabbitSettings)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _rabbitSettings = rabbitSettings.Value;

        InitializeRabbitMqListener();
    }

    private void InitializeRabbitMqListener()
    {
        var factory = new ConnectionFactory
        {
            HostName = _rabbitSettings.HostName,
            Port = _rabbitSettings.Port,
            UserName = _rabbitSettings.UserName,
            Password = _rabbitSettings.Password,
            DispatchConsumersAsync = true // Permite o uso do AsyncEventingBasicConsumer no .NET
        };

        try
        {
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // 1. Configurar Dead Letter Exchange (DLX) e Queue (DLQ)
            string dlxName = "email_notifications_dlx";
            string dlqName = "email_notifications_dlq";
            
            _channel.ExchangeDeclare(dlxName, ExchangeType.Direct);
            _channel.QueueDeclare(queue: dlqName, durable: true, exclusive: false, autoDelete: false, arguments: null);
            _channel.QueueBind(queue: dlqName, exchange: dlxName, routingKey: _queueName);
            _channel.QueueBind(queue: dlqName, exchange: dlxName, routingKey: ""); // Bind adicional por segurança

            // 2. Garantir que a fila principal existe com o argumento x-dead-letter-exchange
            var queueArgs = new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", dlxName }
            };

            _channel.QueueDeclare(queue: _queueName,
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: queueArgs);

            // 3. Configurar Bulk/Batch de Ingestão (Busca lotes de 50 mensagens em vez de 1 por 1)
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 50, global: false);

            _logger.Info($"[RabbitMQ] Conectado e escutando a fila: {_queueName} (com DLQ em {dlqName} e Prefetch de 50)");
        }
        catch (Exception ex)
        {
            _logger.ErroArquivo(
                "ErroRabbitMQ_Startup", 
                $"Falha ao conectar no provedor MQTT na incializacao. Detalhes: {ex.Message}", 
                "MessageConsumer", 
                null);
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel == null || _channel.IsClosed)
        {
            _logger.Info("[RabbitMQ] Canal indisponível. Interrompendo BackgroundService.");
            return Task.CompletedTask;
        }

        stoppingToken.ThrowIfCancellationRequested();

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (ch, ea) =>
        {
            await _processingSemaphore.WaitAsync(stoppingToken);
            try
            {
                var body = ea.Body.ToArray();
                var messageString = Encoding.UTF8.GetString(body);

                try
                {
                     // Conversão Payload fila para DTO
                     var notificationDto = JsonSerializer.Deserialize<NotificationMessageDto>(
                         messageString, 
                         new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                     );

                     // Resolver dependências da pilha Scope (Application / EF / Providers)
                     using (var scope = _scopeFactory.CreateScope())
                     {
                         var processor = scope.ServiceProvider.GetRequiredService<IMessageProcessor>();
                         await processor.ProcessAsync(notificationDto, stoppingToken);
                     }

                     // Marca mensagem como processada (Sucesso db e email)
                     _channel.BasicAck(ea.DeliveryTag, false);
                     
                     _logger.Info($"[RabbitMQ] Mensagem na Tag {ea.DeliveryTag} concluída em Batch.");
                }
                catch (Exception ex)
                {
                     _logger.ErroArquivo(
                         $"ErroConsumer_{ea.DeliveryTag}", 
                         $"Falha sistêmica no processamento do evento RabbitMq. Payload cru: {messageString}  | Exceção: {ex.Message}", 
                         "MessageConsumer", 
                         null);
                     
                     // Não tira da fila (Nack e requeue = false, envia pra DLQ se houver ou ignora).
                     _channel.BasicNack(ea.DeliveryTag, false, requeue: false); 
                }
            }
            finally
            {
                _processingSemaphore.Release();
            }
        };

        _channel.BasicConsume(queue: _queueName, autoAck: false, consumer: consumer);

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}
