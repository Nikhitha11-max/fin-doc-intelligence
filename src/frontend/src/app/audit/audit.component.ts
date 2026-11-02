import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { AuditEvent, AuditFilters, AuditService } from './audit.service';

@Component({
  selector: 'app-audit',
  imports: [DatePipe],
  template: `
    <main>
      <h1>Audit log</h1>
      <p class="note">Records who did what and when. Document contents and prompts are never stored here.</p>
      <form class="filters" (submit)="apply($event)">
        <label>From <input type="date" #from /></label>
        <label>To <input type="date" #to /></label>
        <label>Actor <input type="text" #actor maxlength="450" /></label>
        <label>Action <input type="text" #action maxlength="100" /></label>
        <button type="submit" (click)="capture(from.value, to.value, actor.value, action.value)">Filter</button>
      </form>
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      @if (loading()) { <p>Loading audit events…</p> }
      @else if (items().length === 0) { <p>No audit events match.</p> }
      @else {
        <table>
          <thead><tr><th>When</th><th>Actor</th><th>Action</th><th>Detail</th></tr></thead>
          <tbody>
            @for (event of items(); track event.id) {
              <tr>
                <td>{{ event.occurredAt | date: 'medium' }}</td>
                <td>{{ event.actorUserId }}</td>
                <td>{{ event.action }}</td>
                <td>{{ event.detail ?? '' }}</td>
              </tr>
            }
          </tbody>
        </table>
        <nav class="pager" aria-label="Audit pages">
          <button type="button" [disabled]="page() <= 1" (click)="load(page() - 1)">Previous</button>
          <span>Page {{ page() }} of {{ totalPages() }}</span>
          <button type="button" [disabled]="page() >= totalPages()" (click)="load(page() + 1)">Next</button>
        </nav>
      }
    </main>
  `,
  styles: `
    :host { display: block; max-width: 70rem; margin: 1rem auto; padding: 0 1rem; font-family: system-ui, sans-serif; }
    .note { color: #555; }
    .filters { display: flex; flex-wrap: wrap; gap: 1rem; align-items: end; margin-bottom: 1rem; }
    .filters label { display: flex; flex-direction: column; font-size: 0.85rem; }
    table { width: 100%; border-collapse: collapse; }
    th, td { text-align: left; padding: 0.5rem; border-bottom: 1px solid #ddd; }
    .pager { display: flex; justify-content: center; align-items: center; gap: 1rem; margin: 1rem; }
    .error { color: #b00020; }
  `,
})
export class AuditComponent implements OnInit {
  private readonly api = inject(AuditService);
  private filters: AuditFilters = {};

  readonly items = signal<AuditEvent[]>([]);
  readonly page = signal(1);
  readonly total = signal(0);
  readonly pageSize = signal(50);
  readonly loading = signal(false);
  readonly error = signal('');

  totalPages(): number {
    return Math.max(1, Math.ceil(this.total() / this.pageSize()));
  }

  ngOnInit(): void {
    this.load(1);
  }

  capture(from: string, to: string, actor: string, action: string): void {
    this.filters = {
      from: from ? new Date(`${from}T00:00:00`).toISOString() : undefined,
      to: to ? new Date(`${to}T23:59:59.999`).toISOString() : undefined,
      actor: actor.trim() || undefined,
      action: action.trim() || undefined,
    };
  }

  apply(event: Event): void {
    event.preventDefault();
    this.load(1);
  }

  load(page: number): void {
    this.loading.set(true);
    this.error.set('');
    this.api.list(this.filters, page).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.page.set(result.page);
        this.total.set(result.total);
        this.pageSize.set(result.pageSize);
        this.loading.set(false);
      },
      error: (e: HttpErrorResponse) => {
        this.loading.set(false);
        this.items.set([]);
        this.error.set(e.status === 403
          ? 'You do not have permission to view the audit log.'
          : e.status === 400
            ? 'The filters are not valid.'
            : 'Unable to load the audit log.');
      },
    });
  }
}
