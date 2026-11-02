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
      <h1>Conversation history</h1>
      <p class="note">Conversations are private to you and retained under the configured history policy.</p>
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      @if (loading()) { <p>Loading conversations…</p> }
      @else if (items().length === 0) { <p>No conversations yet.</p> }
      @else {
        <ol>
          @for (conversation of items(); track conversation.id) {
            <li>
              <button type="button" (click)="open(conversation)">
                <strong>{{ conversation.title }}</strong>
                <span>{{ conversation.messageCount / 2 }} questions · {{ conversation.updatedAt | date: 'medium' }}</span>
              </button>
            </li>
          }
        </ol>
        <nav class="pager" aria-label="Conversation pages">
          <button type="button" [disabled]="page() <= 1" (click)="load(page() - 1)">Previous</button>
          <span>Page {{ page() }} of {{ totalPages() }}</span>
          <button type="button" [disabled]="page() >= totalPages()" (click)="load(page() + 1)">Next</button>
        </nav>
      }
    </main>
  `,
  styles: `
    :host { display: block; max-width: 60rem; margin: 1rem auto; padding: 0 1rem; font-family: system-ui, sans-serif; }
    .note { color: #555; }
    ol { padding: 0; list-style: none; }
    li { border-bottom: 1px solid #ddd; }
    li button { display: flex; width: 100%; justify-content: space-between; gap: 1rem; padding: 1rem; text-align: left; }
    li span { color: #555; }
    .pager { display: flex; justify-content: center; align-items: center; gap: 1rem; margin: 1rem; }
    .error { color: #b00020; }
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
