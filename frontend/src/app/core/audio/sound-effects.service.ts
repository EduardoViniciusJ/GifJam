import { effect, inject, Injectable } from '@angular/core';

import { MusicPlayerService } from './music-player.service';

const COUNTDOWN_SOURCE = '/audio/sfx/countdown.wav';
const WINNER_REVEAL_SOURCE = '/audio/sfx/winner-reveal.wav';
const MAXIMUM_WINNER_REVEAL_DURATION_MS = 9_000;

@Injectable({ providedIn: 'root' })
export class SoundEffectsService {
  private readonly audios = new Map<string, HTMLAudioElement>();
  private readonly stopTimers = new Map<string, ReturnType<typeof setTimeout>>();
  private readonly music = inject(MusicPlayerService);

  private readonly syncAudioSettingsEffect = effect(() => {
    const volume = this.music.volume();
    const muted = this.music.isMuted();
    this.audios.forEach((audio) => {
      audio.volume = volume;
      audio.muted = muted;
    });
  });

  constructor() {
    this.preload();
  }

  playCountdown(): void {
    const audio = this.audioFor(COUNTDOWN_SOURCE);
    if (!audio) {
      return;
    }

    audio.currentTime = 0;
    audio.volume = this.music.volume();
    audio.muted = this.music.isMuted();
    void audio.play().catch(() => undefined);
  }

  stopCountdown(): void {
    this.stopSource(COUNTDOWN_SOURCE);
  }

  playWinnerReveal(): void {
    this.stopCountdown();
    const audio = this.audioFor(WINNER_REVEAL_SOURCE);
    if (!audio) {
      return;
    }

    audio.currentTime = 0;
    audio.volume = this.music.volume();
    audio.muted = this.music.isMuted();
    void audio.play().catch(() => undefined);
    this.stopTimers.set(
      WINNER_REVEAL_SOURCE,
      setTimeout(() => this.stopSource(WINNER_REVEAL_SOURCE), MAXIMUM_WINNER_REVEAL_DURATION_MS),
    );
  }

  private preload(): void {
    [COUNTDOWN_SOURCE, WINNER_REVEAL_SOURCE].forEach((source) => this.audioFor(source));
  }

  private audioFor(source: string): HTMLAudioElement | null {
    if (typeof Audio === 'undefined') {
      return null;
    }

    const existing = this.audios.get(source);
    if (existing) {
      return existing;
    }

    const audio = new Audio(source);
    audio.preload = 'auto';
    audio.volume = this.music.volume();
    audio.muted = this.music.isMuted();
    this.audios.set(source, audio);
    return audio;
  }

  private stopSource(source: string): void {
    const timer = this.stopTimers.get(source);
    if (timer) {
      clearTimeout(timer);
      this.stopTimers.delete(source);
    }

    const audio = this.audios.get(source);
    if (!audio) {
      return;
    }

    audio.pause();
    audio.currentTime = 0;
  }
}
