# Guia de Testes Manuais com xUnit via Visual Studio

A classe de integração e testes Ponta-A-Ponta (E2E) foi configurada utilizando o Framework de injeção `xUnit` diretamente via projeto C#. 

Os testes foram preparados em formato `Fact`, desenhados para conectar na sua fila do RabbitMQ local.

Este guia prático documenta como engatilhar e analisar os três disparos disponíveis (`SMS`, `WhatsApp`, `E-mail`).

---

## Passo 1: Subindo as Dependências
Estes testes injetarão mensagens na fila `notification.queue` esperando que um worker as processe.
O RabbitMQ deve estar rodando na máquina:
1. Abra o serviço RabbitMQ `localhost:15672` e verifique a porta de conexão `5672`.
2. Para acompanhamento de Logs do NotificationService e constatar o processamento das mensagens, execute o projeto **`NotificationService.Api`** apertando **F5** no Visual Studio.

## Passo 2: Acessando o Teste no Visual Studio
Pelo Visual Studio:
1. Abra a pasta `tests` no seu _Gerenciador de Soluções_ (Solution Explorer).
2. Vá ao caminho `NotificationService.Tests` > `IntegrationTests`.
3. Dê `Duplo Clique` no arquivo `NotificationE2ETests.cs`.

## Passo 3: Configurando o Método desejado
Atualmente, as anotações `[Fact]` dos 3 métodos do arquivo finalizam com a instrução `(Skip = "Teste manual...")`. O Visual Studio **pulará** esses testes se rodados assim. Ele usa essa estratégia para evitar falhas caso a pipeline rode numa esteira remota que não possua o RabbitMQ (como Github Actions/TFS).

Para rodar um teste em específico:
1. Mude a assinatura do Fact removendo o Skip: 
   * **De:** `[Fact(Skip = "Teste manual de WhatsApp. Remova o parâmetro 'Skip' para executar.")]`
   * **Para:** `[Fact]`
2. O botãozinho verde de disparo (CodeLens) deve brotar acima da assinatura no Visual Studio.
3. Aperte `Control + S` para salvar o arquivo de testes.

## Passo 4: Executando no Gerenciador de Testes
Caso o botão verde do CodeLens não esteja visível:
1. No menu superior do Visual Studio vá até **Teste > Gerenciador de Testes** (Test > Test Explorer).
2. Expanda os elementos da árvore à esquerda:
   * `NotificationService.Tests` > `NotificationService.Tests.IntegrationTests` > `NotificationE2ETests`.
3. Ele destacará individualmente cada um dos três canais (`Deve_Publicar_NotificacaoWhatsApp_Na_Fila_RabbitMQ`, por exemplo).
4. Clique com o botão direito sobre um dos testes e vá em **`Executar`** (Run).

## Passo 5: Avaliando o Retorno
- **Se o xUnit der Check Verde (Aprovado):**
  A classe conseguiu conectar na Fila por debaixo dos panos, despachou o JSON estático construído pelo código C# e encerrou a conexão de modo saudável. Sua mensagem já está na Fila ou processada.
- **Se o xUnit der X Vermelho (Falha):**
  Aperte no teste falho, no painel direito aparecerá a "Mensagem da Exceção". Provavelmente seu RabbitMQ local não está acessível no bind padrão `localhost`.

### Acompanhando o Disparo
Com a API rodando:
Vá ao painel da sua Aplicação do NotificationService que estará em execução e observe seus Logs nativos. Textos como `[Twilio] Iniciando envio de WhatsApp para +5511970823248` demonstrarão que a Notificação seguiu perfeitamente na sua ponta pelo Padrão Strategy!

---

## Passo 6: Configurando o Seu Próprio Twilio (Para Novos Devs)

Atenção: A filial de código original contém chaves de Trial estáticas do desenvolvedor principal. **Para não onerar os créditos dessa conta ou evitar erros de "unverified number"**, você deve sobrescrever as chaves localmente na sua máquina com a sua própria conta!

### A. Criando a Conta e Validando seu Número
1. Acesse [Twilio.com](https://www.twilio.com/) e clique em **"Sign Up"** (Criar conta gratuita).
2. Preencha seus dados, confirme o e-mail e, em seguida, o Twilio pedirá para você **verificar um número de telefone**. Coloque o seu celular pessoal real (com DDD). 
   *Nota: O Twilio em modo Trial (teste) só tem permissão para enviar SMS/WhatsApp para números que você tiver verificado e provado que são seus na plataforma.*

### B. Obtendo as Chaves de Acesso (API Credentials)
1. Após logar, você cairá no painel principal, o **Twilio Console**.
2. Role a página para baixo até encontrar a seção **"Account Info"**.
3. Copie o valor do campo **Account SID** (começa com "AC...").
4. No campo de baixo, **Auth Token**, clique em "Show" (mostrar) e copie a senha gigante que aparecer. Guarde essas duas informações no bloco de notas.

### C. Configurando o WhatsApp (Sandbox)
O WhatsApp não permite envios diretos sem aprovação da Meta. Por isso, usamos o "Sandbox" (Caixa de Areia) do Twilio para testar livremente.
1. No menu lateral esquerdo do Twilio, expanda **"Messaging"**.
2. Clique em **"Try it out"** e depois selecione **"Send a WhatsApp message"**.
3. A tela mostrará um número de telefone americano do Twilio (ex: `+1 415 523 8886`) e um código em negrito dizendo *"Join <alguma-palavra>"* (ex: `join force-nature`).
4. **No seu WhatsApp real do celular:** Salve esse número americano na agenda e mande uma mensagem para ele com a exata frase gerada (ex: `join force-nature`).
5. O Twilio vai responder na hora: *"Twilio Sandbox: You are all set!"*. Isso significa que seu celular e o Sandbox estão pareados por 24 horas. (Se for testar amanhã de novo, precisa mandar a frase novamente!).

### D. Injetando no Código C#
1. Abra os arquivos `TwilioSmsProvider.cs` e `TwilioWhatsAppProvider.cs` na pasta `src/NotificationService.Infrastructure/Messaging/Providers`.
2. Substitua os valores das propriedades `_accountSid` e `_authToken` pelos seus dados copiados no Passo B.
3. Abra a classe de testes `NotificationE2ETests.cs` na pasta de integração.
4. Altere a string `+5511970823248` para o SEU número de celular real que você cadastrou no Passo A (não esqueça do código do país +55).
5. Tudo pronto! Aperte F5 na API e dê o Play no teste pelo Visual Studio!

---

## Passo 7: Testando na Nuvem sem Instalar Nada (Testcontainers)

Se você não possui o RabbitMQ instalado na sua máquina e precisa validar se a comunicação com as filas e a conversão do payload do C# estão funcionando:

O projeto possui a inovação de **Testcontainers**. A classe `TestcontainersRabbitMqTests.cs` usará a API do Docker do seu computador para baixar uma imagem do RabbitMQ, subir um servidor real descartável na memória, realizar os envios, coletar os callbacks e destruir o servidor antes de finalizar o TestRunner.

**Requisitos:** Você só precisa estar rodando o aplicativo **Docker Desktop** na sua barra de tarefas do Windows. Não há configurações! Dê duplo-clique no teste via Visual Studio Test Explorer e observe a classe fazer tudo no modo invisível!
