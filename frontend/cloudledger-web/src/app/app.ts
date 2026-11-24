import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';
import { ToastComponent } from './core/toast.component';

@Component({
  selector: 'app-root',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, ToastComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  logout() {
    this.auth.logout();
  }

  go(path: string): void {
    void this.router.navigate([path]);
  }
}
