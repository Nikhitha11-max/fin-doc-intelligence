import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { AuditEvent, AuditFilters, AuditService } from './audit.service';

@Component({
  selector: 'app-audit',
  imports: [DatePipe],
  template: `
    <main>
      <header class="page-head">
        <h1>Audit log</h1>
        <p>Records who did what and when. Document contents and prompts are never stored here.</p>
      </header>
      <form class="card mb-6 grid items-end gap-4 sm:grid-cols-2 lg:grid-cols-5" (submit)="apply($event)">
        <label>From <input type="date" class="mt-1.5" #from /></label>
        <label>To <input type="date" class="mt-1.5" #to /></label>
        <label>Actor <input type="text" class="mt-1.5" #actor maxlength="450" /></label>
        <label>Action <input type="text" class="mt-1.5" #action maxlength="100" /></label>
        <button type="submit" (click)="capture(from.value, to.value, actor.value, action.value)">Filter</button>
      </form>
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      @if (loading()) { <p class="loading">Loading audit events…</p> }
      @else if (items().length === 0) { <p class="empty">No audit events match.</p> }
      @else {
        <div class="table-wrap">
          <table>
            <thead><tr><th>When</th><th>Actor</th><th>Action</th><th>Detail</th></tr></thead>
            <tbody>
              @for (event of items(); track event.id) {
                <tr>
                  <td class="whitespace-nowrap text-slate-500">{{ event.occurredAt | date: 'medium' }}</td>
                  <td class="font-mono text-xs">{{ event.actorUserId }}</td>
                  <td><span class="badge">{{ event.action }}</span></td>
                  <td>{{ event.detail ?? '' }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <nav class="pager" aria-label="Audit pages">
          <button type="button" class="btn-secondary btn-sm" [disabled]="page() <= 1" (click)="load(page() - 1)">Previous</button>
          <span>Page {{ page() }} of {{ totalPages() }}</span>
          <button type="button" class="btn-secondary btn-sm" [disabled]="page() >= totalPages()" (click)="load(page() + 1)">Next</button>
        </nav>
      }
    </main>
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
