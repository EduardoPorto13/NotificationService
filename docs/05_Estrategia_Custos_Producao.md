# Estratégia de Custos e Provedores para Produção

Quando o `NotificationService` for publicado em ambiente produtivo, especialmente para projetos com baixo orçamento inicial no Brasil, a escolha dos provedores (Brokers) atrelados à interface `INotificationProvider` ditará os custos operacionais.

Abaixo, o plano de ação recomendado para escalar o sistema gastando o mínimo possível, aproveitando o padrão **Strategy** já implementado em nossa Arquitetura.

---

## 1. E-mail: SendGrid (Grátis)
O envio de E-mails transacionais não exige altos investimentos iniciais.
- **Plano Recomendado:** A **SendGrid** oferece um plano "Free Forever" (Gratuito para sempre) agressivo, permitindo enviar até **100 e-mails/dia** a custo zero.
- **Como Ligar:** O projeto já possui a classe oficial `SendGridEmailProvider`. Basta inserir sua própria API Key no `appsettings.json` de produção.
- *Alternativas Grátis:* Se o volume aumentar drasticamente no futuro, o **Mailgun** ou o **Brevo (Sendinblue)** também contam com ótimas cotas grátis.

## 2. SMS: Gateways Locais (Custo Mínimo/Prepago)
O provedor atual `TwilioSmsProvider` é de nível mundial, porém cobra em Dólares (USD). Para campanhas estritas no Brasil, seu uso pode encarecer a operação (aproximadamente R$ 0,15 por SMS enviado).
- **Plano Recomendado:** Compre pacotes pré-pagos (ex: R$ 30,00 ou R$ 50,00) em plataformas e Gateways nacionais, como **SMSDev**, **Zenvia** ou **KingSMS**, que costumam cobrar míseros centavos por envio.
- **Como Ligar:** Crie uma nova classe `NacionalSmsProvider : INotificationProvider`, cole a requisição HTTP pedida por esses provedores `HttpPost(url, json)` usando o `HttpClient` do C#, e registre no `Program.cs`.

## 3. WhatsApp: Alternativa de Custo Zero
O WhatsApp foi a única barreira de mensageria da atualidade. A Meta (dona) cobra oficialmente de US$ 0.05 a US$ 0.08 centavos por cada notificação de empresas (Twilio Business API). Não há conta grátis para produção homologada.
- **Plano Recomendado (Zero Custo):** Use integrações *Não-Oficiais*. Muitos desenvolvedores instalam um script em NodeJS (biblioteca `whatsapp-web.js` ou a plataforma open-source gratuita **Evolution API** / **Baileys**).
- **Como Funciona:** Você espeta um chip pré-pago da Vivo/Claro num celular antigo, sobe a **Evolution API** no seu provedor via Docker, lê o QR Code pelo WhatsApp desse celular teste e pronto! 
- **Como Ligar:** A nossa arquitetura do `NotificationService` apenas fará um POST JSON pelo C# (criando um futuro `EvolutionWhatsAppProvider`) enviando o telefone de destino para o IP da sua Evolution API rodando. Ela encaminhará como se você estivesse digitando no WhatsApp Web. *Atenção: Apenas modere o volume para o chip não ser banido pela Meta em caso de spam.*

---

Esta é a força da nossa `Clean Architecture`. Mudar de provedor global pago (Twilio) para soluções orgânicas e gratuitas requer apenas criar **um arquivo de classe novo** implementando `INotificationProvider`, sem mexer num milésimo do motor de Orquestração, Domain ou RabbitMQ!
