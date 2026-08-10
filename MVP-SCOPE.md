# GifJam - Escopo do MVP

## Objetivo

Validar se grupos privados de amigos conseguem iniciar, concluir e querer repetir uma partida de GifJam. O MVP prioriza diversão, ritmo e confiabilidade do fluxo multiplayer.

## Must Have

- Login obrigatório com Discord OAuth2 para obter nome, nome de exibição e avatar.
- Criação de sala privada com código e link compartilhável.
- Entrada de 2 a 6 jogadores e lobby em tempo real.
- Indicação de host, presença e estado pronto/aguardando.
- Escolha de 3 a 6 rodadas pelo host.
- Envio anônimo de uma frase por jogador e por rodada.
- Um voto por jogador em uma frase alheia.
- Desempate aleatório de frases, pois uma única frase precisa ser escolhida.
- Pesquisa e escolha de um GIF pela API KLIPY.
- Um voto por jogador em um GIF alheio.
- Um ponto por voto recebido pelo GIF, acumulado no ranking geral.
- Empates de GIF e do ranking compartilhando a mesma posição.
- Cronômetros controlados pelo backend: 30s, 20s, 60s e 20s.
- Avanço automático quando todos os jogadores conectados elegíveis responderem.
- Revelação dos autores apenas no resultado da rodada.
- Ranking final e suporte a reconexão pela mesma conta Discord.
- Interface responsiva para desktop e celular.

## Should Have

- Feedback visual de quantos jogadores já responderam, sem revelar quem ou o conteúdo.
- Estado de indisponibilidade da busca de GIF com tentativa novamente.
- Limpeza automática de partidas e submissões após 24 horas.
- Telemetria técnica mínima: logs de transição, falhas externas e partidas concluídas.

## Could Have

- Botão para copiar código e link da sala.
- Confirmação antes de abandonar uma partida ativa.
- Atalhos de teclado e pequenas animações de revelação.
- Opção de reiniciar com o mesmo grupo depois do ranking final.

## Fora do MVP

- Matchmaking, salas públicas ou descoberta de partidas.
- Bot e Discord Activity.
- Chat, voz ou vídeo dentro do jogo.
- Espectadores e entrada de novos jogadores após o início.
- Perfis, histórico, conquistas e estatísticas permanentes.
- Frases prontas, pacotes temáticos e conteúdo customizado pelo host.
- Moderação avançada, denúncias e painel administrativo.
- Aplicativo nativo, monetização, anúncios e assinatura.
- Escala horizontal, Redis ou Azure SignalR Service.

## Decisões de Escopo

- Dois jogadores são aceitos sem modo especial. Como cada um só pode votar no outro, empates frequentes são um comportamento conhecido e válido.
- A meta de 15 minutos vale para 3 rodadas. Partidas de 4 a 6 rodadas podem durar mais.
- Votos em frases apenas escolhem o prompt; não geram pontos.
- Não haverá bônus de vitória. O ranking soma somente votos recebidos pelos GIFs.
- O backend nunca envia a autoria durante as fases anônimas, mesmo que o frontend pudesse escondê-la visualmente.

## Hipóteses a Validar

- O login Discord é rápido e não impede o grupo de começar.
- Os jogadores entendem as regras sem explicação externa.
- Sessenta segundos são suficientes para encontrar um GIF relevante.
- O voto único no GIF favorito é rápido e parece justo.
- A espera entre fases permanece aceitável com 2 a 6 jogadores.
- O anonimato torna a votação mais divertida e menos enviesada.

## Critérios de Sucesso

- 5 ou mais partidas observadas com grupos reais.
- Taxa de conclusão igual ou superior a 80%.
- Mediana de até 15 minutos para partidas de 3 rodadas.
- Pelo menos 70% dos participantes respondem que jogariam novamente.
- Pelo menos 90% das transições de fase ocorrem sem recarregar a página.
- Nenhum caso observado de voto próprio, voto duplicado ou autoria vazada antes da revelação.

## Riscos Principais

- A busca da KLIPY pode retornar conteúdo irrelevante ou ficar indisponível.
- O OAuth pode criar atrito em navegadores com bloqueio de cookies entre domínios.
- A combinação de quatro fases por rodada pode quebrar o ritmo em grupos lentos.
- Uma única réplica da API é suficiente para o MVP, mas representa ponto único de falha.
- O plano gratuito do Neon pode introduzir pequena latência ao acordar o banco.
