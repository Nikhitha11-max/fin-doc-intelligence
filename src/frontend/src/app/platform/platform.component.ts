import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { AccessService, describeError, TenantSummary } from '../core/access.service';

const PAGE_SIZE = 10;

@Component({
  selector: 'app-platform',
  imports: [DatePipe],
  template: `
    <header class="page-head">
      <h1>Organizations</h1>
      <p>Create organizations and manage their lifecycle.</p>
    </header>
    <section class="card mb-6" aria-label="Create an organization">
      <h2 class="mb-3">Create an organization</h2>
      <div class="grid gap-3 md:grid-cols-[1fr_1.2fr_1fr_auto]">
        <input #name type="text" placeholder="Organization name" maxlength="200" aria-label="Organization name" />
        <input #adminEmail type="email" placeholder="First administrator email" aria-label="Administrator email" />
        <input #adminPassword type="password" placeholder="Temporary password" aria-label="Administrator password" autocomplete="new-password" />
        <button type="button" (click)="create(name, adminEmail, adminPassword)">Create organization</button>
      </div>
    </section>

    @if (message()) { <p class="message" role="status">{{ message() }}</p> }
    @if (error()) { <p class="error" role="alert">{{ error() }}</p> }

    @if (loading()) {
      <p class="loading">Loading…</p>
    } @else {
      <div class="table-wrap">
        <table>
          <thead><tr><th>Name</th><th>Status</th><th>Created</th><th></th></tr></thead>
          <tbody>
            @for (t of items(); track t.id) {
              <tr>
                <td class="font-medium text-slate-900">
                  @if (editingId() === t.id) {
                    <div class="flex items-center gap-1.5">
                      <input #rename [value]="t.name" maxlength="200" aria-label="New name" (keydown.enter)="saveName(t, rename.value)" />
                      <button type="button" class="btn-sm" (click)="saveName(t, rename.value)">Save</button>
                      <button type="button" class="btn-secondary btn-sm" (click)="editingId.set(null)">Cancel</button>
                    </div>
                  } @else {
                    {{ t.name }}
                  }
                </td>
                <td><span class="badge" [class.completed]="t.isActive" [class.failed]="!t.isActive">{{ t.isActive ? 'Active' : 'Deactivated' }}</span></td>
                <td class="text-slate-500">{{ t.createdAt | date: 'mediumDate' }}</td>
                <td>
                  <div class="row-actions">
                    <button type="button" class="btn-secondary btn-sm" (click)="editingId.set(t.id)">Rename</button>
                    <button type="button" [class]="t.isActive ? 'btn-danger btn-sm' : 'btn-secondary btn-sm'" (click)="toggle(t)">{{ t.isActive ? 'Deactivate' : 'Reactivate' }}</button>
                  </div>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      <nav class="pager" aria-label="Pagination">
        <button type="button" class="btn-secondary btn-sm" [disabled]="page() <= 1" (click)="goTo(page() - 1)">Previous</button>
        <span>Page {{ page() }} of {{ totalPages() }} ({{ total() }} organizations)</span>
        <button type="button" class="btn-secondary btn-sm" [disabled]="page() >= totalPages()" (click)="goTo(page() + 1)">Next</button>
      </nav>
    }
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
