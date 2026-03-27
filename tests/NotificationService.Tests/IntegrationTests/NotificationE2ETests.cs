using NotificationService.Application.DTOs;
using NotificationService.Domain.Enums;
using RabbitMQ.Client;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Xunit;

namespace NotificationService.Tests.IntegrationTests;

public class NotificationE2ETests
{
    private const string QueueName = "email_notifications_queue";
    private const string ExchangeName = ""; 
    private const string RoutingKey = "email_notifications_queue";

    // Mantenho com "Skip" apenas para evitar que rodadas de CL/CI falhem se não houver um RabbitMQ rodando. 
    // Para rodar localmente no seu Visual Studio e disparar a mensagem, apenas comente ou remova o (Skip = "...").

    [Fact(Skip = "Teste manual de SMS. Remova o parâmetro 'Skip' para executar.")]
    //[Fact]
    public void Deve_Publicar_NotificacaoSms_Na_Fila_RabbitMQ()
    {
        PublicarMensagem(NotificationChannel.Sms, "+5511970823248", "Teste SMS RabbitMQ", "Olá! Esta é uma notificação teste via SMS injetada do xUnit.");
        Assert.True(true);
    }

    [Fact(Skip = "Teste manual de WhatsApp. Remova o parâmetro 'Skip' para executar.")]
    //[Fact]
    public void Deve_Publicar_NotificacaoWhatsApp_Na_Fila_RabbitMQ()
    {
        PublicarMensagem(NotificationChannel.WhatsApp, "+5511970823248", "Teste WhatsApp RabbitMQ", "Olá! Esta é uma notificação teste via WhatsApp injetada do xUnit.");
        Assert.True(true);
    }

    //[Fact(Skip = "Teste manual de E-mail. Remova o parâmetro 'Skip' para executar.")]
    [Fact]
    public void Deve_Publicar_NotificacaoEmail_Na_Fila_RabbitMQ()
    {
        PublicarMensagem(NotificationChannel.Email, "eduporto13@gmail.com", "Teste Email RabbitMQ", "Olá! Esta é uma notificação HTML de e-mail enviada via RabbitMQ e testada no xUnit.");
        Assert.True(true);
    }

    private void PublicarMensagem(NotificationChannel channelType, string destinationTo, string subject, string bodyText)
    {
        // Conexão com o RabbitMQ local default
        var factory = new ConnectionFactory() { HostName = "localhost" };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        // 1. Garante configuração estrita da Fila + DLQ
        var queueArgs = new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", "email_notifications_dlx" }
        };

        // Usa default exchange (string vazia) se ExchangeName for vazio
        if (!string.IsNullOrEmpty(ExchangeName))
            channel.ExchangeDeclare(exchange: ExchangeName, type: ExchangeType.Direct, durable: true);
            
        channel.QueueDeclare(queue: QueueName, durable: true, exclusive: false, autoDelete: false, arguments: queueArgs);
        
        if (!string.IsNullOrEmpty(ExchangeName))
            channel.QueueBind(queue: QueueName, exchange: ExchangeName, routingKey: RoutingKey);

        // 2. Monta o DTO estático com o canal dinâmico e contatos fornecidos
        var messageDto = new NotificationMessageDto
        {
            Channel = channelType,
            To = destinationTo,
            Type = "TesteGeralTemplate",
            Data = new Dictionary<string, string>
            {
                { "Subject", subject },
                { "Body", bodyText }
            }
        };

        var json = JsonSerializer.Serialize(messageDto);
        var body = Encoding.UTF8.GetBytes(json);

        // Propriedades da mensagem (DeliveryMode = 2 faz com ela ser persistente no disco)
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;

        // 3. Publica a mensagem na exchange
        channel.BasicPublish(exchange: ExchangeName,
                             routingKey: RoutingKey,
                             basicProperties: properties,
                             body: body);
    }
}
