import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { AnswerCitation, AnswerResponse, AnswersService } from './answers.service';

@Component({
  selector: 'app-answers',
  template: `
    <main>
      <h1>Ask your documents</h1>
      <p class="note">Local demo answers use exact word overlap and quoted source passages only. No external model is used.</p>
      <form (submit)="$event.preventDefault(); ask(question.value)">
        <label for="question">Question</label>
        <textarea #question id="question" maxlength="1000" required rows="4" placeholder="Ask a question about your documents"></textarea>
        <button type="submit" [disabled]="loading()">Ask</button>
      </form>

      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      @if (loading()) { <p>Preparing an answer…</p> }
      @if (response(); as result) {
        <section aria-live="polite">
          <h2>Answer</h2>
          <p class="answer" [class.insufficient]="result.insufficientEvidence">{{ result.answer }}</p>
          @if (result.citations.length > 0) {
            <h2>Sources</h2>
            <ol>
              @for (citation of result.citations; track citation.chunkId) {
                <li><strong>{{ citation.documentTitle }}</strong> · {{ source(citation) }}</li>
              }
            </ol>
          }
        </section>
      }
    </main>
  `,
  styles: `
    :host { display: block; max-width: 60rem; margin: 1rem auto; padding: 0 1rem; font-family: system-ui, sans-serif; }
    main { padding-bottom: 2rem; }
    .note { color: #555; }
    form { margin: 1.5rem 0; }
    label, textarea { display: block; width: 100%; }
    textarea { box-sizing: border-box; margin: 0.4rem 0 0.8rem; padding: 0.6rem; }
    .answer { white-space: pre-wrap; }
    .insufficient, .note { color: #555; }
    .error { color: #b00020; }
  `,
})
export class AnswersComponent {
  private readonly api = inject(AnswersService);

  readonly response = signal<AnswerResponse | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');

  ask(question: string): void {
    if (!question.trim()) {
      this.error.set('Enter a question.');
      this.response.set(null);
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.response.set(null);
    this.api.ask(question.trim()).subscribe({
      next: (result) => {
        this.response.set(result);
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
