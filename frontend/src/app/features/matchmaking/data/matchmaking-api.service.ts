import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { apiUrl } from '@core/http/api-url';

import { MatchmakingSnapshot } from './matchmaking.models';

@Injectable({ providedIn: 'root' })
export class MatchmakingApiService {
  private readonly http = inject(HttpClient);

  join(): Observable<MatchmakingSnapshot> {
    return this.http.post<MatchmakingSnapshot>(apiUrl('/matchmaking/join'), {});
  }

  leave(): Observable<void> {
    return this.http.post<void>(apiUrl('/matchmaking/leave'), {});
  }

  status(): Observable<MatchmakingSnapshot> {
    return this.http.get<MatchmakingSnapshot>(apiUrl('/matchmaking/status'));
  }
}
