import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArrowLeft, lucideTrophy } from '@ng-icons/lucide';

import { BrandComponent } from '@shared/ui/brand/brand.component';

@Component({
  selector: 'app-ranking-page',
  imports: [BrandComponent, NgIcon, RouterLink],
  providers: [provideIcons({ lucideArrowLeft, lucideTrophy })],
  template: `
    <div class="game-shell">
      <header class="game-header">
        <app-brand tone="dark" />
      </header>
      <main class="route-state">
        <span class="route-state__icon route-state__icon--orange">
          <ng-icon name="lucideTrophy" aria-hidden="true" />
        </span>
        <p class="eyebrow eyebrow--dark">RANKING</p>
        <h1>A melhor resposta ganha a rodada</h1>
        <p>O ranking da partida aparece aqui assim que o backend enviar os resultados.</p>
        <a class="button button--outline" routerLink="/">
          <ng-icon name="lucideArrowLeft" aria-hidden="true" />
          Voltar ao início
        </a>
      </main>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RankingPage {}
