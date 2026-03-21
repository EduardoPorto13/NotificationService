using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Interfaces;
using Polly;
using Polly.Registry;
using Polly.CircuitBreaker;
using System.Text.Json;

namespace NotificationService.Application.Services;

public class MessageProcessor : IMessageProcessor
{
    private readonly IEnumerable<INotificationProvider> _providers;
    private readonly INotificationLogRepository _emailLogRepository;
    private readonly IAppLogger _logger;
    private readonly ResiliencePipelineProvider<string> _pipelineProvider;
    private readonly ITemplateRenderer _templateRenderer;

    public MessageProcessor(
        IEnumerable<INotificationProvider> providers, 
        INotificationLogRepository emailLogRepository, 
        IAppLogger logger,
        ResiliencePipelineProvider<string> pipelineProvider,
        ITemplateRenderer templateRenderer)
    {
        _providers = providers;
        _emailLogRepository = emailLogRepository;
        _logger = logger;
        _pipelineProvider = pipelineProvider;
        _templateRenderer = templateRenderer;
    }

    public async Task ProcessAsync(NotificationMessageDto message, CancellationToken cancellationToken = default)
    {
        var correlationId = !string.IsNullOrWhiteSpace(message.Metadata?.CorrelationId) 
            ? message.Metadata.CorrelationId 
            : Guid.NewGuid().ToString();

        _logger.Info($"[MessageProcessor] Recebida requisição de notificação para: {message.To} [CorrelationId: {correlationId}]");

        if (string.IsNullOrWhiteSpace(message.To))
        {
            _logger.ErroArquivo(
                "ErroNotificacao_SemDestinatario",
                $"Falha ao processar mensagem. Destinatário (To) está em nulo ou branco. [CorrelationId: {correlationId}]",
                "Application",
                null);
            return;
        }

        string corpoFinal = message.Data.ContainsKey("Body") ? message.Data["Body"] : "Olá, você tem uma nova notificação.";
        if (!string.IsNullOrEmpty(message.Type))
        {
            try
            {
                corpoFinal = await _templateRenderer.RenderAsync(message.Type, message.Channel, message.Data, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.ErroArquivo(
                    "ErroTemplate_Render",
                    $"Falha ao renderizar template {message.Type} para {message.Channel}: {ex.Message} [CorrelationId: {correlationId}]",
                    "Application",
                    null);
            }
        }

        // 1. Cria Entidade inicial com Status "Pendente"
        var emailModel = new NotificationLog
        {
            Id = Guid.NewGuid(),
            CorrelationId = correlationId,
            Channel = message.Channel,
            To = message.To,
            Subject = message.Data.ContainsKey("Subject") ? message.Data["Subject"] : "Notificação Automática",
            Payload = corpoFinal,
            AttachmentsJson = message.Attachments?.Any() == true ? JsonSerializer.Serialize(message.Attachments) : null,
            CreatedAt = DateTime.UtcNow,
            Attempts = 1,
            Status = 0 // 0 = Pending/Processando
        };

        // Salva na Base pra caso caia a energia ter resquício
        await _emailLogRepository.AddAsync(emailModel, cancellationToken);
        _logger.Info($"[MessageProcessor] EmailLog[{emailModel.Id}] registrado no Banco como Pendente. [CorrelationId: {correlationId}]");

        // 2. Dispara o Provider correspondente ao Canal com Polly (Retry e Circuit Breaker)
        var provider = _providers.FirstOrDefault(p => p.SupportedChannel == message.Channel);
        if (provider == null)
        {
            _logger.ErroArquivo(
                "ErroNotificacao_CanalNaoSuportado",
                $"Nenhum provedor implementado para o canal: {message.Channel} [CorrelationId: {correlationId}]",
                "Application",
                null);
                
            emailModel.Status = 2; // Failed
            emailModel.ErrorMessage = "Provedor não integrado/registrado.";
            await _emailLogRepository.UpdateAsync(emailModel, cancellationToken);
            return;
        }

        var pipeline = _pipelineProvider.GetPipeline("email-provider"); // manteremos email-provider como pipeline genérica por ora
        bool sucesso = false;

        try
        {
            sucesso = await pipeline.ExecuteAsync(async ct => 
                await provider.SendAsync(emailModel, ct), 
                cancellationToken);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.ErroArquivo(
                "ErroProvider_CircuitBreaker",
                $"Circuito aberto ao tentar enviar e-mail {emailModel.Id}. Exceção: {ex.Message} [CorrelationId: {correlationId}]",
                "Application",
                null);
            
            // Lançar exceção para que o RabbitMQ consumer falhe (Nack) e envie para DLQ
            throw; 
        }
        // 3. Atualiza estado Pós-Envio
        if (sucesso)
        {
            emailModel.Status = 1; // 1 = Success
            emailModel.SentAt = DateTime.UtcNow;
            _logger.Info($"[MessageProcessor] E-mail{emailModel.Id} enviado para {emailModel.To} com sucesso. [CorrelationId: {correlationId}]");
        }
        else
        {
            emailModel.Status = 2; // 2 = Failed
             _logger.Info($"[MessageProcessor] Falha no disparo do EmailLog {emailModel.Id}. [CorrelationId: {correlationId}]");
            // O próprio SendGridEmailProvider já injetou a exception logada pelo AppLogger.
        }

        // Grava fechamento do caso na base de dados
        await _emailLogRepository.UpdateAsync(emailModel, cancellationToken);
    }
}
