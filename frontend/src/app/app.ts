import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { MusicControlComponent } from '@shared/ui/music-control/music-control.component';

@Component({
  selector: 'app-root',
  imports: [MusicControlComponent, RouterOutlet],
  templateUrl: './app.html',
})
export class App {}
