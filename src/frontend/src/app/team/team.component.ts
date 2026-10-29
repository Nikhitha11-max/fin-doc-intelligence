import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { AccessService, describeError, Member, ROLES, TenantRole } from '../core/access.service';

const PAGE_SIZE = 10;

@Component({
  selector: 'app-team',
  template: `
    <h1>Team</h1>
    <section class="add">
      <input #email type="email" placeholder="Email" aria-label="Email" />
      <input #password type="password" placeholder="Temporary password" aria-label="Password" autocomplete="new-password" />
      <select #role aria-label="Role">
        @for (r of roles; track r) {
          <option [value]="r" [selected]="r === 'Analyst'">{{ r }}</option>
        }
      </select>
      <button type="button" (click)="add(email, password, role)">Add member</button>
    </section>

    @if (message()) { <p class="message" role="status">{{ message() }}</p> }
    @if (error()) { <p class="error" role="alert">{{ error() }}</p> }

    @if (loading()) {
      <p>Loading…</p>
    } @else {
      <table>
        <thead><tr><th>Email</th><th>Role</th><th></th></tr></thead>
        <tbody>
          @for (m of items(); track m.userId) {
            <tr>
              <td>{{ m.email }}</td>
              <td>
                <select [value]="m.role" [attr.aria-label]="'Role for ' + m.email" (change)="changeRole(m, $any($event.target).value)">
                  @for (r of roles; track r) {
                    <option [value]="r" [selected]="r === m.role">{{ r }}</option>
                  }
                </select>
              </td>
              <td><button type="button" (click)="remove(m)">Remove</button></td>
            </tr>
          }
        </tbody>
      </table>
      <nav class="pager" aria-label="Pagination">
        <button type="button" [disabled]="page() <= 1" (click)="goTo(page() - 1)">Previous</button>
        <span>Page {{ page() }} of {{ totalPages() }} ({{ total() }} members)</span>
        <button type="button" [disabled]="page() >= totalPages()" (click)="goTo(page() + 1)">Next</button>
      </nav>
    }
  `,
  styles: `
    :host { display: block; max-width: 60rem; margin: 1rem auto; padding: 0 1rem; font-family: system-ui, sans-serif; }
    .add { display: flex; gap: 0.5rem; flex-wrap: wrap; margin: 1rem 0; }
    table { width: 100%; border-collapse: collapse; }
    th, td { text-align: left; padding: 0.5rem; border-bottom: 1px solid #ddd; }
    .pager { display: flex; gap: 1rem; align-items: center; margin-top: 1rem; }
    .message { color: #1b5e20; }
    .error { color: #b00020; }
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
