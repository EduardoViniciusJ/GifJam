import { ChangeDetectionStrategy, Component } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideLoaderCircle } from '@ng-icons/lucide';

import { BrandComponent } from '@shared/ui/brand/brand.component';

@Component({
  selector: 'app-auth-callback-page',
  imports: [BrandComponent, NgIcon],
  providers: [provideIcons({ lucideLoaderCircle })],
  template: `
    <main class="status-page" aria-live="polite">
      <app-brand />
      <ng-icon class="status-page__spinner" name="lucideLoaderCircle" aria-hidden="true" />
      <h1>Conectando sua conta</h1>
      <p>Aguarde enquanto concluímos a entrada com o Discord.</p>
    </main>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthCallbackPage {}
