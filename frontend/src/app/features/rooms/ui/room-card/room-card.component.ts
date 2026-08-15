import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideSparkles, lucideUsers } from '@ng-icons/lucide';

import { PublicRoomSummary } from '../../data/room-directory.models';

@Component({
  selector: 'app-room-card',
  imports: [NgIcon],
  providers: [provideIcons({ lucideSparkles, lucideUsers })],
  template: `
    <button
      class="room-card"
      type="button"
      [attr.aria-label]="'Entrar na sala de ' + room().hostDisplayName"
      (click)="selected.emit(room())"
    >
      <span
        class="room-card__preview"
        [class.room-card__preview--ai]="room().mode === 'AiRandomPhrases'"
      >
        <span class="room-card__status">ABERTA</span>
        <img src="/brand/gifjam-mascot.webp" alt="" loading="lazy" />
        <span class="room-card__occupancy">
          <ng-icon name="lucideUsers" aria-hidden="true" />
          {{ room().playerCount }}/{{ room().capacity }} jogadores
        </span>
      </span>

      <span class="room-card__details">
        <span class="room-card__avatar" aria-hidden="true">
          @if (room().hostAvatarUrl; as avatarUrl) {
            <img [src]="avatarUrl" alt="" loading="lazy" />
          } @else {
            {{ room().hostDisplayName.charAt(0).toUpperCase() }}
          }
        </span>
        <span class="room-card__copy">
          <strong>Sala de {{ room().hostDisplayName }}</strong>
          <small>Host: {{ room().hostDisplayName }}</small>
          <span class="room-card__tags">
            <span>
              @if (room().mode === 'AiRandomPhrases') {
                <ng-icon name="lucideSparkles" aria-hidden="true" />
                Frases IA
              } @else {
                Clássico
              }
            </span>
            <span>{{ room().totalRounds }} rodadas</span>
          </span>
        </span>
      </span>
    </button>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoomCardComponent {
  readonly room = input.required<PublicRoomSummary>();
  readonly selected = output<PublicRoomSummary>();
}
