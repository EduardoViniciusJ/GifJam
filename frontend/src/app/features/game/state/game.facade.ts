import { DestroyRef, Injectable, computed, effect, inject, signal } from '@angular/core';
import { interval } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { ApiProblemError } from '@core/models/problem-details.model';
import { SoundEffectsService } from '@core/audio/sound-effects.service';
import { GifApiService } from '@features/game/data/gif-api.service';
import { GameRealtimeService } from '@features/game/data/game-realtime.service';
import {
  GifSearchItem,
  PlayerGifSnapshot,
  PlayerPhraseSnapshot,
  RevealedGifSnapshot,
} from '@features/game/data/game.models';
import { GameStore } from '@features/game/state/game.store';

@Injectable()
export class GameFacade {
  private readonly store = inject(GameStore);
  private readonly realtime = inject(GameRealtimeService);
  private readonly gifApi = inject(GifApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly soundEffects = inject(SoundEffectsService);

  private readonly gameCodeState = signal('');
  private readonly now = signal(Date.now());
  private readonly serverClockOffset = signal(0);
  private readonly lastSearchQuery = signal('hello');
  private lastSuggestedRound = 0;
  private lastVotingRound = 0;
  private lastWinnerRevealRound = 0;
  private countdownKey = '';
  private expirationSyncKey = '';
  private expirationSyncStartedAt = 0;
  private expirationSyncOperation: Promise<void> | null = null;

  readonly round = this.store.round;
  readonly totalRounds = computed(() => this.store.lobby()?.totalRounds ?? 0);
  readonly playerNames = computed(
    () => this.store.lobby()?.players.map((player) => player.displayName) ?? [],
  );
  readonly isAiPhraseMode = computed(() => this.store.lobby()?.mode === 'AiRandomPhrases');
  readonly phraseText = signal('');
  readonly selectedPhraseId = signal<string | null>(null);
  readonly selectedGifId = signal<string | null>(null);
  readonly gifQuery = signal('');
  readonly gifCategories: GifCategory[] = [
    { label: 'Hello', query: 'hello' },
    { label: 'LOL', query: 'lol' },
    { label: 'Love', query: 'love' },
    { label: 'Happy Birthday', query: 'happy birthday' },
    { label: 'Thank You', query: 'thank you' },
    { label: 'Excited', query: 'excited' },
    { label: 'Yes', query: 'yes' },
    { label: 'No', query: 'no' },
    { label: 'Sorry', query: 'sorry' },
  ];
  readonly gifResults = signal<GifSearchItem[]>([]);
  readonly gifResultsCount = computed(() => this.gifResults().length);
  readonly nextCursor = signal<string | null>(null);
  readonly selectedGif = signal<GifSearchItem | null>(null);
  readonly votingCarouselIndex = signal(0);
  readonly searching = signal(false);
  readonly pending = signal(false);
  readonly message = signal('');

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

  private readonly serverNow = computed(() => this.now() + this.serverClockOffset());

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

  private readonly phaseExperienceEffect = effect(() => {
    const round = this.round();
    const gameCode = this.gameCodeState();
    if (!gameCode) {
      return;
    }

    if (round?.phase === 'GifSubmission' && this.lastSuggestedRound !== round.roundNumber) {
      this.lastSuggestedRound = round.roundNumber;
      this.gifQuery.set('');
      this.gifResults.set([]);
      this.selectedGif.set(null);
      this.fetchGifs('hello', null, false);
    }

    if (round?.phase === 'GifVoting' && this.lastVotingRound !== round.roundNumber) {
      this.lastVotingRound = round.roundNumber;
      this.votingCarouselIndex.set(0);
    }
  });

  private readonly soundEffectsExperience = effect(() => {
    const round = this.round();
    const remainingSeconds = this.remainingSeconds();
    const isPresentingGifs = this.gifPresentationActive();

    if (!round) {
      this.stopCountdown();
      return;
    }

    if (
      (round.phase === 'Results' || round.phase === 'Completed') &&
      this.lastWinnerRevealRound !== round.roundNumber
    ) {
      this.lastWinnerRevealRound = round.roundNumber;
      this.soundEffects.playWinnerReveal();
    }

    const countdownIsRelevant =
      (round.phase === 'GifSubmission' || round.phase === 'GifVoting') &&
      !isPresentingGifs &&
      remainingSeconds > 0 &&
      remainingSeconds <= 10;
    if (countdownIsRelevant) {
      const key = `${round.roundNumber}:${round.phase}`;
      if (this.countdownKey !== key) {
        this.countdownKey = key;
        this.soundEffects.playCountdown();
      }
    } else {
      this.stopCountdown();
    }
  });

  constructor() {
    this.destroyRef.onDestroy(() => this.soundEffects.stopCountdown());
    interval(1_000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.now.set(Date.now());
        void this.syncExpiredPhase();
      });
  }

  setGameCode(gameCode: string): void {
    this.gameCodeState.set(gameCode);
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

    void this.runCommand(() => this.realtime.submitPhrase(this.gameCodeState(), text));
  }

  votePhrase(phrase: PlayerPhraseSnapshot): void {
    if (phrase.isOwn || this.round()?.hasVotedPhrase || this.remainingSeconds() === 0) {
      return;
    }

    this.selectedPhraseId.set(phrase.id);
    void this.runCommand(() => this.realtime.votePhrase(this.gameCodeState(), phrase.id));
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

    void this.runCommand(() =>
      this.realtime.submitGif(this.gameCodeState(), selection.selectionToken),
    );
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
    void this.runCommand(() => this.realtime.voteGif(this.gameCodeState(), gif.id));
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

    void this.runCommand(() => this.realtime.setResultsReady(this.gameCodeState()));
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

  private fetchGifs(query: string, cursor: string | null, append: boolean): void {
    this.searching.set(true);
    this.message.set('');
    this.lastSearchQuery.set(query);
    this.gifApi
      .search(this.gameCodeState(), query, cursor)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.gifResults.update((items) =>
            append ? this.mergeGifResults(items, response.items) : response.items,
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

  private mergeGifResults(
    currentItems: GifSearchItem[],
    nextItems: GifSearchItem[],
  ): GifSearchItem[] {
    const seen = new Set(currentItems.map((item) => this.gifResultKey(item)));
    return [
      ...currentItems,
      ...nextItems.filter((item) => {
        const key = this.gifResultKey(item);
        if (seen.has(key)) {
          return false;
        }

        seen.add(key);
        return true;
      }),
    ];
  }

  private gifResultKey(item: GifSearchItem): string {
    return item.sourceUrl || item.mediaUrl || item.id;
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
      await this.realtime.requestSync(this.gameCodeState());
    } catch {
      this.message.set('A ação não foi enviada. O estado será sincronizado novamente.');
    } finally {
      this.pending.set(false);
    }
  }

  private async syncExpiredPhase(): Promise<void> {
    const round = this.round();
    const gameCode = this.gameCodeState();
    if (
      !round ||
      round.phase === 'Completed' ||
      !gameCode ||
      this.remainingSeconds() > 0 ||
      this.expirationSyncOperation
    ) {
      return;
    }

    const key = `${round.roundNumber}:${round.phase}`;
    const now = Date.now();
    if (this.expirationSyncKey === key && now - this.expirationSyncStartedAt < 3_000) {
      return;
    }

    this.expirationSyncKey = key;
    this.expirationSyncStartedAt = now;
    const operation = this.realtime.requestSync(gameCode);
    this.expirationSyncOperation = operation;
    try {
      await operation;
    } catch {
      // Keep retrying while the server still reports the expired phase.
    } finally {
      if (this.expirationSyncOperation === operation) {
        this.expirationSyncOperation = null;
      }
    }
  }

  private stopCountdown(): void {
    if (!this.countdownKey) {
      return;
    }

    this.countdownKey = '';
    this.soundEffects.stopCountdown();
  }
}

export interface GifCategory {
  label: string;
  query: string;
}
