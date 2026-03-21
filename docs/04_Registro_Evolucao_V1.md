# Implementação Concluída: Resiliência e DLQ

O plano de implementação para os itens de **Robustez e Resiliência** foi aplicado com sucesso e o projeto está compilando normalmente. A seguir, o resumo das alterações entregues:

### 1. Injeção de Dependências e Polly (Retry + Circuit Breaker)
O pacote `Microsoft.Extensions.Resilience` foi adicionado à API e as diretrizes do Polly (v8+) configuradas através da interface `ResiliencePipelineBuilder`:
- **Retry**: Configurado para capturar qualquer exceção, com *Exponential Backoff* de até 3 tentativas, aguardando 2 segundos iniciais.
- **Circuit Breaker**: Tolera uma taxa de falha de 50% em janelas de amostragem de 10 segundos antes de abrir o circuito por 30 segundos (evitando requisições contínuas a serviços fora do ar).

[Ver alterações no Program.cs](file:///c:/Users/eduardo.porto/Downloads/E/Eduardo.porto/AgendaMil/NotificationService/NotificationService/src/NotificationService.Api/Program.cs)

### 2. Integração no `MessageProcessor`
A chamada principal de envio de e-mails (`SendEmailAsync`) agora é executada inteiramente dentro da pipeline abstrata do Polly, permitindo que falhas transitórias de e-mail (como lentidão no provedor) sejam suprimidas pelas tentativas, mas reportando e interrompendo falhas graves e crônicas com um alerta no log via `BrokenCircuitException`.

[Ver uso da ResiliencePipeline](file:///c:/Users/eduardo.porto/Downloads/E/Eduardo.porto/AgendaMil/NotificationService/NotificationService/src/NotificationService.Application/Services/MessageProcessor.cs)

### 3. Setup de Dead Letter Queue (DLQ)
A infraestrutura nativa do consumo de RabbitMQ foi melhorada com o padrão DLQ. Em vez de descartar mensagens com falhas sistêmicas irrecuperáveis, o sistema agora as redireciona para uma fila especial (`email_notifications_dlq`), permitindo investigação futura e reprocessamento sob demanda. O downgrade do `RabbitMQ.Client` para `6.8.1` foi feito para estabilizar a compilação com a sintaxe atual da aplicação.

[Ver configuração do Exchange e Queue](file:///c:/Users/eduardo.porto/Downloads/E/Eduardo.porto/AgendaMil/NotificationService/NotificationService/src/NotificationService.Infrastructure/Messaging/RabbitMq/NotificationMessageConsumer.cs)

- Polly Configuration: `NotificationService.Api/Program.cs`
- Dead Letter Queue: `NotificationService.Infrastructure/Messaging/RabbitMq/NotificationMessageConsumer.cs`

---

## Refatoração de Canais, Templates e Anexos

**Objetivo Executado:** Mudar o sistema engessado de envios apenas de E-mails para uma plataforma flexível através do padrão Strategy, capaz de rotear mensagens HTML ou Textos via SendGrid e Twilio, contemplando também arquivos anexados.

### 1. Refatoração do Domínio
Renomeou-se o antigo `EmailLog` para a entidade genérica `NotificationLog`, adicionando as propriedades `Channel` (Enum) e `AttachmentsJson`. 
- **Migration `RenameEmailLogToNotificationLog`:** Realizou a atualização das tabelas de `EmailLogs` para `NotificationLogs`.
- **Migration `AddAttachmentsColumn`:** Inseriu a string JSON para evitar que arquivos anexados sejam perdidos em caso de paradas em Circuit Breaker.

### 2. Padrão Strategy - Roteamento de Comunicação
A interface antiga `IEmailProvider` foi renomeada para `INotificationProvider`.
Criou-se três provedores diferentes que implementam essa interface e se autenticam explicitamente por Canal (`SupportedChannel`):
 - `SendGridEmailProvider`: Processa payload HTML e insere itens deserializados enviados através da propriedade `AttachmentsJson` usando `msg.AddAttachment`.
 - `TwilioSmsProvider`: Dispara plain texts contínuos à interface do Twilio via SMS.
 - `TwilioWhatsAppProvider`: Adequa metadados acrescentando `whatsapp:` na frente dos remetentes usando API REST Twilio.
     
Para orquestrar, `MessageProcessor.cs` passou a receber em seu construtor todos os providers registrados:
```csharp
var provider = _providers.FirstOrDefault(p => p.SupportedChannel == message.Channel);
```

### 3. Motor de Inteligência de Templates (Fluid.Core)
Adicionado pacote `Fluid.Core` e implementada e injeção do `FluidTemplateRenderer`. Se um arquivo ditar seu formato, a biblioteca interpola adequadamente as chaves que foram recebidas em `message.Data`. Ele detecta automativamente baseando-se no `TemplateName` preenchido.

### Passos Manuais de Verificação Sugeridos:
- **Testar E-mail Padrão:** Enviar payload RabbitMQ com `{ channel: 0 }` (E-mail) para verificar se será disparado o `SendGridProvider`.
- **Testar SMS/WhatsApp:** Enviar payload via Management UI `{ channel: 1 }` ou `{ channel: 2 }`.
- **Anexos:** Enviar um item para o Rabbit contendo a sub-lista `"Attachments": [ { "FileName": "teste.txt", "ContentType": "plain/text", "Base64Content": "xxxx" } ]`. Validar se o E-mail chega na caixa com o respectivo dado anexado via SendGrid e as Strings preenchidas em `NotificationLogs.AttachmentsJson`.

---

### Avaliação e Próximos Passos
O fluxo todo compila sem erros (Warnings suprimidas / tratadas). Agora o `NotificationService` possui a fundação para operar de maneira mais confiável em produção frente a instabilidades de rede ou do provedor de mensageria. 

**Validação Manual Sugerida**:
Rodar a aplicação com `dotnet run` e injetar configurações falsas para o _SendGrid_. Observar as retentativas silenciosas e em seguida acessar a porta `15672` (Painel do RabbitMQ Local) para constatar a captura da mensagem originária na fila morta.

---

## Engenharia Avançada (Performance, Rastreio e Automação de Testes)

**Objetivo Executado:** Tornar a aplicação observável de ponta a ponta e segura contra exaustões de recursos do banco de dados, além de garantir que a esteira de CI/CD (Github Actions / Azure DevOps) possa executar testes E2E sem necessitar de infraestrutura persistente.

### 1. Rastreabilidade (Correlation ID)
Foi implementada a transição da chave `CorrelationId`. Quando os publicadores geram uma mensagem JSON englobando a sub-chave `Metadata { CorrelationId: "" }`, o NotificationService automaticamente captura esse ID, salva na nova coluna `CorrelationId` da tabela `NotificationLogs` e espelha em todos os arquivos de Lock de texto usando o padrão `[CorrelationId: XXX]`. 
Isso permite encontrar no Elasticsearch ou no banco exatamente qual pacote deu falha.

### 2. Consumo Agressivo e Pooling (Prefetch / Semaphore)
O Consumer assíncrono agora busca blocos de 50 mensagens do RabbitMQ simultaneamente (`BasicQos`). 
Para evitar que essas 50 mensagens em formato de *fire-and-forget* esgotem o `Connection Pool` do Entity Framework Core ou causem *Deadlocks* transacionais ao atualizar o Status para sucesso/falha ao mesmo tempo, foi estipulado um colete salva-vidas de Multiplexação via **`SemaphoreSlim`** no C# que estreita a esteira e despacha grupos seguros pro EF, acelerando drasticamente o consumo do RabbitMQ sem derrubar o Banco SQL.

### 3. Testcontainers (Integração Híbrida)
Os testes de Integração baseados no Runner `xUnit` foram modernizados. A classe autônoma `TestcontainersRabbitMqTests.cs` agora injeta dinamicamente o RabbitMQ consumindo as APIS do Docker Hub:
1. Ao rodar o pacote `dotnet test`, o C# atrai a imagem oficial do Rabbit;
2. Instancia a imagem na memória em fração de segundos.
3. Exclui e formata `Exchange` e `Queues`.
4. Injeta DTOs validando rastreabilidade.
5. Consome, finaliza com Assert de Sucesso e Autodestrói a rede Docker da Máquina.
