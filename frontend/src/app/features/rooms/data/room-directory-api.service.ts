import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { apiUrl } from '@core/http/api-url';

import { PublicRoomDirectoryResponse, RoomDirectorySort } from './room-directory.models';

@Injectable({ providedIn: 'root' })
export class RoomDirectoryApiService {
  private readonly http = inject(HttpClient);

  getPublic(
    sort: RoomDirectorySort,
    page: number,
    pageSize: number,
  ): Observable<PublicRoomDirectoryResponse> {
    const params = new HttpParams().set('sort', sort).set('page', page).set('pageSize', pageSize);

    return this.http.get<PublicRoomDirectoryResponse>(apiUrl('/rooms/public'), { params });
  }
}
