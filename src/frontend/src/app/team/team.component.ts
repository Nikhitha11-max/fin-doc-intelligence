import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { AccessService, describeError, Member, ROLES, TenantRole } from '../core/access.service';

const PAGE_SIZE = 10;

@Component({
  selector: 'app-team',
  template: `
    <header class="page-head">
      <h1>Team</h1>
      <p>Add members to your organization and manage their roles.</p>
    </header>
    <section class="card mb-6" aria-label="Add a member">
      <h2 class="mb-3">Add a member</h2>
      <div class="grid gap-3 md:grid-cols-[1.3fr_1.3fr_1fr_auto]">
        <input #email type="email" placeholder="Email" aria-label="Email" />
        <input #password type="password" placeholder="Temporary password" aria-label="Password" autocomplete="new-password" />
        <select #role aria-label="Role">
          @for (r of roles; track r) {
            <option [value]="r" [selected]="r === 'Analyst'">{{ r }}</option>
          }
        </select>
        <button type="button" (click)="add(email, password, role)">Add member</button>
      </div>
    </section>

    @if (message()) { <p class="message" role="status">{{ message() }}</p> }
    @if (error()) { <p class="error" role="alert">{{ error() }}</p> }

    @if (loading()) {
      <p class="loading">Loading…</p>
    } @else {
      <div class="table-wrap">
        <table>
          <thead><tr><th>Email</th><th>Role</th><th></th></tr></thead>
          <tbody>
            @for (m of items(); track m.userId) {
              <tr>
                <td class="font-medium text-slate-900">{{ m.email }}</td>
                <td class="max-w-56">
                  <select [value]="m.role" [attr.aria-label]="'Role for ' + m.email" (change)="changeRole(m, $any($event.target).value)">
                    @for (r of roles; track r) {
                      <option [value]="r" [selected]="r === m.role">{{ r }}</option>
                    }
                  </select>
                </td>
                <td><div class="row-actions"><button type="button" class="btn-danger btn-sm" (click)="remove(m)">Remove</button></div></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      <nav class="pager" aria-label="Pagination">
        <button type="button" class="btn-secondary btn-sm" [disabled]="page() <= 1" (click)="goTo(page() - 1)">Previous</button>
        <span>Page {{ page() }} of {{ totalPages() }} ({{ total() }} members)</span>
        <button type="button" class="btn-secondary btn-sm" [disabled]="page() >= totalPages()" (click)="goTo(page() + 1)">Next</button>
      </nav>
    }
  `,
})
export class TeamComponent implements OnInit {
  private readonly api = inject(AccessService);

  readonly roles = ROLES;
  readonly items = signal<Member[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly loading = signal(false);
  readonly message = signal('');
  readonly error = signal('');
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.total() / PAGE_SIZE)));

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.listMembers(this.page(), PAGE_SIZE).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.total.set(result.total);
        this.loading.set(false);
      },
      error: (e: HttpErrorResponse) => this.fail(e, 'Could not load members.'),
    });
  }

  goTo(page: number): void {
    this.page.set(page);
    this.load();
  }

  add(email: HTMLInputElement, password: HTMLInputElement, role: HTMLSelectElement): void {
    this.clear();
    this.api.addMember(email.value.trim(), password.value, role.value as TenantRole).subscribe({
      next: () => {
        email.value = '';
        password.value = '';
        this.message.set('Member added.');
        this.load();
      },
      error: (e: HttpErrorResponse) => this.fail(e, 'Could not add the member.'),
    });
  }

  changeRole(member: Member, role: TenantRole): void {
    this.clear();
    this.api.changeRole(member.userId, role).subscribe({
      next: () => {
        this.message.set('Role updated.');
        this.load();
      },
      error: (e: HttpErrorResponse) => {
        this.fail(e, 'Could not change the role.');
        this.load();
      },
    });
  }

  remove(member: Member): void {
    if (!confirm(`Remove ${member.email} from your organization?`)) {
      return;
    }
    this.clear();
    this.api.removeMember(member.userId).subscribe({
      next: () => {
        this.message.set('Member removed.');
        this.load();
      },
      error: (e: HttpErrorResponse) => this.fail(e, 'Could not remove the member.'),
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
