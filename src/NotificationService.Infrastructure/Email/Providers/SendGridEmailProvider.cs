using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;
using NotificationService.Domain.Settings;
using NotificationService.Application.DTOs;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace NotificationService.Infrastructure.Email.Providers;

public class SendGridEmailProvider : INotificationProvider
{
    private readonly IAppLogger _logger;
    private readonly EmailProviderSettings _settings;
    private readonly SendGridClient _client;
    
    public NotificationChannel SupportedChannel => NotificationChannel.Email;

    public SendGridEmailProvider(IOptions<EmailProviderSettings> settings, IAppLogger logger)
    {
        _logger = logger;
        _settings = settings.Value;

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            _logger.Info("[SendGrid] AVISO: A ApiKey do SendGrid não foi configurada nas variaveis de ambiente ou appsettings.");
            
        _client = new SendGridClient(_settings.ApiKey);
    }

    public async Task<bool> SendAsync(NotificationLog emailModel, CancellationToken cancellationToken = default)
    {
        _logger.Info($"[SendGrid] Iniciando envio para o e-mail: {emailModel.To} | Assunto: {emailModel.Subject}");
        
        try
        {
            var from = new EmailAddress(_settings.FromEmail, _settings.FromName);
            var to = new EmailAddress(emailModel.To);
            
            // Aqui assumiremos Payload/corpo HTML e formatação limpa baseada no template passado.
            var htmlContent = string.IsNullOrWhiteSpace(emailModel.Payload) ? "Sem Conteúdo" : emailModel.Payload;
            var plainTextContent = string.Empty;

            var msg = MailHelper.CreateSingleEmail(from, to, emailModel.Subject, plainTextContent, htmlContent);

            if (!string.IsNullOrEmpty(emailModel.AttachmentsJson))
            {
                try
                {
                    var attachments = JsonSerializer.Deserialize<List<AttachmentDto>>(emailModel.AttachmentsJson);
                    if (attachments != null)
                    {
                        foreach (var att in attachments)
                        {
                            msg.AddAttachment(att.FileName, att.Base64Content, att.ContentType);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.ErroArquivo(
                        "ErroSendGrid_Anexo",
                        $"Falha ao desserializar/anexar arquivos no e-mail {emailModel.Id}: {ex.Message}",
                        "SendGridEmailProvider",
                        null);
                }
            }

            var response = await _client.SendEmailAsync(msg, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.Info($"[SendGrid] E-mail enviado com sucesso. StatusCode: {response.StatusCode}");
                return true;
            }
            else
            {
                // Converte payload da falha via Json pra log de erro:
                var responseBody = await response.Body.ReadAsStringAsync(cancellationToken);
                _logger.ErroArquivo(
                    $"ErroSendGrid_{emailModel.Id}", 
                    $"Falha no SendGrid. Status: {response.StatusCode} | Resposta: {responseBody}", 
                    "SendGridProvider", 
                    null);

                return false;
            }
        }
        catch(Exception ex)
        {
             _logger.ErroArquivo(
                    $"ExceptionSendGrid_{emailModel.Id}", 
                    $"Erro interno ao contatar provedor: {ex.Message}", 
                    "SendGridProvider", 
                    null);
                    
             return false;
        }
    }
}
