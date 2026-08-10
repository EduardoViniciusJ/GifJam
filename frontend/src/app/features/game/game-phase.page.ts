import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideCheck,
  lucideChevronLeft,
  lucideChevronRight,
  lucideClock,
  lucideCrown,
  lucideLoaderCircle,
  lucideMessageSquare,
  lucidePaperclip,
  lucidePlay,
  lucideSearch,
  lucideSend,
  lucideSparkles,
  lucideTrophy,
  lucideUsers,
  lucideVote,
} from '@ng-icons/lucide';
import { interval } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { GifApiService } from '@core/games/gif-api.service';
import { GameRealtimeService } from '@core/games/game-realtime.service';
import {
  GifSearchItem,
  PlayerGifSnapshot,
  PlayerPhraseSnapshot,
  RevealedGifSnapshot,
} from '@core/games/game.models';
import { GameStore } from '@core/games/game.store';
import { ApiProblemError } from '@core/models/problem-details.model';

@Component({
  selector: 'app-game-phase',
  imports: [CommonModule, FormsModule, NgIcon, RouterLink],
  providers: [
    provideIcons({
      lucideCheck,
      lucideChevronLeft,
      lucideChevronRight,
      lucideClock,
      lucideCrown,
      lucideLoaderCircle,
      lucideMessageSquare,
      lucidePaperclip,
      lucidePlay,
      lucideSearch,
      lucideSend,
      lucideSparkles,
      lucideTrophy,
      lucideUsers,
      lucideVote,
    }),
  ],
  template: `
    @if (round(); as round) {
      <main
        class="phase-page"
        [class.phase-page--results]="round.phase === 'Results' || round.phase === 'Completed'"
        aria-live="polite"
      >
        <header class="phase-header">
          <div>
            <p class="eyebrow eyebrow--dark">
              RODADA {{ round.roundNumber }} DE {{ totalRounds() }}
            </p>
            <h1>{{ phaseTitle(round.phase) }}</h1>
          </div>
          <div class="phase-timer" [class.phase-timer--urgent]="remainingSeconds() <= 10">
            <ng-icon name="lucideClock" aria-hidden="true" />
            <span>{{ formatSeconds(remainingSeconds()) }}</span>
          </div>
        </header>

        @if (round.phase === 'PhraseSubmission') {
          <section class="phase-panel phrase-panel" aria-labelledby="phrase-title">
            <div class="phase-panel__intro">
              <span class="phase-panel__icon"><ng-icon name="lucideMessageSquare" /></span>
              <div>
                <h2 id="phrase-title">Crie uma frase para esta rodada</h2>
                <p>Escreva algo que seus amigos possam interpretar com um GIF.</p>
              </div>
            </div>

            @if (submissionProgress(); as progress) {
              <div class="phase-progress" aria-live="polite">
                <span class="phase-progress__bar" aria-hidden="true">
                  <i
                    [style.width.%]="progressPercentage(progress.completed, progress.eligible)"
                  ></i>
                </span>
                <span>
                  {{ progress.completed }} de {{ progress.eligible }} jogadores concluíram esta
                  etapa.
                </span>
              </div>
            }

            @if (round.hasSubmittedPhrase) {
              <div class="submitted-state">
                <ng-icon name="lucideCheck" aria-hidden="true" />
                <strong>Frase enviada</strong>
                <span>Aguardando os outros jogadores.</span>
              </div>
            } @else {
              <label class="sr-only" for="phrase-input">Sua frase</label>
              <textarea
                id="phrase-input"
                rows="5"
                maxlength="180"
                [value]="phraseText()"
                placeholder="Quando você percebe que..."
                (input)="updatePhrase($event)"
              ></textarea>
              <div class="field-footer">
                <span>{{ phraseText().length }}/180</span>
                <button
                  class="button button--primary"
                  type="button"
                  [disabled]="!canSubmitPhrase() || pending()"
                  (click)="submitPhrase()"
                >
                  @if (pending()) {
                    <ng-icon name="lucideLoaderCircle" class="button-spinner" aria-hidden="true" />
                  } @else {
                    <ng-icon name="lucideSend" aria-hidden="true" />
                  }
                  Enviar frase
                </button>
              </div>
            }
          </section>
        }

        @if (round.phase === 'PhraseVoting') {
          <section class="phase-panel" aria-labelledby="phrase-vote-title">
            <div class="phase-panel__intro">
              <span class="phase-panel__icon"><ng-icon name="lucideVote" /></span>
              <div>
                @if (isAiPhraseMode()) {
                  <h2 id="phrase-vote-title">Escolha a melhor frase da IA</h2>
                  <p>
                    As frases foram criadas para esta sala. Vote na que combina melhor com um GIF.
                  </p>
                } @else {
                  <h2 id="phrase-vote-title">Escolha a frase mais engraçada</h2>
                  <p>Seu voto é anônimo. A sua própria frase está marcada e não pode ser votada.</p>
                }
              </div>
            </div>
            @if (submissionProgress(); as progress) {
              <div class="phase-progress" aria-live="polite">
                <span class="phase-progress__bar" aria-hidden="true">
                  <i
                    [style.width.%]="progressPercentage(progress.completed, progress.eligible)"
                  ></i>
                </span>
                <span>
                  {{ progress.completed }} de {{ progress.eligible }} jogadores concluíram esta
                  etapa.
                </span>
              </div>
            }
            <div class="phrase-options">
              @for (phrase of round.phrases; track phrase.id) {
                <button
                  class="phrase-option"
                  [class.phrase-option--own]="phrase.isOwn"
                  [class.phrase-option--selected]="selectedPhraseId() === phrase.id"
                  [disabled]="phrase.isOwn || round.hasVotedPhrase || pending()"
                  type="button"
                  (click)="votePhrase(phrase)"
                >
                  <span>{{ phrase.text }}</span>
                  @if (phrase.isOwn) {
                    <small>Esta é a sua frase</small>
                  } @else if (selectedPhraseId() === phrase.id) {
                    <ng-icon name="lucideCheck" aria-hidden="true" />
                  }
                </button>
              } @empty {
                <p class="empty-state">Aguardando frases para votar.</p>
              }
            </div>
            @if (round.hasVotedPhrase) {
              <div class="submitted-state">
                <ng-icon name="lucideCheck" aria-hidden="true" />
                <strong>Voto enviado</strong>
                <span>Aguardando os outros jogadores.</span>
              </div>
            }
          </section>
        }

        @if (round.phase === 'GifSubmission') {
          <section class="phase-panel gif-panel" aria-labelledby="gif-title">
            <div class="phase-panel__intro">
              <span class="phase-panel__icon"><ng-icon name="lucidePaperclip" /></span>
              <div>
                <h2 id="gif-title">Escolha seu GIF</h2>
                <p class="selected-phrase">
                  {{ round.selectedPhrase?.text ?? 'Frase selecionada' }}
                </p>
              </div>
            </div>

            <form class="gif-search" (ngSubmit)="searchGifs()">
              <label class="sr-only" for="gif-search-input">Buscar GIF</label>
              <ng-icon name="lucideSearch" aria-hidden="true" />
              <input
                id="gif-search-input"
                name="gifSearch"
                type="search"
                minlength="2"
                maxlength="80"
                [ngModel]="gifQuery()"
                placeholder="gato bravo, reação surpresa..."
                (ngModelChange)="gifQuery.set($event)"
              />
              <button class="button button--outline" type="submit" [disabled]="searching()">
                @if (searching()) {
                  <ng-icon name="lucideLoaderCircle" class="button-spinner" aria-hidden="true" />
                } @else {
                  <ng-icon name="lucideSearch" aria-hidden="true" />
                }
                Buscar
              </button>
            </form>

            <div class="gif-suggestions" aria-label="Sugestões de busca">
              <span><ng-icon name="lucideSparkles" aria-hidden="true" /> Sugestões</span>
              @for (suggestion of gifSuggestions; track suggestion) {
                <button
                  type="button"
                  [disabled]="searching()"
                  (click)="searchSuggestion(suggestion)"
                >
                  {{ suggestion }}
                </button>
              }
            </div>

            @if (selectedGif(); as selectedGif) {
              <div class="selected-gif-note">
                <ng-icon name="lucideCheck" aria-hidden="true" />
                GIF selecionado: {{ selectedGif.description || 'sem descrição' }}
              </div>
            }

            <div class="gif-grid">
              @for (gif of gifResults(); track gif.id) {
                <button
                  class="gif-tile"
                  [class.gif-tile--selected]="selectedGif()?.id === gif.id"
                  type="button"
                  (click)="selectGif(gif)"
                >
                  <img
                    [src]="gif.previewUrl"
                    [alt]="gif.description || 'GIF disponível para seleção'"
                    [width]="gif.previewWidth || 320"
                    [height]="gif.previewHeight || 180"
                    loading="lazy"
                  />
                  @if (selectedGif()?.id === gif.id) {
                    <span class="gif-tile__check"><ng-icon name="lucideCheck" /></span>
                  }
                </button>
              } @empty {
                @if (searching()) {
                  @for (item of [1, 2, 3, 4, 5, 6, 7, 8]; track item) {
                    <span class="gif-skeleton" aria-hidden="true"></span>
                  }
                } @else {
                  <p class="empty-state">Nenhum GIF encontrado. Tente outra reação.</p>
                }
              }
            </div>

            @if (nextCursor()) {
              <button
                class="button button--outline load-more"
                type="button"
                (click)="loadMoreGifs()"
              >
                Carregar mais
              </button>
            }

            @if (round.hasSubmittedGif) {
              <div class="submitted-state">
                <ng-icon name="lucideCheck" aria-hidden="true" />
                <strong>GIF enviado</strong>
                <span>Você pode substituir a seleção até o fim da fase.</span>
              </div>
            }
            <button
              class="button button--primary button--large gif-submit"
              type="button"
              [disabled]="!selectedGif() || pending()"
              (click)="submitGif()"
            >
              @if (pending()) {
                <ng-icon name="lucideLoaderCircle" class="button-spinner" aria-hidden="true" />
              } @else {
                <ng-icon name="lucideSend" aria-hidden="true" />
              }
              {{ round.hasSubmittedGif ? 'Substituir GIF' : 'Enviar GIF' }}
            </button>
            <p class="provider-note">Powered by KLIPY. Atribuição fornecida pelo provedor.</p>
          </section>
        }

        @if (round.phase === 'GifVoting') {
          <section class="phase-panel gif-voting-panel" aria-labelledby="gif-vote-title">
            <div class="phase-panel__intro">
              <span class="phase-panel__icon"><ng-icon name="lucideVote" /></span>
              <div>
                <h2 id="gif-vote-title">
                  {{ gifPresentationActive() ? 'Hora do show' : 'Vote no GIF perfeito' }}
                </h2>
                <p>
                  {{
                    gifPresentationActive()
                      ? 'Cada resposta aparece sozinha por 5 segundos.'
                      : 'Agora escolha a melhor resposta. Seu próprio GIF não pode receber seu voto.'
                  }}
                </p>
              </div>
            </div>

            @if (activeVotingGif(); as gif) {
              <div class="gif-stage" [class.gif-stage--presenting]="gifPresentationActive()">
                <div class="gif-stage__status">
                  <span>
                    {{ gifPresentationActive() ? 'APRESENTANDO' : 'ESCOLHA SEU FAVORITO' }}
                  </span>
                  <strong> {{ activeVotingGifIndex() + 1 }} de {{ round.gifs.length }} </strong>
                </div>
                <div class="gif-stage__media">
                  <img [src]="gif.mediaUrl" [alt]="gif.description || 'GIF concorrente'" />
                </div>

                <div class="gif-stage__dots" aria-hidden="true">
                  @for (item of round.gifs; track item.id; let index = $index) {
                    <i [class.gif-stage__dot--active]="index === activeVotingGifIndex()"></i>
                  }
                </div>

                @if (gifPresentationActive()) {
                  <p class="gif-stage__countdown">
                    Os votos abrem depois da apresentação completa.
                  </p>
                } @else if (round.hasVotedGif) {
                  <div class="submitted-state">
                    <ng-icon name="lucideCheck" aria-hidden="true" />
                    <strong>Voto enviado</strong>
                    <span>Aguardando a revelação.</span>
                  </div>
                } @else {
                  <div class="gif-stage__controls">
                    <button
                      class="icon-button"
                      type="button"
                      aria-label="GIF anterior"
                      (click)="previousVotingGif()"
                    >
                      <ng-icon name="lucideChevronLeft" aria-hidden="true" />
                    </button>
                    <button
                      class="button button--primary button--large"
                      type="button"
                      [disabled]="gif.isOwn || pending()"
                      (click)="voteGif(gif)"
                    >
                      <ng-icon name="lucideVote" aria-hidden="true" />
                      {{ gif.isOwn ? 'Este é o seu GIF' : 'Votar neste GIF' }}
                    </button>
                    <button
                      class="icon-button"
                      type="button"
                      aria-label="Próximo GIF"
                      (click)="nextVotingGif()"
                    >
                      <ng-icon name="lucideChevronRight" aria-hidden="true" />
                    </button>
                  </div>
                }
              </div>
            } @else {
              <p class="empty-state">Aguardando GIFs enviados.</p>
            }

            @if (!gifPresentationActive() && submissionProgress(); as progress) {
              <div class="phase-progress" aria-live="polite">
                <span class="phase-progress__bar" aria-hidden="true">
                  <i
                    [style.width.%]="progressPercentage(progress.completed, progress.eligible)"
                  ></i>
                </span>
                <span>{{ progress.completed }} de {{ progress.eligible }} votos enviados.</span>
              </div>
            }
          </section>
        }

        @if (round.phase === 'Results' || round.phase === 'Completed') {
          <section class="results-experience" aria-labelledby="results-title">
            @if (round.reveal; as reveal) {
              @if (reveal.phrase; as phrase) {
                <div class="revealed-phrase">
                  <span>Frase escolhida</span>
                  <strong>“{{ phrase.text }}”</strong>
                  @if (phrase.author; as author) {
                    <small>por {{ author.displayName }}</small>
                  } @else {
                    <small>Frase gerada pela IA</small>
                  }
                </div>
              }

              <div class="winner-layout">
                <div class="winner-zone">
                  <div class="winner-zone__title">
                    <ng-icon name="lucideCrown" aria-hidden="true" />
                    <div>
                      <p class="eyebrow eyebrow--dark">
                        {{
                          winningGifs(reveal.gifs).length > 1 ? 'GIFS VENCEDORES' : 'GIF VENCEDOR'
                        }}
                      </p>
                      <h2 id="results-title">
                        {{
                          round.phase === 'Completed'
                            ? 'Campeões da partida'
                            : 'Destaques da rodada'
                        }}
                      </h2>
                    </div>
                  </div>

                  <div class="winner-grid">
                    @for (gif of winningGifs(reveal.gifs); track gif.id) {
                      <article class="winner-feature">
                        <img [src]="gif.mediaUrl" [alt]="gif.description || 'GIF vencedor'" />
                        <footer>
                          <div class="winner-author">
                            @if (gif.author.avatarUrl) {
                              <img [src]="gif.author.avatarUrl" alt="" />
                            } @else {
                              <span>{{ gif.author.displayName.charAt(0) }}</span>
                            }
                            <div>
                              <strong>{{ gif.author.displayName }}</strong>
                              <small
                                >{{ gif.voteCount }}
                                {{ gif.voteCount === 1 ? 'voto' : 'votos' }}</small
                              >
                            </div>
                          </div>
                          <b>+{{ gif.voteCount }} pts</b>
                        </footer>
                      </article>
                    } @empty {
                      <p class="empty-state">A rodada terminou sem GIF vencedor.</p>
                    }
                  </div>
                </div>

                @if (round.ranking; as ranking) {
                  <aside class="winner-ranking" aria-label="Ranking da partida">
                    <div class="winner-ranking__title">
                      <ng-icon name="lucideTrophy" aria-hidden="true" />
                      <h3>{{ ranking.isFinal ? 'Ranking final' : 'Ranking geral' }}</h3>
                    </div>
                    <div class="ranking-list">
                      @for (entry of ranking.entries; track entry.userId) {
                        <div class="ranking-row" [class.ranking-row--leader]="entry.position === 1">
                          <strong>{{ entry.position }}</strong>
                          @if (entry.avatarUrl) {
                            <img [src]="entry.avatarUrl" alt="" />
                          } @else {
                            <span class="ranking-avatar">{{ entry.displayName.charAt(0) }}</span>
                          }
                          <span>{{ entry.displayName }}</span>
                          <b>{{ entry.score }} pts</b>
                        </div>
                      }
                    </div>
                  </aside>
                }
              </div>

              @if (otherGifs(reveal.gifs).length > 0) {
                <section class="other-submissions" aria-labelledby="other-submissions-title">
                  <h3 id="other-submissions-title">Outros envios</h3>
                  <div class="revealed-gifs">
                    @for (gif of otherGifs(reveal.gifs); track gif.id) {
                      <article class="revealed-gif">
                        <img [src]="gif.previewUrl" [alt]="gif.description || 'GIF revelado'" />
                        <footer>
                          <span>{{ gif.author.displayName }}</span>
                          <strong
                            >{{ gif.voteCount }}
                            {{ gif.voteCount === 1 ? 'voto' : 'votos' }}</strong
                          >
                        </footer>
                      </article>
                    }
                  </div>
                </section>
              }
            } @else {
              <p class="empty-state">Revelando o resultado...</p>
            }

            @if (round.phase === 'Results') {
              <div class="results-ready-bar">
                <div>
                  <p class="next-round-note">
                    <ng-icon name="lucideClock" aria-hidden="true" />
                    Próxima rodada em {{ formatSeconds(remainingSeconds()) }}
                  </p>
                  @if (submissionProgress(); as progress) {
                    <small
                      >{{ progress.completed }} de {{ progress.eligible }} jogadores prontos</small
                    >
                  }
                </div>
                <button
                  class="button button--primary button--large"
                  type="button"
                  [disabled]="round.hasConfirmedResults || pending()"
                  (click)="confirmResults()"
                >
                  <ng-icon name="lucideCheck" aria-hidden="true" />
                  {{ round.hasConfirmedResults ? 'Você está pronto' : 'Pronto para continuar' }}
                </button>
              </div>
            } @else {
              <a class="button button--outline final-home-link" routerLink="/">
                <ng-icon name="lucidePlay" aria-hidden="true" />
                Voltar ao início
              </a>
            }
          </section>
        }

        @if (message()) {
          <p class="phase-notice" aria-live="assertive">{{ message() }}</p>
        }
      </main>
    } @else {
      <main class="route-state" aria-live="polite">
        <ng-icon class="status-page__spinner" name="lucideLoaderCircle" aria-hidden="true" />
        <h1>Sincronizando a partida</h1>
        <p>Aguardando o estado atual do servidor.</p>
      </main>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GamePhasePage implements OnInit {
  readonly gameCode = input.required<string>();

  private readonly store = inject(GameStore);
  private readonly realtime = inject(GameRealtimeService);
  private readonly gifApi = inject(GifApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly round = this.store.round;
  readonly submissionProgress = this.store.submissionProgress;
  readonly totalRounds = computed(() => this.store.lobby()?.totalRounds ?? 0);
  readonly isAiPhraseMode = computed(() => this.store.lobby()?.mode === 'AiRandomPhrases');
  readonly phraseText = signal('');
  readonly selectedPhraseId = signal<string | null>(null);
  readonly selectedGifId = signal<string | null>(null);
  readonly gifQuery = signal('');
  readonly gifSuggestions = ['reações', 'risada', 'surpresa', 'não acredito', 'comemoração'];
  readonly gifResults = signal<GifSearchItem[]>([]);
  readonly nextCursor = signal<string | null>(null);
  readonly selectedGif = signal<GifSearchItem | null>(null);
  readonly votingCarouselIndex = signal(0);
  readonly searching = signal(false);
  readonly pending = signal(false);
  readonly message = signal('');
  private readonly now = signal(Date.now());
  private readonly serverClockOffset = signal(0);
  private readonly lastSearchQuery = signal('reações');
  private lastSuggestedRound = 0;
  private lastVotingRound = 0;
  private readonly rejectionEffect = effect(() => {
    const rejection = this.realtime.lastCommandRejected();
    if (rejection) {
      this.message.set(rejection.message);
    }
  });
  private readonly clockSyncEffect = effect(() => {
    const serverTime = this.round()?.serverTime;
    if (serverTime) {
      this.serverClockOffset.set(Date.parse(serverTime) - Date.now());
    }
  });
  private readonly serverNow = computed(() => this.now() + this.serverClockOffset());

  readonly remainingSeconds = computed(() => {
    const phaseEndsAt = this.round()?.phaseEndsAt;
    if (!phaseEndsAt) {
      return 0;
    }

    return Math.max(0, Math.ceil((Date.parse(phaseEndsAt) - this.serverNow()) / 1_000));
  });

  readonly gifPresentationActive = computed(() => {
    const round = this.round();
    const presentationEndsAt = round?.gifVotingPresentationEndsAt;
    return (
      round?.phase === 'GifVoting' &&
      Boolean(presentationEndsAt) &&
      Date.parse(presentationEndsAt ?? '') > this.serverNow()
    );
  });

  readonly activeVotingGifIndex = computed(() => {
    const round = this.round();
    if (!round || round.gifs.length === 0) {
      return 0;
    }

    if (this.gifPresentationActive() && round.gifVotingPresentationEndsAt) {
      const presentationEndsAt = Date.parse(round.gifVotingPresentationEndsAt);
      const presentationStartedAt = presentationEndsAt - round.gifs.length * 5_000;
      const index = Math.floor((this.serverNow() - presentationStartedAt) / 5_000);
      return Math.min(round.gifs.length - 1, Math.max(0, index));
    }

    return Math.min(round.gifs.length - 1, Math.max(0, this.votingCarouselIndex()));
  });

  readonly activeVotingGif = computed(
    () => this.round()?.gifs[this.activeVotingGifIndex()] ?? null,
  );

  private readonly phaseExperienceEffect = effect(() => {
    const round = this.round();
    if (round?.phase === 'GifSubmission' && this.lastSuggestedRound !== round.roundNumber) {
      this.lastSuggestedRound = round.roundNumber;
      this.gifQuery.set('');
      this.gifResults.set([]);
      this.selectedGif.set(null);
      this.fetchGifs('reações', null, false);
    }

    if (round?.phase === 'GifVoting' && this.lastVotingRound !== round.roundNumber) {
      this.lastVotingRound = round.roundNumber;
      this.votingCarouselIndex.set(0);
    }
  });

  ngOnInit(): void {
    interval(1_000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.now.set(Date.now()));
  }

  updatePhrase(event: Event): void {
    const target = event.target;
    if (target instanceof HTMLTextAreaElement) {
      this.phraseText.set(target.value.slice(0, 180));
    }
  }

  canSubmitPhrase(): boolean {
    return this.phraseText().trim().length > 0 && this.remainingSeconds() > 0;
  }

  submitPhrase(): void {
    const text = this.phraseText().trim();
    if (!this.canSubmitPhrase() || !text) {
      return;
    }

    void this.runCommand(() => this.realtime.submitPhrase(this.gameCode(), text));
  }

  votePhrase(phrase: PlayerPhraseSnapshot): void {
    if (phrase.isOwn || this.round()?.hasVotedPhrase || this.remainingSeconds() === 0) {
      return;
    }

    this.selectedPhraseId.set(phrase.id);
    void this.runCommand(() => this.realtime.votePhrase(this.gameCode(), phrase.id));
  }

  searchGifs(): void {
    const query = this.gifQuery().trim();
    if (query.length < 2 || this.searching()) {
      this.message.set('Digite pelo menos 2 caracteres para buscar um GIF.');
      return;
    }

    this.nextCursor.set(null);
    this.gifResults.set([]);
    this.fetchGifs(query, null, false);
  }

  searchSuggestion(suggestion: string): void {
    if (this.searching()) {
      return;
    }

    this.gifQuery.set(suggestion);
    this.nextCursor.set(null);
    this.gifResults.set([]);
    this.fetchGifs(suggestion, null, false);
  }

  loadMoreGifs(): void {
    const cursor = this.nextCursor();
    const query = this.lastSearchQuery();
    if (!cursor || !query || this.searching()) {
      return;
    }

    this.fetchGifs(query, cursor, true);
  }

  selectGif(gif: GifSearchItem): void {
    if (this.remainingSeconds() === 0) {
      return;
    }

    this.selectedGif.set(gif);
  }

  submitGif(): void {
    const selection = this.selectedGif();
    if (!selection || this.remainingSeconds() === 0) {
      return;
    }

    void this.runCommand(() => this.realtime.submitGif(this.gameCode(), selection.selectionToken));
  }

  voteGif(gif: PlayerGifSnapshot): void {
    if (
      gif.isOwn ||
      this.gifPresentationActive() ||
      this.round()?.hasVotedGif ||
      this.remainingSeconds() === 0
    ) {
      return;
    }

    this.selectedGifId.set(gif.id);
    void this.runCommand(() => this.realtime.voteGif(this.gameCode(), gif.id));
  }

  previousVotingGif(): void {
    const count = this.round()?.gifs.length ?? 0;
    if (count === 0 || this.gifPresentationActive()) {
      return;
    }

    this.votingCarouselIndex.update((index) => (index - 1 + count) % count);
  }

  nextVotingGif(): void {
    const count = this.round()?.gifs.length ?? 0;
    if (count === 0 || this.gifPresentationActive()) {
      return;
    }

    this.votingCarouselIndex.update((index) => (index + 1) % count);
  }

  confirmResults(): void {
    if (this.round()?.hasConfirmedResults || this.pending() || this.remainingSeconds() === 0) {
      return;
    }

    void this.runCommand(() => this.realtime.setResultsReady(this.gameCode()));
  }

  winningGifs(gifs: RevealedGifSnapshot[]): RevealedGifSnapshot[] {
    return gifs.filter((gif) => gif.position === 1);
  }

  otherGifs(gifs: RevealedGifSnapshot[]): RevealedGifSnapshot[] {
    return gifs.filter((gif) => gif.position !== 1);
  }

  phaseTitle(phase: string): string {
    switch (phase) {
      case 'PhraseSubmission':
        return 'Crie sua frase';
      case 'PhraseVoting':
        return 'Vote na melhor frase';
      case 'GifSubmission':
        return 'Escolha seu GIF';
      case 'GifVoting':
        return 'Vote no melhor GIF';
      case 'Results':
        return 'Revelação';
      case 'Completed':
        return 'Ranking final';
      default:
        return 'Partida';
    }
  }

  formatSeconds(seconds: number): string {
    const minutes = Math.floor(seconds / 60);
    const remainder = seconds % 60;
    return `${String(minutes).padStart(2, '0')}:${String(remainder).padStart(2, '0')}`;
  }

  progressPercentage(completed: number, eligible: number): number {
    if (eligible <= 0) {
      return 0;
    }

    return Math.min(100, Math.max(0, (completed / eligible) * 100));
  }

  private fetchGifs(query: string, cursor: string | null, append: boolean): void {
    this.searching.set(true);
    this.message.set('');
    this.lastSearchQuery.set(query);
    this.gifApi
      .search(this.gameCode(), query, cursor)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.gifResults.update((items) =>
            append ? [...items, ...response.items] : response.items,
          );
          this.nextCursor.set(response.nextCursor);
          this.searching.set(false);
        },
        error: (error: unknown) => {
          this.searching.set(false);
          this.message.set(
            error instanceof ApiProblemError
              ? error.message
              : 'Não foi possível buscar GIFs agora. Tente novamente.',
          );
        },
      });
  }

  private async runCommand(command: () => Promise<void>): Promise<void> {
    if (this.pending()) {
      return;
    }

    this.pending.set(true);
    this.message.set('');
    this.realtime.clearCommandRejection();
    try {
      await command();
      await this.realtime.requestSync(this.gameCode());
    } catch {
      this.message.set('A ação não foi enviada. O estado será sincronizado novamente.');
    } finally {
      this.pending.set(false);
    }
  }
}
