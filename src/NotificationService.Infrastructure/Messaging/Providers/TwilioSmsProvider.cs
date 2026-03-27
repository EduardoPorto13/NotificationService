using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace NotificationService.Infrastructure.Messaging.Providers;

public class TwilioSmsProvider : INotificationProvider
{
    private readonly IAppLogger _logger;
    // TODO: Idealmente essas chaves devem vir do IOptions<TwilioSettings> (appsettings.json) ou Environment Variable
    private readonly string _accountSid = Environment.GetEnvironmentVariable("TWILIO_ACCOUNT_SID") ?? "CHAVE_REMOVIDA"; 
    private readonly string _authToken = Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN") ?? "CHAVE_REMOVIDA"; 
    private readonly string _fromNumber = "+12543828803";

    public NotificationChannel SupportedChannel => NotificationChannel.Sms;

    public TwilioSmsProvider(IAppLogger logger)
    {
        _logger = logger;
        TwilioClient.Init(_accountSid, _authToken);
    }

    public async Task<bool> SendAsync(NotificationLog message, CancellationToken cancellationToken = default)
    {
        _logger.Info($"[Twilio] Iniciando envio de SMS para {message.To}");
        try
        {
            var msg = await MessageResource.CreateAsync(
                body: message.Payload,
                from: new PhoneNumber(_fromNumber),
                to: new PhoneNumber(message.To)
            );
            
            _logger.Info($"[Twilio] SMS enviado com sucesso. SID: {msg.Sid}");
            return msg.ErrorCode == null;
        }
        catch (Exception ex)
        {
            _logger.ErroArquivo(
                "ErroTwilio_Sms",
                $"Falha ao enviar SMS para {message.To}: {ex.Message}",
                "TwilioSmsProvider",
                null);
            return false;
        }
    }
}
