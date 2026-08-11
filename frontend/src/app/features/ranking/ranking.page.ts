import { AsyncPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArrowLeft, lucideTrophy } from '@ng-icons/lucide';
import { catchError, Observable, of } from 'rxjs';

import { BrandComponent } from '@shared/ui/brand/brand.component';
import { GlobalRankingSnapshot } from '@features/game/data/game.models';
import { RankingApiService } from '@features/ranking/data/ranking-api.service';

@Component({
  selector: 'app-ranking-page',
  imports: [AsyncPipe, BrandComponent, NgIcon, RouterLink],
  providers: [provideIcons({ lucideArrowLeft, lucideTrophy })],
  template: `
    <div class="game-shell">
      <header class="game-header">
        <app-brand tone="dark" />
      </header>
      <main class="ranking-guide" aria-labelledby="ranking-title">
        <div class="ranking-guide__heading">
          <span class="route-state__icon route-state__icon--orange">
            <ng-icon name="lucideTrophy" aria-hidden="true" />
          </span>
          <p class="eyebrow eyebrow--dark">RANKING GERAL</p>
          <h1 id="ranking-title">Quem está no topo?</h1>
          <p>A pontuação acumulada nas partidas do GifJam define os maiores pontuadores.</p>
        </div>

        @if (ranking$ | async; as ranking) {
          @if (ranking.entries.length) {
            <section class="global-ranking" aria-label="Maiores pontuadores">
              <div class="global-ranking__header">
                <span>POSIÇÃO</span>
                <span>JOGADOR</span>
                <span>PONTOS</span>
              </div>
              @for (entry of ranking.entries; track entry.userId) {
                <div
                  class="global-ranking__row"
                  [class.global-ranking__row--current]="entry.isCurrentUser"
                >
                  <strong>{{ entry.position }}</strong>
                  <div class="global-ranking__player">
                    @if (entry.avatarUrl) {
                      <img
                        [src]="entry.avatarUrl"
                        [alt]="'Avatar de ' + entry.displayName"
                        width="44"
                        height="44"
                      />
                    } @else {
                      <span class="ranking-avatar" aria-hidden="true">{{
                        entry.displayName.charAt(0)
                      }}</span>
                    }
                    <span>
                      <b>{{ entry.displayName }}</b>
                      @if (entry.isCurrentUser) {
                        <small>Você</small>
                      }
                    </span>
                  </div>
                  <b class="global-ranking__score">{{ entry.score }} pts</b>
                </div>
              }
            </section>
          } @else {
            <p class="empty-state">
              Ainda não há pontuações registradas. Jogue uma partida para aparecer aqui.
            </p>
          }
        } @else {
          <p class="empty-state">Não foi possível carregar o ranking. Tente novamente.</p>
        }

        <a class="button button--outline" routerLink="/">
          <ng-icon name="lucideArrowLeft" aria-hidden="true" />
          Voltar ao início
        </a>
      </main>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RankingPage implements OnInit {
  private readonly rankingApi = inject(RankingApiService);
  private readonly destroyRef = inject(DestroyRef);

  ranking$!: Observable<GlobalRankingSnapshot | null>;

  ngOnInit(): void {
    this.ranking$ = this.rankingApi.getGlobal().pipe(
      catchError(() => of(null)),
      takeUntilDestroyed(this.destroyRef),
    );
  }
}
