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
    <main class="mx-auto mt-12 max-w-md">
      <div class="mb-8 flex flex-col items-center text-center">
        <span class="mb-4 flex h-14 w-14 items-center justify-center rounded-2xl bg-indigo-600 text-2xl font-bold text-white shadow-lg shadow-indigo-200">F</span>
        <h1>Sign in to FinDoc AI</h1>
        <p class="mt-1 text-slate-500">Secure financial-document intelligence.</p>
      </div>
      <form class="card space-y-5 !p-8 shadow-xl shadow-slate-200" [formGroup]="form" (ngSubmit)="submit()">
        <label>Email <input type="email" class="mt-1.5" formControlName="email" autocomplete="username" placeholder="you@company.com" /></label>
        <label>Password <input type="password" class="mt-1.5" formControlName="password" autocomplete="current-password" placeholder="••••••••" /></label>
        @if (error()) {
          <p class="error !mb-0" role="alert">{{ error() }}</p>
        }
        <button type="submit" class="w-full !py-2.5" [disabled]="form.invalid || busy()">{{ busy() ? 'Signing in…' : 'Sign in' }}</button>
      </form>
    </main>
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
