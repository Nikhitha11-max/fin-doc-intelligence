import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AnswersService, ConversationListItem } from '../answers/answers.service';

@Component({
  selector: 'app-history',
  imports: [DatePipe],
  template: `
    <main>
      <header class="page-head">
        <h1>Conversation history</h1>
        <p>Conversations are private to you and retained under the configured history policy.</p>
      </header>
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      @if (loading()) { <p class="loading">Loading conversations…</p> }
      @else if (items().length === 0) { <p class="empty">No conversations yet.</p> }
      @else {
        <ol class="card divide-y divide-slate-100 !p-0">
          @for (conversation of items(); track conversation.id) {
            <li>
              <button type="button" class="!flex !w-full !justify-between !gap-4 !rounded-none !bg-transparent !px-5 !py-4 !text-left !font-normal !text-slate-700 !shadow-none hover:!bg-slate-50" (click)="open(conversation)">
                <strong class="truncate text-slate-900">{{ conversation.title }}</strong>
                <span class="shrink-0 text-xs text-slate-500">{{ conversation.messageCount / 2 }} questions · {{ conversation.updatedAt | date: 'medium' }}</span>
              </button>
            </li>
          }
        </ol>
        <nav class="pager" aria-label="Conversation pages">
          <button type="button" class="btn-secondary btn-sm" [disabled]="page() <= 1" (click)="load(page() - 1)">Previous</button>
          <span>Page {{ page() }} of {{ totalPages() }}</span>
          <button type="button" class="btn-secondary btn-sm" [disabled]="page() >= totalPages()" (click)="load(page() + 1)">Next</button>
        </nav>
      }
    </main>
  `,
})
export class HistoryComponent implements OnInit {
  private readonly api = inject(AnswersService);
  private readonly router = inject(Router);

  readonly items = signal<ConversationListItem[]>([]);
  readonly page = signal(1);
  readonly total = signal(0);
  readonly loading = signal(false);
  readonly error = signal('');

  ngOnInit(): void {
    this.load(1);
  }

  load(page: number): void {
    this.loading.set(true);
    this.error.set('');
    this.api.list(page).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.page.set(result.page);
        this.total.set(result.total);
        this.loading.set(false);
      },
      error: (e: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(e.status === 403
          ? 'You do not have permission to view conversation history.'
          : 'Unable to load conversation history.');
      },
    });
  }

  open(conversation: ConversationListItem): void {
    void this.router.navigate(['/answers'], { queryParams: { conversationId: conversation.id } });
  }

  totalPages(): number {
    return Math.max(1, Math.ceil(this.total() / 20));
  }
}
