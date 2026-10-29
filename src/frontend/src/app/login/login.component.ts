import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AccessService } from '../core/access.service';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule],
  template: `
    <main class="card">
      <h1>Sign in to FinDoc AI</h1>
      <form [formGroup]="form" (ngSubmit)="submit()">
        <label>Email <input type="email" formControlName="email" autocomplete="username" /></label>
        <label>Password <input type="password" formControlName="password" autocomplete="current-password" /></label>
        @if (error()) {
          <p class="error" role="alert">{{ error() }}</p>
        }
        <button type="submit" [disabled]="form.invalid || busy()">Sign in</button>
      </form>
    </main>
  `,
  styles: `
    .card { max-width: 22rem; margin: 4rem auto; padding: 1.5rem; border: 1px solid #ccc; border-radius: 8px; }
    label { display: block; margin-bottom: 1rem; }
    input { display: block; width: 100%; padding: 0.4rem; box-sizing: border-box; }
    .error { color: #b00020; }
  `,
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly access = inject(AccessService);
  private readonly router = inject(Router);

  readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });
  readonly busy = signal(false);
  readonly error = signal('');

  submit(): void {
    if (this.form.invalid) {
      return;
    }
    this.busy.set(true);
    this.error.set('');
    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: () =>
        this.access.loadMe().subscribe({
          next: (me) => void this.router.navigate([me.isPlatformAdmin ? '/platform' : '/documents']),
          error: () => void this.router.navigate(['/documents']),
        }),
      error: (e: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(e.status === 401 ? 'Invalid email or password.' : 'Sign-in failed. Please try again.');
      },
    });
  }
}
