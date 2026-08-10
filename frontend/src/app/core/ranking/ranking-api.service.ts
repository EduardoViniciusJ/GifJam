import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { GlobalRankingSnapshot } from '@core/games/game.models';
import { apiUrl } from '@core/http/api-url';

@Injectable({ providedIn: 'root' })
export class RankingApiService {
  private readonly http = inject(HttpClient);

  getGlobal(): Observable<GlobalRankingSnapshot> {
    return this.http.get<GlobalRankingSnapshot>(apiUrl('/ranking'));
  }
}
