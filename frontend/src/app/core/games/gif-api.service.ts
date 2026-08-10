import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { GifSearchResponse } from './game.models';

@Injectable({ providedIn: 'root' })
export class GifApiService {
  private readonly http = inject(HttpClient);

  search(gameCode: string, query: string, cursor: string | null): Observable<GifSearchResponse> {
    let params = new HttpParams().set('q', query);
    if (cursor) {
      params = params.set('cursor', cursor);
    }

    return this.http.get<GifSearchResponse>(
      `/api/games/${encodeURIComponent(gameCode)}/gifs/search`,
      { params },
    );
  }
}
