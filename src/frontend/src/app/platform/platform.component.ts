import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { AccessService, describeError, TenantSummary } from '../core/access.service';

const PAGE_SIZE = 10;

@Component({
  selector: 'app-platform',
  imports: [DatePipe],
  template: `
    <h1>Organizations</h1>
    <section class="add">
      <input #name type="text" placeholder="Organization name" maxlength="200" aria-label="Organization name" />
      <input #adminEmail type="email" placeholder="First administrator email" aria-label="Administrator email" />
      <input #adminPassword type="password" placeholder="Temporary password" aria-label="Administrator password" autocomplete="new-password" />
      <button type="button" (click)="create(name, adminEmail, adminPassword)">Create organization</button>
    </section>

    @if (message()) { <p class="message" role="status">{{ message() }}</p> }
    @if (error()) { <p class="error" role="alert">{{ error() }}</p> }

    @if (loading()) {
      <p>Loading…</p>
    } @else {
      <table>
        <thead><tr><th>Name</th><th>Status</th><th>Created</th><th></th></tr></thead>
        <tbody>
          @for (t of items(); track t.id) {
            <tr>
              <td>
                @if (editingId() === t.id) {
                  <input #rename [value]="t.name" maxlength="200" aria-label="New name" (keydown.enter)="saveName(t, rename.value)" />
                  <button type="button" (click)="saveName(t, rename.value)">Save</button>
                  <button type="button" (click)="editingId.set(null)">Cancel</button>
                } @else {
                  {{ t.name }}
                }
              </td>
              <td>{{ t.isActive ? 'Active' : 'Deactivated' }}</td>
              <td>{{ t.createdAt | date: 'mediumDate' }}</td>
              <td class="actions">
                <button type="button" (click)="editingId.set(t.id)">Rename</button>
                <button type="button" (click)="toggle(t)">{{ t.isActive ? 'Deactivate' : 'Reactivate' }}</button>
              </td>
            </tr>
          }
        </tbody>
      </table>
      <nav class="pager" aria-label="Pagination">
        <button type="button" [disabled]="page() <= 1" (click)="goTo(page() - 1)">Previous</button>
        <span>Page {{ page() }} of {{ totalPages() }} ({{ total() }} organizations)</span>
        <button type="button" [disabled]="page() >= totalPages()" (click)="goTo(page() + 1)">Next</button>
      </nav>
    }
  `,
  styles: `
    :host { display: block; max-width: 60rem; margin: 1rem auto; padding: 0 1rem; font-family: system-ui, sans-serif; }
    .add { display: flex; gap: 0.5rem; flex-wrap: wrap; margin: 1rem 0; }
    table { width: 100%; border-collapse: collapse; }
    th, td { text-align: left; padding: 0.5rem; border-bottom: 1px solid #ddd; }
    .actions { white-space: nowrap; }
    .pager { display: flex; gap: 1rem; align-items: center; margin-top: 1rem; }
    .message { color: #1b5e20; }
    .error { color: #b00020; }
  `,
})
export class PlatformComponent implements OnInit {
  private readonly api = inject(AccessService);

  readonly items = signal<TenantSummary[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly loading = signal(false);
  readonly message = signal('');
  readonly error = signal('');
  readonly editingId = signal<string | null>(null);
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.total() / PAGE_SIZE)));

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.listTenants(this.page(), PAGE_SIZE).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.total.set(result.total);
        this.loading.set(false);
      },
      error: (e: HttpErrorResponse) => this.fail(e, 'Could not load organizations.'),
    });
  }

  goTo(page: number): void {
    this.page.set(page);
    this.load();
  }

  create(name: HTMLInputElement, email: HTMLInputElement, password: HTMLInputElement): void {
    this.clear();
    this.api.createTenant(name.value.trim(), email.value.trim(), password.value).subscribe({
      next: () => {
        name.value = '';
        email.value = '';
        password.value = '';
        this.message.set('Organization created.');
        this.load();
      },
      error: (e: HttpErrorResponse) => this.fail(e, 'Could not create the organization.'),
    });
  }

  saveName(tenant: TenantSummary, name: string): void {
    this.clear();
    this.api.renameTenant(tenant.id, name).subscribe({
      next: () => {
        this.editingId.set(null);
        this.message.set('Organization renamed.');
        this.load();
      },
      error: (e: HttpErrorResponse) => this.fail(e, 'Could not rename the organization.'),
    });
  }

  toggle(tenant: TenantSummary): void {
    if (tenant.isActive && !confirm(`Deactivate ${tenant.name}? Its members lose access immediately.`)) {
      return;
    }
    this.clear();
    this.api.setTenantActive(tenant.id, !tenant.isActive).subscribe({
      next: () => {
        this.message.set(tenant.isActive ? 'Organization deactivated.' : 'Organization reactivated.');
        this.load();
      },
      error: (e: HttpErrorResponse) => this.fail(e, 'Could not change the organization status.'),
    });
  }

  private clear(): void {
    this.message.set('');
    this.error.set('');
  }

  private fail(e: HttpErrorResponse, fallback: string): void {
    this.loading.set(false);
    this.error.set(describeError(e, fallback));
  }
}
