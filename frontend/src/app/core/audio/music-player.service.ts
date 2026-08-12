import { Injectable, signal } from '@angular/core';

const DEFAULT_VOLUME = 0.5;
const STORAGE_KEY = 'gifjam.music.settings';

const MUSIC_TRACKS = [
  { id: 'gifjam', label: 'GifJam 01', src: '/audio/gifjam.mp3' },
  { id: 'gifjam-alt', label: 'GifJam 02', src: '/audio/gifjam-alt.mp3' },
] as const;

type MusicTrackId = (typeof MUSIC_TRACKS)[number]['id'];

interface MusicSettings {
  volume: number;
  muted: boolean;
  trackId: MusicTrackId;
}

@Injectable({ providedIn: 'root' })
export class MusicPlayerService {
  private readonly audio: HTMLAudioElement | null;

  readonly tracks = MUSIC_TRACKS;
  readonly volume = signal(DEFAULT_VOLUME);
  readonly isMuted = signal(false);
  readonly isPlaying = signal(false);
  readonly currentTrackId = signal<MusicTrackId>('gifjam');

  constructor() {
    const settings = this.readSettings();
    this.volume.set(settings.volume);
    this.isMuted.set(settings.muted);
    this.currentTrackId.set(settings.trackId);

    if (typeof Audio === 'undefined') {
      this.audio = null;
      return;
    }

    const audio = new Audio(this.trackById(settings.trackId).src);
    audio.loop = true;
    audio.preload = 'metadata';
    audio.volume = settings.volume;
    audio.muted = settings.muted;
    audio.addEventListener('play', () => this.isPlaying.set(true));
    audio.addEventListener('pause', () => this.isPlaying.set(false));
    audio.addEventListener('ended', () => this.isPlaying.set(false));
    this.audio = audio;
    this.startOnFirstInteraction();
  }

  togglePlayback(): void {
    if (!this.audio) {
      return;
    }

    if (this.audio.paused) {
      void this.audio.play().catch(() => this.isPlaying.set(false));
      return;
    }

    this.audio.pause();
  }

  selectTrack(trackId: MusicTrackId): void {
    if (trackId === this.currentTrackId()) {
      return;
    }

    const shouldResume = this.isPlaying() && !this.isMuted();
    this.currentTrackId.set(trackId);
    this.persistSettings();

    if (!this.audio) {
      return;
    }

    this.audio.pause();
    this.audio.src = this.trackById(trackId).src;
    this.audio.load();

    if (shouldResume) {
      void this.audio.play().catch(() => this.isPlaying.set(false));
    }
  }

  setVolume(value: number): void {
    const nextVolume = clamp(value, 0, 1);
    this.volume.set(nextVolume);

    if (this.audio) {
      this.audio.volume = nextVolume;
    }

    this.persistSettings();
  }

  toggleMute(): void {
    const nextMuted = !this.isMuted();
    this.isMuted.set(nextMuted);

    if (this.audio) {
      this.audio.muted = nextMuted;
    }

    this.persistSettings();
  }

  private startOnFirstInteraction(): void {
    if (!this.audio || this.isMuted() || typeof document === 'undefined') {
      return;
    }

    const startPlayback = (): void => {
      document.removeEventListener('pointerdown', startPlayback);
      document.removeEventListener('keydown', startPlayback);

      if (!this.isMuted()) {
        void this.audio?.play().catch(() => undefined);
      }
    };

    document.addEventListener('pointerdown', startPlayback, { once: true });
    document.addEventListener('keydown', startPlayback, { once: true });
  }

  private readSettings(): MusicSettings {
    if (typeof window === 'undefined') {
      return { volume: DEFAULT_VOLUME, muted: false, trackId: 'gifjam' };
    }

    try {
      const stored = window.localStorage.getItem(STORAGE_KEY);
      if (!stored) {
        return { volume: DEFAULT_VOLUME, muted: false, trackId: 'gifjam' };
      }

      const parsed: unknown = JSON.parse(stored);
      if (!parsed || typeof parsed !== 'object') {
        return { volume: DEFAULT_VOLUME, muted: false, trackId: 'gifjam' };
      }

      const record = parsed as Record<string, unknown>;
      const storedVolume = record['volume'];
      const volume =
        typeof storedVolume === 'number' && Number.isFinite(storedVolume)
          ? clamp(storedVolume, 0, 1)
          : DEFAULT_VOLUME;
      const storedTrackId = record['trackId'];
      const trackId = this.isTrackId(storedTrackId) ? storedTrackId : 'gifjam';

      return { volume, muted: record['muted'] === true, trackId };
    } catch {
      return { volume: DEFAULT_VOLUME, muted: false, trackId: 'gifjam' };
    }
  }

  private persistSettings(): void {
    if (typeof window === 'undefined') {
      return;
    }

    try {
      window.localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({
          volume: this.volume(),
          muted: this.isMuted(),
          trackId: this.currentTrackId(),
        }),
      );
    } catch {
      // Local storage can be unavailable in private browsing contexts.
    }
  }

  private trackById(trackId: MusicTrackId) {
    return this.tracks.find((track) => track.id === trackId) ?? this.tracks[0];
  }

  private isTrackId(value: unknown): value is MusicTrackId {
    return this.tracks.some((track) => track.id === value);
  }
}

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.min(Math.max(value, minimum), maximum);
}
