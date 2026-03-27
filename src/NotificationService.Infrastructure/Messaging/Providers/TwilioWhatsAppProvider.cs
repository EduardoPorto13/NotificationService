using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace NotificationService.Infrastructure.Messaging.Providers;

public class TwilioWhatsAppProvider : INotificationProvider
{
    private readonly IAppLogger _logger;
    // TODO: Idealmente essas chaves devem vir do IOptions<TwilioSettings> (appsettings.json) ou Environment Variable
    private readonly string _accountSid = Environment.GetEnvironmentVariable("TWILIO_ACCOUNT_SID") ?? "CHAVE_REMOVIDA"; 
    private readonly string _authToken = Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN") ?? "CHAVE_REMOVIDA";
    // O sandbox do WhatsApp do Twilio manda com prefixo whatsapp:
    private readonly string _fromNumber = "whatsapp:+14155238886";

    public NotificationChannel SupportedChannel => NotificationChannel.WhatsApp;

    public TwilioWhatsAppProvider(IAppLogger logger)
    {
        _logger = logger;
        TwilioClient.Init(_accountSid, _authToken);
    }

    public async Task<bool> SendAsync(NotificationLog message, CancellationToken cancellationToken = default)
    {
        _logger.Info($"[Twilio] Iniciando envio de WhatsApp para {message.To}");
        try
        {
            // O Twilio exige que números de whats tenham o prefixo 'whatsapp:'
            var toFormatted = message.To.StartsWith("whatsapp:") ? message.To : $"whatsapp:{message.To}";

            var msg = await MessageResource.CreateAsync(
                body: message.Payload,
                from: new PhoneNumber(_fromNumber),
                to: new PhoneNumber(toFormatted)
            );
            
            _logger.Info($"[Twilio] WhatsApp enviado com sucesso. SID: {msg.Sid}");
            return msg.ErrorCode == null;
        }
        catch (Exception ex)
        {
            _logger.ErroArquivo(
                "ErroTwilio_WhatsApp",
                $"Falha ao enviar WhatsApp para {message.To}: {ex.Message}",
                "TwilioWhatsAppProvider",
                null);
            return false;
        }
    }
}
