import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AnswerCitation, AnswerResponse, AnswersService, ConversationMessage } from './answers.service';

@Component({
  selector: 'app-answers',
  template: `
    <main>
      <header class="page-head">
        <h1>Ask your documents</h1>
        <p>Local demo answers use exact word overlap and quoted source passages only. No external model is used.</p>
      </header>
      <form class="card mb-6" (submit)="$event.preventDefault(); ask(question.value)">
        <label for="question">Question</label>
        <textarea #question id="question" class="mt-1.5" maxlength="1000" required rows="4" placeholder="Ask a question about your documents"></textarea>
        <div class="mt-3 flex justify-end">
          <button type="submit" [disabled]="loading()">Ask</button>
        </div>
      </form>

      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      @if (loading()) { <p class="loading">Preparing an answer…</p> }
      @if (response(); as result) {
        <section class="card mb-6 space-y-3 border-l-4 !border-indigo-500" aria-live="polite">
          <h2>Answer</h2>
          <p class="whitespace-pre-wrap leading-relaxed" [class.text-slate-500]="result.insufficientEvidence" [class.italic]="result.insufficientEvidence">{{ result.answer }}</p>
          @if (result.citations.length > 0) {
            <h2 class="pt-2">Sources</h2>
            <ol class="list-decimal space-y-1 pl-5">
              @for (citation of result.citations; track citation.chunkId) {
                <li><strong class="text-slate-900">{{ citation.documentTitle }}</strong> · <span class="text-slate-500">{{ source(citation) }}</span></li>
              }
            </ol>
          }
        </section>
      }
      @if (messages().length > 0) {
        <section class="space-y-3" aria-label="Conversation history">
          @for (message of messages(); track message.id) {
            <article class="card" [class.!bg-indigo-50]="message.role === 'user'">
              <h2 class="mb-2 text-xs font-semibold uppercase tracking-wide" [class.text-indigo-700]="message.role === 'user'" [class.text-slate-500]="message.role !== 'user'">{{ message.role === 'user' ? 'You' : 'Answer' }}</h2>
              <p class="whitespace-pre-wrap leading-relaxed">{{ message.text }}</p>
              @if (message.citations.length > 0) {
                <ul class="mt-3 space-y-1 border-t border-slate-200 pt-3 text-xs">
                  @for (citation of message.citations; track citation.chunkId) {
                    <li><strong class="text-slate-900">{{ citation.documentTitle }}</strong> · <span class="text-slate-500">{{ source(citation) }}</span></li>
                  }
                </ul>
              }
            </article>
          }
        </section>
      }
    </main>
  `,
})
export class AnswersComponent implements OnInit {
  private readonly api = inject(AnswersService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly response = signal<AnswerResponse | null>(null);
  readonly messages = signal<ConversationMessage[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  private conversationId: string | undefined;

  ngOnInit(): void {
    const id = this.route.snapshot.queryParamMap.get('conversationId');
    if (id) {
      this.conversationId = id;
      this.loadConversation(id);
    }
  }

  ask(question: string): void {
    if (!question.trim()) {
      this.error.set('Enter a question.');
      this.response.set(null);
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.response.set(null);
    this.api.ask(question.trim(), this.conversationId).subscribe({
      next: (result) => {
        this.response.set(result);
        this.conversationId = result.conversationId;
        void this.router.navigate([], {
          relativeTo: this.route,
          queryParams: { conversationId: result.conversationId },
          queryParamsHandling: 'merge',
          replaceUrl: true,
        });
        this.loadConversation(result.conversationId);
        this.loading.set(false);
      },
      error: (e: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(e.status === 400 && typeof e.error?.detail === 'string'
          ? e.error.detail
          : e.status === 403
            ? 'You do not have permission to ask questions.'
            : e.status === 503
              ? 'The answer provider is temporarily unavailable.'
              : 'Unable to answer the question. Please try again.');
      },
    });
  }

  private loadConversation(id: string): void {
    this.api.get(id).subscribe({
      next: (conversation) => {
        this.messages.set(conversation.messages);
        this.response.set(null);
      },
      error: (e: HttpErrorResponse) => {
        this.error.set(e.status === 404 || e.status === 403
          ? 'This conversation is no longer available.'
          : 'Unable to load this conversation.');
      },
    });
  }

  source(citation: AnswerCitation): string {
    if (citation.pageStart !== null) {
      return citation.pageStart === citation.pageEnd
        ? `Page ${citation.pageStart}`
        : `Pages ${citation.pageStart}–${citation.pageEnd}`;
    }
    if (citation.sheetName !== null && citation.rowStart !== null) {
      const rows = citation.rowStart === citation.rowEnd
        ? `row ${citation.rowStart}`
        : `rows ${citation.rowStart}–${citation.rowEnd}`;
      return `Sheet ${citation.sheetName}, ${rows}`;
    }
    return 'Source location unavailable';
  }
}
