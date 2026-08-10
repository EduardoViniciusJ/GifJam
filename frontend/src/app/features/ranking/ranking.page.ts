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
      <main class="ranking-guide">
        <div class="ranking-guide__heading">
          <span class="route-state__icon route-state__icon--orange">
            <ng-icon name="lucideTrophy" aria-hidden="true" />
          </span>
          <p class="eyebrow eyebrow--dark">RANKING DA PARTIDA</p>
          <h1>A melhor resposta ganha a rodada</h1>
          <p>O ranking é atualizado ao vivo dentro da sala, sempre depois da votação.</p>
        </div>
        <section class="ranking-guide__steps" aria-label="Como a pontuação funciona">
          <article>
            <strong>01</strong>
            <h2>Vote sem autoria</h2>
            <p>Durante a votação, os GIFs aparecem embaralhados e sem nome.</p>
          </article>
          <article>
            <strong>02</strong>
            <h2>Receba pontos</h2>
            <p>Cada voto recebido pelo seu GIF vale um ponto na partida.</p>
          </article>
          <article>
            <strong>03</strong>
            <h2>Veja a revelação</h2>
            <p>Autores, votos e posições aparecem juntos no resultado da rodada.</p>
          </article>
        </section>
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
