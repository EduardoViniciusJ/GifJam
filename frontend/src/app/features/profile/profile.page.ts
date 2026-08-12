import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideArrowLeft,
  lucideLogOut,
  lucideShieldCheck,
  lucideTrash2,
  lucideTrophy,
} from '@ng-icons/lucide';
import { catchError, of } from 'rxjs';

import { AuthService } from '@core/auth/auth.service';
import { BrandComponent } from '@shared/ui/brand/brand.component';

@Component({
  selector: 'app-profile-page',
  imports: [BrandComponent, NgIcon, ReactiveFormsModule, RouterLink],
  providers: [
    provideIcons({ lucideArrowLeft, lucideLogOut, lucideShieldCheck, lucideTrash2, lucideTrophy }),
  ],
  styles: [
    `
      .profile-page {
        width: min(100% - 32px, 720px);
        margin: 0 auto;
        padding: 48px 0 64px;
      }
      .profile-card,
      .profile-danger {
        border: 1px solid #e5e7eb;
        border-radius: 24px;
        background: #fff;
        padding: 28px;
        box-shadow: 0 18px 50px rgb(16 24 40 / 8%);
      }
      .profile-card__identity {
        display: flex;
        align-items: center;
        gap: 20px;
      }
      .profile-card__identity img,
      .profile-avatar {
        width: 96px;
        height: 96px;
        border-radius: 50%;
        object-fit: cover;
      }
      .profile-avatar {
        display: grid;
        place-items: center;
        background: #ff7a45;
        color: #fff;
        font-size: 38px;
        font-weight: 800;
      }
      .profile-handle {
        color: #667085;
        margin: 6px 0 0;
      }
      .profile-stats {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 12px;
        margin-top: 28px;
      }
      .profile-stats > div {
        display: grid;
        grid-template-columns: 1fr auto;
        gap: 6px 12px;
        padding: 16px;
        border-radius: 16px;
        background: #fff7f1;
      }
      .profile-stats span {
        color: #667085;
        font-size: 13px;
      }
      .profile-stats strong {
        grid-row: 2;
        font-size: 22px;
      }
      .profile-stats ng-icon {
        grid-column: 2;
        grid-row: 1 / span 2;
        align-self: center;
        color: #ff7a45;
        font-size: 24px;
      }
      .profile-danger {
        display: flex;
        flex-direction: column;
        gap: 18px;
        margin-top: 20px;
        border-color: #fecaca;
      }
      .profile-danger h2 {
        margin: 0 0 6px;
      }
      .profile-danger p {
        margin: 0;
        color: #667085;
      }
      .button--danger {
        border: 0;
        background: #b42318;
        color: #fff;
      }
      .profile-delete-form {
        display: grid;
        gap: 10px;
      }
      .profile-delete-form input {
        min-height: 44px;
        border: 1px solid #d0d5dd;
        border-radius: 10px;
        padding: 0 12px;
      }
      .profile-delete-form__actions,
      .profile-actions {
        display: flex;
        gap: 12px;
        flex-wrap: wrap;
      }
      .profile-actions {
        margin-top: 20px;
        justify-content: flex-end;
      }
      @media (max-width: 520px) {
        .profile-card__identity {
          align-items: flex-start;
          flex-direction: column;
        }
        .profile-stats {
          grid-template-columns: 1fr;
        }
      }
    `,
  ],
  template: `
    <div class="game-shell">
      <header class="game-header">
        <app-brand tone="dark" />
      </header>
      <main class="profile-page" aria-labelledby="profile-title">
        @if (user(); as profile) {
          <section class="profile-card">
            <div class="profile-card__identity">
              @if (profile.avatarUrl) {
                <img
                  [src]="profile.avatarUrl"
                  [alt]="'Avatar de ' + profile.displayName"
                  width="96"
                  height="96"
                />
              } @else {
                <span class="profile-avatar" aria-hidden="true">{{
                  profile.displayName.charAt(0)
                }}</span>
              }
              <div>
                <p class="eyebrow eyebrow--dark">PERFIL DO JOGADOR</p>
                <h1 id="profile-title">{{ profile.displayName }}</h1>
                <p class="profile-handle">
                  <a
                    [href]="'https://discord.com/users/' + profile.discordId"
                    target="_blank"
                    rel="noreferrer"
                    >{{ '@' + profile.username }} · Ver no Discord</a
                  >
                </p>
              </div>
            </div>

            <div class="profile-stats" aria-label="Estatísticas do jogador">
              <div>
                <span>Ranking</span
                ><strong>{{ profile.rank ? '#' + profile.rank : 'Sem posição' }}</strong
                ><ng-icon name="lucideTrophy" aria-hidden="true" />
              </div>
              <div>
                <span>Pontuação</span><strong>{{ profile.totalScore ?? 0 }} pts</strong
                ><ng-icon name="lucideShieldCheck" aria-hidden="true" />
              </div>
            </div>
          </section>

          <section class="profile-danger" aria-labelledby="delete-title">
            <div>
              <h2 id="delete-title">Excluir conta</h2>
              <p>
                Essa ação remove seu perfil, pontuação e histórico associado. Não é possível
                desfazer.
              </p>
            </div>
            @if (!deleting()) {
              <button class="button button--danger" type="button" (click)="showDelete.set(true)">
                <ng-icon name="lucideTrash2" aria-hidden="true" /> Excluir conta
              </button>
            }
            @if (showDelete()) {
              <form class="profile-delete-form" (ngSubmit)="deleteAccount()">
                <label for="delete-confirmation">Digite EXCLUIR para confirmar</label>
                <input id="delete-confirmation" [formControl]="confirmation" autocomplete="off" />
                @if (errorMessage()) {
                  <span class="field-error">{{ errorMessage() }}</span>
                }
                <div class="profile-delete-form__actions">
                  <button
                    class="button button--outline"
                    type="button"
                    (click)="showDelete.set(false)"
                  >
                    Cancelar
                  </button>
                  <button
                    class="button button--danger"
                    type="submit"
                    [disabled]="confirmation.invalid || deleting()"
                  >
                    {{ deleting() ? 'Excluindo…' : 'Confirmar exclusão' }}
                  </button>
                </div>
              </form>
            }
          </section>

          <div class="profile-actions">
            <button class="button button--outline" type="button" (click)="logout()">
              <ng-icon name="lucideLogOut" aria-hidden="true" /> Sair
            </button>
            <a class="button button--outline" routerLink="/"
              ><ng-icon name="lucideArrowLeft" aria-hidden="true" /> Voltar</a
            >
          </div>
        } @else {
          <p class="empty-state">Carregando seu perfil…</p>
        }
      </main>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfilePage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly user = this.auth.user;
  readonly showDelete = signal(false);
  readonly deleting = signal(false);
  readonly errorMessage = signal('');
  readonly confirmation = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/^EXCLUIR$/)],
  });

  ngOnInit(): void {
    this.auth
      .restore()
      .pipe(catchError(() => of(null)))
      .subscribe((user) => {
        if (!user) void this.router.navigateByUrl('/');
      });
  }

  logout(): void {
    this.auth.logout();
    void this.router.navigateByUrl('/');
  }

  deleteAccount(): void {
    this.confirmation.markAsTouched();
    if (this.confirmation.invalid || this.deleting()) return;
    this.deleting.set(true);
    this.errorMessage.set('');
    this.auth.deleteAccount(this.confirmation.value).subscribe({
      next: () => void this.router.navigateByUrl('/'),
      error: () => {
        this.deleting.set(false);
        this.errorMessage.set('Não foi possível excluir a conta. Tente novamente.');
      },
    });
  }
}
