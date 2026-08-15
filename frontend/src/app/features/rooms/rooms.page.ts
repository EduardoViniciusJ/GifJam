import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideArrowLeft,
  lucideArrowRight,
  lucideClock3,
  lucideFlame,
  lucidePlus,
} from '@ng-icons/lucide';

import { AuthService } from '@core/auth/auth.service';
import { BrandComponent } from '@shared/ui/brand/brand.component';

import { PublicRoomSummary } from './data/room-directory.models';
import { RoomDirectoryRealtimeService } from './data/room-directory-realtime.service';
import { RoomDirectoryFacade } from './state/room-directory.facade';
import { RoomCardComponent } from './ui/room-card/room-card.component';

@Component({
  selector: 'app-rooms-page',
  imports: [BrandComponent, NgIcon, RoomCardComponent, RouterLink],
  providers: [
    RoomDirectoryRealtimeService,
    RoomDirectoryFacade,
    provideIcons({ lucideArrowLeft, lucideArrowRight, lucideClock3, lucideFlame, lucidePlus }),
  ],
  templateUrl: './rooms.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoomsPage implements OnInit, OnDestroy {
  private readonly router = inject(Router);
  readonly auth = inject(AuthService);
  readonly directory = inject(RoomDirectoryFacade);

  ngOnInit(): void {
    void this.directory.initialize({ pageSize: 20, sort: 'popular' });
  }

  ngOnDestroy(): void {
    void this.directory.destroy();
  }

  createRoom(): void {
    void this.router.navigate(['/sala', 'nova']);
  }

  openRoom(room: PublicRoomSummary): void {
    void this.directory.openRoom(room);
  }
}
