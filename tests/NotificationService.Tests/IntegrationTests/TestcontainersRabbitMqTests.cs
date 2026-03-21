using NotificationService.Application.DTOs;
using NotificationService.Domain.Enums;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using Testcontainers.RabbitMq;
using Xunit;

namespace NotificationService.Tests.IntegrationTests;

public class TestcontainersRabbitMqTests : IAsyncLifetime
{
    private RabbitMqContainer _rabbitMqContainer;
    private IConnection _connection;
    private IModel _channel;

    private const string QueueName = "email_notifications_queue";
    private const string ExchangeName = "";
    private const string RoutingKey = "email_notifications_queue";

    public async Task InitializeAsync()
    {
        // Levanta um container do RabbitMQ diretamente do DockerHub na memória!
        _rabbitMqContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management")
            .Build();

        await _rabbitMqContainer.StartAsync();

        // Conecta usando a porta dinâmica e string de conexão do Container gerado
        var factory = new ConnectionFactory 
        { 
            Uri = new Uri(_rabbitMqContainer.GetConnectionString()) 
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Configuração espelhada da Fila Real
        var queueArgs = new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", "email_notifications_dlx" }
        };

        _channel.ExchangeDeclare("email_notifications_dlx", ExchangeType.Direct);
        _channel.QueueDeclare(queue: "email_notifications_dlq", durable: true, exclusive: false, autoDelete: false, arguments: null);
        _channel.QueueBind("email_notifications_dlq", "email_notifications_dlx", QueueName);

        _channel.QueueDeclare(queue: QueueName, durable: true, exclusive: false, autoDelete: false, arguments: queueArgs);
    }

    [Fact]
    public async Task Deve_Publicar_E_Consumir_Mensagem_Do_Testcontainer_Autonomo()
    {
        // Arrange - Monta o DTO com Metadata de Rastreabilidade (CorrelationId)
        var messageDto = new NotificationMessageDto
        {
            Channel = NotificationChannel.Email,
            To = "testcontainers@docker.local",
            Type = "TesteGeralTemplate",
            Data = new Dictionary<string, string>
            {
                { "Subject", "RabbitMq Autônomo" },
                { "Body", "Conteúdo validado com sucesso via Docker efêmero." }
            },
            Metadata = new NotificationMetadataDto
            {
                CorrelationId = Guid.NewGuid().ToString(),
                Source = "XUnit_Testcontainers"
            }
        };

        var json = JsonSerializer.Serialize(messageDto);
        var body = Encoding.UTF8.GetBytes(json);
        
        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;

        var messageReceived = new TaskCompletionSource<string>();

        // Consumidor efêmero só para escutar se o que postamos entra rápido na fila
        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += (model, ea) =>
        {
            var receivedBody = ea.Body.ToArray();
            var receivedString = Encoding.UTF8.GetString(receivedBody);
            messageReceived.TrySetResult(receivedString);
            _channel.BasicAck(ea.DeliveryTag, false);
        };

        _channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);

        // Act - Publicamos na fila containerizada
        _channel.BasicPublish(exchange: ExchangeName,
                             routingKey: RoutingKey,
                             basicProperties: properties,
                             body: body);

        // Assert - Esperamos o consumidor interno pegar
        var delayTask = Task.Delay(TimeSpan.FromSeconds(5));
        var completedTask = await Task.WhenAny(messageReceived.Task, delayTask);

        Assert.True(completedTask == messageReceived.Task, "O consumidor demorou mais que 5 segundos para ler ou a mensagem se perdeu!");

        var consumedJson = messageReceived.Task.Result;
        var consumedDto = JsonSerializer.Deserialize<NotificationMessageDto>(consumedJson);

        Assert.NotNull(consumedDto);
        Assert.Equal("testcontainers@docker.local", consumedDto.To);
        Assert.Equal("XUnit_Testcontainers", consumedDto.Metadata.Source);
    }

    public async Task DisposeAsync()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        
        // Finaliza o container e exclui a imagem isolada finalizando o teste sem sujeira
        if (_rabbitMqContainer != null)
        {
            await _rabbitMqContainer.DisposeAsync();
        }
    }
}
