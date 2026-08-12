import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, input } from '@angular/core';
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
import { GameFacade } from '@features/game/state/game.facade';
import { PlayerMentionTextComponent } from '@features/game/ui/player-mention-text/player-mention-text.component';

@Component({
  selector: 'app-game-phase',
  imports: [CommonModule, FormsModule, NgIcon, PlayerMentionTextComponent, RouterLink],
  providers: [
    GameFacade,
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
  templateUrl: './game-phase.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GamePhasePage {
  readonly gameCode = input.required<string>();
  private readonly facade = inject(GameFacade);
  private readonly gameCodeEffect = effect(() => this.facade.setGameCode(this.gameCode()));

  readonly round = this.facade.round;
  readonly totalRounds = this.facade.totalRounds;
  readonly playerNames = this.facade.playerNames;
  readonly isAiPhraseMode = this.facade.isAiPhraseMode;
  readonly phraseText = this.facade.phraseText;
  readonly selectedPhraseId = this.facade.selectedPhraseId;
  readonly selectedGifId = this.facade.selectedGifId;
  readonly gifQuery = this.facade.gifQuery;
  readonly gifSuggestions = this.facade.gifSuggestions;
  readonly gifResults = this.facade.gifResults;
  readonly gifResultsCount = this.facade.gifResultsCount;
  readonly nextCursor = this.facade.nextCursor;
  readonly selectedGif = this.facade.selectedGif;
  readonly votingCarouselIndex = this.facade.votingCarouselIndex;
  readonly activeVotingGifIndex = this.facade.activeVotingGifIndex;
  readonly activeVotingGif = this.facade.activeVotingGif;
  readonly gifPresentationActive = this.facade.gifPresentationActive;
  readonly searching = this.facade.searching;
  readonly pending = this.facade.pending;
  readonly message = this.facade.message;
  readonly remainingSeconds = this.facade.remainingSeconds;

  updatePhrase(event: Event): void {
    this.facade.updatePhrase(event);
  }

  canSubmitPhrase(): boolean {
    return this.facade.canSubmitPhrase();
  }

  submitPhrase(): void {
    this.facade.submitPhrase();
  }

  votePhrase(phrase: Parameters<GameFacade['votePhrase']>[0]): void {
    this.facade.votePhrase(phrase);
  }

  searchGifs(): void {
    this.facade.searchGifs();
  }

  searchSuggestion(suggestion: string): void {
    this.facade.searchSuggestion(suggestion);
  }

  loadMoreGifs(): void {
    this.facade.loadMoreGifs();
  }

  selectGif(gif: Parameters<GameFacade['selectGif']>[0]): void {
    this.facade.selectGif(gif);
  }

  submitGif(): void {
    this.facade.submitGif();
  }

  voteGif(gif: Parameters<GameFacade['voteGif']>[0]): void {
    this.facade.voteGif(gif);
  }

  previousVotingGif(): void {
    this.facade.previousVotingGif();
  }

  nextVotingGif(): void {
    this.facade.nextVotingGif();
  }

  confirmResults(): void {
    this.facade.confirmResults();
  }

  winningGifs(
    gifs: Parameters<GameFacade['winningGifs']>[0],
  ): ReturnType<GameFacade['winningGifs']> {
    return this.facade.winningGifs(gifs);
  }

  otherGifs(gifs: Parameters<GameFacade['otherGifs']>[0]): ReturnType<GameFacade['otherGifs']> {
    return this.facade.otherGifs(gifs);
  }

  phaseTitle(phase: string): string {
    return this.facade.phaseTitle(phase);
  }

  formatSeconds(seconds: number): string {
    return this.facade.formatSeconds(seconds);
  }
}
