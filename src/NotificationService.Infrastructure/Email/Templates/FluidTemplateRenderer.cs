using Fluid;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;
using System.IO;

namespace NotificationService.Infrastructure.Email.Templates;

public class FluidTemplateRenderer : ITemplateRenderer
{
    private readonly FluidParser _parser;
    private readonly TemplateOptions _options;

    public FluidTemplateRenderer()
    {
        _parser = new FluidParser();
        _options = new TemplateOptions();
    }

    public async Task<string> RenderAsync(string templateName, NotificationChannel channel, IDictionary<string, string> data, CancellationToken cancellationToken = default)
    {
        // Aqui simularíamos a busca do template em um banco de dados ou FileSystem.
        // O sufixo do canal determina a sintaxe (ex: _Email = HTML, _Sms = PlainText).
        // Ex: var templateFísico = File.ReadAllText($"Templates/{templateName}_{channel}.liquid");
        
        string rawTemplate = GetFallbackTemplate(templateName, channel);

        if (_parser.TryParse(rawTemplate, out var template, out var error))
        {
            var context = new TemplateContext(data, _options);
            return await template.RenderAsync(context);
        }

        throw new InvalidOperationException($"Erro ao fazer parse do template Liquid: {error}");
    }

    private string GetFallbackTemplate(string templateName, NotificationChannel channel)
    {
        // Stub para prova de conceito. Na vida real, estaria no banco.
        if (channel == NotificationChannel.Email)
            return "<html><body><h1>Sua Mensagem:</h1><p>{{ Body }}</p></body></html>";
            
        return "Notificação AgendaMil: {{ Body }}";
    }
}
