# Configuração do bot do Discord

O bot usa a mesma aplicação do Discord já utilizada pelo login OAuth2 do GifJam. Não crie uma segunda aplicação: o usuário do bot e o login pertencem à mesma aplicação no Discord Developer Portal.

## 1. Criar o usuário do bot

1. Abra o [Discord Developer Portal](https://discord.com/developers/applications).
2. Selecione a aplicação do GifJam.
3. Em **General Information**, configure:
   - **Name:** `GifJam`
   - **Description:** `Crie salas do GifJam diretamente no Discord e convide seus amigos para jogar.`
   - Ícone e banner oficiais do produto.
4. Abra **Bot** e clique em **Add Bot**, caso o usuário do bot ainda não exista.
5. Em **Username**, use `GifJam`.
6. Gere ou redefina o token em **Reset Token** e guarde-o somente no gerenciador de segredos do servidor. Nunca envie o token em chat, screenshot ou commit.

## 2. Intents e opções do bot

O MVP recebe somente interações de slash commands. Mantenha estas opções desativadas:

- **Requires OAuth2 Code Grant:** desativado.
- **Private Channel Obfuscation:** desativado.
- **Presence Intent:** desativado.
- **Server Members Intent:** desativado.
- **Message Content Intent:** desativado.

O bot consegue ficar online e exibir `Jogando GifJam` sem habilitar o Presence Intent. Esse intent serve para receber atualizações de presença de outros usuários.

## 3. Configurar a instalação

1. Abra **Installation**.
2. Habilite **Guild Install** e desabilite **User Install**.
3. Em **Default Install Settings**, adicione os scopes:
   - `bot`
   - `applications.commands` — em português pode aparecer como **Usar comandos de aplicação** ou **Usar comandos de barra**.
4. Nas permissões do bot, marque somente:
   - **Ver canais**
   - **Enviar mensagens**
   - **Inserir links**
   - **Usar comandos de aplicação/barra**

O inteiro de permissões esperado é `2147503104`. Não habilite **Administrador**.

Não configure uma **Interactions Endpoint URL**. Esta implementação recebe comandos por uma conexão persistente com o Discord Gateway.

## 4. Instalar no servidor

1. Ainda em **Installation**, copie o link de instalação gerado.
2. Abra o link, escolha o servidor e autorize a instalação.
3. A conta usada na instalação precisa ter permissão para gerenciar o servidor.
4. Confirme que o bot aparece na lista de membros. Ele ficará online quando o backend estiver executando com a integração habilitada.

## 5. Configurar o backend

Use variáveis de ambiente; não coloque o token em `appsettings.json`:

```dotenv
Discord__BotEnabled=true
Discord__BotToken=cole_o_token_somente_no_arquivo_env_do_servidor
Discord__DevelopmentGuildId=123456789012345678
Discord__BotActivity=GifJam
```

Durante o desenvolvimento, `Discord__DevelopmentGuildId` deve conter o ID do servidor de teste. Isso registra o comando imediatamente naquele servidor. Ative o **Developer Mode** do Discord e use **Copy Server ID** para obter o valor.

Em produção, deixe `Discord__DevelopmentGuildId` vazio. O comando será registrado globalmente e pode levar algum tempo para aparecer em todos os servidores.

## 6. Comando do MVP

`/gifjam-create` cria uma sala privada com 3 rodadas, modo clássico, 60 segundos para frases e 60 segundos para resultados.

- O usuário que executa o comando vira o host, mesmo no primeiro acesso ao GifJam.
- O bot publica no canal o código e um botão com o link da sala.
- A partida não inicia automaticamente; o host entra pelo link e inicia quando houver de 2 a 6 jogadores e todos estiverem prontos.
- Se o mesmo usuário já hospedar uma sala em lobby, o bot republica essa sala sem alterar suas configurações ou visibilidade.
- Erros e bloqueios por excesso de uso aparecem somente para quem executou o comando.

## 7. Operação e segurança

- O processo da API mantém a conexão Gateway e a presença online. Reinícios e deploys deixam o bot offline temporariamente.
- O bot usa apenas o intent não privilegiado `Guilds`.
- Mantenha uma única réplica da API enquanto os locks forem locais ao processo.
- Se o token vazar, redefina-o imediatamente no Developer Portal e atualize o segredo do servidor.

## 8. Comandos disponíveis

- `/gifjam-create`: cria uma sala privada ou republica a sala em lobby do usuário.
- `/gifjam-room`: republica no canal o código e o link da sala atual, sem criar outra sala.
- `/gifjam-close`: encerra a sala em lobby hospedada por quem executou o comando; participantes não podem encerrar a sala de outro host.
- `/gifjam-help`: mostra de forma privada os comandos disponíveis.
