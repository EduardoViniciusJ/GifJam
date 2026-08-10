import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-brand',
  imports: [RouterLink],
  template: `
    <a
      routerLink="/"
      class="brand"
      [class.brand--dark]="tone() === 'dark'"
      aria-label="GifJam - início"
    >
      <img src="/brand/gifjam-mascot-header.png" width="44" height="44" alt="" />
      <span class="brand__wordmark"><strong>Gif</strong><b>Jam</b></span>
    </a>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BrandComponent {
  readonly tone = input<'light' | 'dark'>('light');
}
