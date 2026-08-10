# GifJam - Diretrizes de Design

## Direção Visual

Interface clean, moderna e clara, com densidade confortável e energia de party game nos conteúdos, não na decoração. As referências de precisão e hierarquia são Linear, Resend e Vercel; o caráter divertido vem dos GIFs, das revelações e de poucos acentos cromáticos.

## Paleta

| Token | Cor | Uso |
|---|---:|---|
| `background` | `#F7F8FA` | Fundo principal |
| `surface` | `#FFFFFF` | Controles, menus e cards reais |
| `foreground` | `#17181B` | Texto principal |
| `muted-foreground` | `#667085` | Texto secundário |
| `border` | `#DDE1E7` | Divisores e bordas |
| `primary` | `#E5484D` | Ação principal e marca |
| `primary-hover` | `#CE3E43` | Hover da ação principal |
| `secondary` | `#2563EB` | Seleção e informação |
| `success` | `#14804A` | Pronto, conectado e sucesso |
| `warning` | `#B54708` | Tempo baixo e atenção |
| `danger` | `#C4320A` | Erros e ações destrutivas |

- Manter contraste mínimo WCAG AA.
- Não usar gradientes como fundo ou decoração.
- Não deixar uma única família de cor dominar a tela.
- Usar cor para estado, nunca como único indicador.

## Tipografia

- Família: `Geist`, com fallback `Inter, system-ui, sans-serif`.
- Corpo: 16px/24px; texto compacto: 14px/20px.
- Títulos de tela: 28px/34px no desktop e 24px/30px no celular.
- Títulos internos: 18px/24px ou 20px/28px.
- Peso 600 para títulos e 500 para ações; evitar excesso de bold.
- `letter-spacing: 0` em todos os estilos.
- Não escalar fonte diretamente pela largura da viewport.

## Espaçamento e Dimensões

- Escala base de 4px: `4, 8, 12, 16, 24, 32, 48, 64`.
- Conteúdo principal com largura máxima de 1120px.
- Alvos interativos com no mínimo 44x44px.
- Barra de ações e cronômetro com dimensões estáveis entre estados.
- Grades de GIFs com `aspect-ratio` definido e 2 colunas no celular, 3 ou 4 no desktop.

## Bordas e Sombras

- Radius padrão: 6px; cards e modais: no máximo 8px; controles pequenos: 4px.
- Borda padrão de 1px com o token `border`.
- Sombra baixa apenas em menus, popovers e modais: `0 8px 24px rgba(23, 24, 27, 0.10)`.
- Seções de página são faixas sem aparência de cards flutuantes.
- Não aninhar cards dentro de cards.

## Componentes Angular

Usar `spartan/ui` com Tailwind como equivalente Angular do modelo shadcn/ui. Os componentes estilizados ficam no código do projeto; os primitives mantidos pela biblioteca fornecem acessibilidade e comportamento.

| Necessidade | Componente/padrão |
|---|---|
| Ações | Button com ícone Lucide quando existir símbolo conhecido |
| Código da sala | Input OTP ou grupo de inputs de um caractere |
| Quantidade de rodadas | Segmented control para 3, 4, 5 e 6 |
| Pronto | Checkbox ou switch com rótulo |
| Jogadores | Lista compacta com avatar, presença e badge do host |
| Progresso de fase | Progress + cronômetro textual acessível |
| GIFs | Grade selecionável com estado `aria-pressed` |
| Confirmações | Dialog |
| Erros breves | Toast; erros persistentes permanecem junto ao controle |
| Perfil | Dropdown menu |
| Carregamento | Skeleton com dimensões iguais ao conteúdo final |

## Telas do Jogo

- Lobby: lista de jogadores como superfície principal; configuração do host em faixa lateral ou inferior, sem card externo redundante.
- Frase: campo amplo, contador de caracteres e ação única de envio.
- Votação de frase: opções embaralhadas em lista; autoria ausente.
- Busca de GIF: busca fixa no topo e grade rolável; seleção sempre evidente.
- Votação de GIF: todos os GIFs aparecem juntos; a própria submissão fica identificada e desabilitada para voto.
- Resultado: GIFs, autores e votos revelados na mesma ordem embaralhada; vencedores empatados recebem igual destaque.
- Ranking final: tabela simples, ordenada por pontos e com posições compartilhadas.

## Movimento e Feedback

- Transições de 150–220ms para hover, seleção e mudança de fase.
- Revelação pode usar sequência curta de até 600ms, sem bloquear interação.
- Respeitar `prefers-reduced-motion` e oferecer estado estático.
- Nunca usar animação para esconder atraso de rede ou alterar o tamanho do layout.

## Responsividade e Acessibilidade

- Suportar teclado, foco visível e leitores de tela em todos os comandos.
- Não depender de hover; ações essenciais permanecem disponíveis por toque.
- Texto não pode sobrepor GIFs, cronômetro ou controles.
- Em telas estreitas, ações principais ficam em uma barra inferior estável.
- Fornecer texto alternativo útil quando o provedor disponibilizar descrição; caso contrário, identificar o item como GIF selecionável.

## Referências

- Linear: hierarquia compacta, feedback e estados.
- Resend: clareza tipográfica e uso de espaço.
- Vercel: contraste, navegação e superfícies neutras.
- shadcn/ui: tokens e composição visual.
- spartan/ui: implementação Angular acessível e editável.
