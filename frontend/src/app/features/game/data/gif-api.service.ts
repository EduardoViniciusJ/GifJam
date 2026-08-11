import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { apiUrl } from '@core/http/api-url';

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
      apiUrl(`/games/${encodeURIComponent(gameCode)}/gifs/search`),
      { params },
    );
  }
}
