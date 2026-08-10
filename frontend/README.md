# GifJam Frontend

Aplicação Angular 22 do GifJam. Esta base inclui rotas lazy, proxy local para a API e o hub
SignalR, tratamento central de autenticação/`ProblemDetails`, Tailwind, Spartan UI, Lucide,
Vitest, ESLint e Prettier.

## Requisitos

- Node `22.22.3+`, `24.15.0+` ou `26+` conforme o `engines` do Angular 22.
- npm `11+`.
- Backend local em `https://localhost:7042` para chamadas REST e SignalR.

O arquivo `.nvmrc` fixa Node `24.15.0` como versão recomendada para desenvolvimento.

## Executar

```bash
npm ci
npm start
```

A aplicação fica em `http://localhost:4200`. O proxy encaminha `/api` e `/hubs` para o
backend local sem colocar credenciais no bundle do navegador.

## Verificar

```bash
npm run format:check
npm run lint
npm run test:ci
npm run build
npm run build:staging
```

## Estrutura

- `src/app/core`: sessão, interceptors HTTP e modelos compartilhados pela aplicação.
- `src/app/features`: telas carregadas sob demanda por rota.
- `src/app/shared`: componentes visuais reutilizáveis.
- `src/environments`: configurações públicas de local e homologação.
- `public/brand`: ativos otimizados usados pela aplicação.
- `brand-sources`: arquivos mestres em alta resolução da identidade GifJam.

Segredos do Discord, KLIPY e Neon pertencem exclusivamente ao backend e nunca devem ser
adicionados ao frontend.
