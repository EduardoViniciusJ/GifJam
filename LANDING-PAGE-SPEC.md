# GifJam - Especificação da Landing Page

Este documento define estrutura e layout. Não contém headlines, textos finais ou copy.

## Objetivo

Levar um visitante autenticado ou não a criar uma sala ou entrar em uma sala existente com o mínimo de etapas, deixando a dinâmica do jogo visualmente compreensível.

## Estrutura

### 1. Cabeçalho

- Barra horizontal compacta com marca à esquerda.
- Área de sessão à direita: avatar/menu quando autenticado ou ação de login quando anônimo.
- Sem navegação extensa; a experiência jogável é a prioridade.

### 2. Hero Jogável

- Faixa de primeira viewport, sem card externo e sem composição dividida texto/imagem.
- Nome GifJam como principal sinal visual.
- Fundo com mídia real do jogo: mosaico claro de GIFs em movimento, com contraste suficiente para o conteúdo frontal.
- Ações primárias visíveis: criar sala e entrar por código.
- Campo de código com cinco caracteres, associado à ação de entrada.
- Estado não autenticado inicia o OAuth e preserva a intenção de criar/entrar.
- Altura responsiva que sempre deixa uma parte da seção seguinte visível.

### 3. Fluxo da Rodada

- Faixa sem card contêiner, com quatro etapas alinhadas no desktop e trilha vertical no celular.
- Cada etapa usa um recorte real da interface: frase, voto, busca de GIF e revelação.
- Numeração e ícones apenas como apoio visual.

### 4. Prévia da Revelação

- Faixa de contraste leve mostrando um exemplo de GIF vencedor, autoria e ranking.
- Elementos apresentados como interface real, não como ilustração genérica.
- Animação sutil opcional para a revelação, respeitando `prefers-reduced-motion`.

### 5. Chamada Final

- Faixa curta com a mesma prioridade do hero: criar sala como ação principal e entrar por código como secundária.
- Sem formulário extra, depoimentos ou captura de e-mail no MVP.

### 6. Rodapé

- Marca, política de privacidade e termos de uso.
- Atribuição obrigatória “Powered by KLIPY” conforme as regras do provedor.
- Identificação clara de que GifJam é independente e não é afiliado ao Discord ou à KLIPY.

## Hierarquia de Ações

1. Criar sala.
2. Entrar com código.
3. Fazer login ou abrir o menu da conta.
4. Consultar privacidade e termos.

## Estados Necessários

- Visitante anônimo, usuário autenticado e retorno de OAuth.
- Código vazio, incompleto, inválido, expirado ou de partida iniciada.
- Carregamento e erro ao criar/entrar na sala.
- Layout de 320px até desktop amplo, sem sobreposição ou texto cortado.

## Diretrizes de Mídia

- Usar GIFs reais retornados pelo provedor nas prévias do produto.
- Evitar imagens puramente atmosféricas, gradientes decorativos e ilustrações SVG genéricas.
- Reservar dimensões fixas ou `aspect-ratio` para impedir salto de layout durante o carregamento.
- Pausar animações fora da viewport e oferecer imagem estática quando movimento reduzido estiver ativo.
