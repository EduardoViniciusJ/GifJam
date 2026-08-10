import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { GameSettings, PlayerGameSnapshot } from './game.models';

@Injectable({ providedIn: 'root' })
export class GameApiService {
  private readonly http = inject(HttpClient);

  create(settings: GameSettings): Observable<PlayerGameSnapshot> {
    return this.http.post<PlayerGameSnapshot>('/api/games', settings);
  }

  join(code: string): Observable<PlayerGameSnapshot> {
    return this.http.post<PlayerGameSnapshot>(`/api/games/${encodeURIComponent(code)}/join`, {});
  }

  leave(code: string): Observable<void> {
    return this.http.post<void>(`/api/games/${encodeURIComponent(code)}/leave`, {});
  }
}
