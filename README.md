# GifJam

GifJam é um jogo multiplayer em que os jogadores criam frases, escolhem GIFs e votam nas melhores combinações. As partidas acontecem em salas privadas, com autenticação pelo Discord e atualizações em tempo real.

## Principais recursos

- Login com Discord OAuth2.
- Criação e entrada em salas privadas.
- Partidas de 2 a 6 jogadores.
- Fluxo de rodadas com frases, escolha de GIFs, votação e ranking.
- Busca de GIFs usando KLIPY e GIPHY.
- Paginação para carregar mais resultados sem exibir todo o catálogo de uma vez.
- Comunicação em tempo real com SignalR.
- Reconexão e recuperação do estado da partida.

## Tecnologias

- Backend: ASP.NET Core 10, Entity Framework Core, PostgreSQL e SignalR.
- Frontend: Angular 22, TypeScript, Tailwind CSS e Vitest.
- Autenticação: Discord OAuth2 e JWT.
- GIFs: KLIPY e GIPHY.

## Estrutura

```text
backend/    API, regras de negócio, persistência, SignalR e integrações externas
frontend/   Aplicação Angular e interface do jogo
design/     Referências e materiais visuais do produto
```

## Requisitos

- .NET SDK 10.0.302 ou compatível.
- Node.js 24.15.0 ou compatível com Angular 22.
- npm 11 ou superior.
- Docker Desktop, para executar o PostgreSQL localmente.

## Configuração local

1. Clone o repositório e entre na pasta do projeto.

2. Crie o arquivo de ambiente do backend:

   ```powershell
   Copy-Item backend/.env.example backend/.env
   ```

3. Preencha `backend/.env` com as credenciais de desenvolvimento do Discord, KLIPY, GIPHY, Gemini e JWT. Esse arquivo é ignorado pelo Git e não deve ser commitado.

4. Inicie o PostgreSQL:

   ```powershell
   cd backend
   docker compose up -d postgres
   ```

5. Aplique as migrations do banco:

   ```powershell
   ./scripts/Update-Database.ps1
   ```

## Executando o projeto

Em um terminal, inicie a API:

```powershell
./backend/scripts/Start-Api.ps1
```

A API ficará disponível em `https://localhost:7042`.

Em outro terminal, instale as dependências e inicie o frontend:

```powershell
cd frontend
npm ci
npm start
```

A aplicação ficará disponível em `http://localhost:4200`. O proxy do Angular encaminha as chamadas REST e SignalR para a API local.

## Verificações

Para executar as verificações do frontend:

```powershell
cd frontend
npm run check
```

Para executar os testes do backend:

```powershell
cd backend
dotnet test
```

Os testes de integração que usam PostgreSQL precisam do Docker em execução.

## Segurança

- Nunca coloque chaves de API, segredos OAuth, senhas ou strings de conexão em arquivos versionados.
- As credenciais de KLIPY e GIPHY são usadas somente pelo backend.
- Use `backend/.env.example` como referência para configurar um ambiente local.
- Para produção, configure os segredos diretamente no provedor de hospedagem e revise as URLs de CORS e OAuth.
