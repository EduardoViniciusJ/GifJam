import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideMusic2,
  lucidePause,
  lucidePlay,
  lucideVolume1,
  lucideVolume2,
  lucideVolumeX,
} from '@ng-icons/lucide';

import { MusicPlayerService } from '@core/audio/music-player.service';

@Component({
  selector: 'app-music-control',
  imports: [NgIcon],
  providers: [
    provideIcons({
      lucideMusic2,
      lucidePause,
      lucidePlay,
      lucideVolume1,
      lucideVolume2,
      lucideVolumeX,
    }),
  ],
  template: `
    <section
      class="music-control"
      [class.music-control--open]="isOpen()"
      aria-label="Controle de música"
    >
      <button
        class="music-control__toggle"
        type="button"
        [attr.aria-label]="isOpen() ? 'Fechar controle de música' : 'Abrir controle de música'"
        [attr.aria-expanded]="isOpen()"
        [title]="isOpen() ? 'Fechar controle de música' : 'Abrir controle de música'"
        (click)="toggleOpen()"
      >
        <ng-icon name="lucideMusic2" aria-hidden="true" />
      </button>

      @if (isOpen()) {
        <div class="music-control__panel">
          <div class="music-control__tracks" aria-label="Escolher música" role="group">
            @for (track of music.tracks; track track.id) {
              <button
                class="music-control__track"
                type="button"
                [class.music-control__track--active]="music.currentTrackId() === track.id"
                [attr.aria-pressed]="music.currentTrackId() === track.id"
                [title]="'Selecionar ' + track.label"
                (click)="music.selectTrack(track.id)"
              >
                {{ track.label }}
              </button>
            }
          </div>

          <button
            class="music-control__button music-control__button--icon"
            type="button"
            [attr.aria-label]="music.isPlaying() ? 'Pausar música' : 'Tocar música'"
            [attr.aria-pressed]="music.isPlaying()"
            [title]="music.isPlaying() ? 'Pausar música' : 'Tocar música'"
            (click)="music.togglePlayback()"
          >
            <ng-icon [name]="music.isPlaying() ? 'lucidePause' : 'lucidePlay'" aria-hidden="true" />
          </button>

          <button
            class="music-control__button music-control__button--icon"
            type="button"
            [attr.aria-label]="music.isMuted() ? 'Ativar música' : 'Silenciar música'"
            [attr.aria-pressed]="music.isMuted()"
            [title]="music.isMuted() ? 'Ativar música' : 'Silenciar música'"
            (click)="music.toggleMute()"
          >
            <ng-icon [name]="volumeIcon" aria-hidden="true" />
          </button>

          <label class="music-control__slider-label" for="music-volume">Volume</label>
          <input
            id="music-volume"
            class="music-control__slider"
            type="range"
            min="0"
            max="1"
            step="0.01"
            [value]="music.volume()"
            [attr.aria-valuetext]="volumeLabel"
            (input)="updateVolume($event)"
          />
        </div>
      }
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MusicControlComponent {
  readonly music = inject(MusicPlayerService);
  readonly isOpen = signal(false);

  get volumeIcon(): 'lucideVolume1' | 'lucideVolume2' | 'lucideVolumeX' {
    if (this.music.isMuted() || this.music.volume() === 0) {
      return 'lucideVolumeX';
    }

    return this.music.volume() < 0.5 ? 'lucideVolume1' : 'lucideVolume2';
  }

  get volumeLabel(): string {
    return `${Math.round(this.music.volume() * 100)}%`;
  }

  toggleOpen(): void {
    this.isOpen.update((isOpen) => !isOpen);
  }

  updateVolume(event: Event): void {
    const target = event.target;
    if (!(target instanceof HTMLInputElement)) {
      return;
    }

    this.music.setVolume(Number(target.value));
  }
}
