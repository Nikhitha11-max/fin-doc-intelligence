import { Component, effect, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { AccessService } from './core/access.service';
import { AuthService } from './core/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  readonly auth = inject(AuthService);
  readonly access = inject(AccessService);

  constructor() {
    effect(() => {
      if (this.auth.isLoggedIn()) {
        this.access.loadMe().subscribe({ error: () => undefined });
      } else {
        this.access.me.set(null);
      }
    });
  }
}
