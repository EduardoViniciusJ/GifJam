import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArrowLeft, lucideClock, lucideUsers } from '@ng-icons/lucide';

import { BrandComponent } from '@shared/ui/brand/brand.component';

@Component({
  selector: 'app-room-page',
  imports: [BrandComponent, NgIcon, RouterLink],
  providers: [provideIcons({ lucideArrowLeft, lucideClock, lucideUsers })],
  template: `
    <div class="game-shell">
      <header class="game-header">
        <app-brand tone="dark" />
      </header>
      <main class="route-state">
        <span class="route-state__icon"><ng-icon name="lucideUsers" aria-hidden="true" /></span>
        <p class="eyebrow eyebrow--dark">SALA</p>
        <h1>{{ roomCode }}</h1>
        <p class="route-state__message">
          <ng-icon name="lucideClock" aria-hidden="true" />
          Preparando a conexão em tempo real.
        </p>
        <a class="button button--outline" routerLink="/">
          <ng-icon name="lucideArrowLeft" aria-hidden="true" />
          Voltar ao início
        </a>
      </main>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoomPage {
  private readonly route = inject(ActivatedRoute);

  readonly roomCode = this.route.snapshot.paramMap.get('code')?.toUpperCase() ?? '';
}
