import { Component, inject, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SessionService } from './core/services/session.service';

@Component({
  imports: [RouterOutlet],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {
  protected readonly sessionService = inject(SessionService);
  protected readonly title = signal('ImAI');
}
