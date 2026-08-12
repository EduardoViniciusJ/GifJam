import { Routes } from '@angular/router';

import { authGuard } from '@core/auth/auth.guard';

export const routes: Routes = [
  {
    path: '',
    title: 'GifJam | A frase certa. O GIF perfeito.',
    loadComponent: () => import('@features/home/home.page').then((page) => page.HomePage),
  },
  {
    path: 'auth/callback',
    title: 'Entrando | GifJam',
    loadComponent: () =>
      import('@features/auth-callback/auth-callback.page').then((page) => page.AuthCallbackPage),
  },
  {
    path: 'sala/:code',
    title: 'Sala | GifJam',
    loadComponent: () => import('@features/room/room.page').then((page) => page.RoomPage),
  },
  {
    path: 'ranking',
    title: 'Ranking | GifJam',
    canActivate: [authGuard],
    loadComponent: () => import('@features/ranking/ranking.page').then((page) => page.RankingPage),
  },
  {
    path: 'perfil',
    title: 'Meu perfil | GifJam',
    canActivate: [authGuard],
    loadComponent: () => import('@features/profile/profile.page').then((page) => page.ProfilePage),
  },
  { path: '**', redirectTo: '' },
];
