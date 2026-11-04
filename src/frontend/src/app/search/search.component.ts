import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { SearchResult, SearchService } from './search.service';

@Component({
  selector: 'app-search',
  template: `
    <main>
      <header class="page-head">
        <h1>Search documents</h1>
        <p>Local demo search uses word overlap only. No model or external provider is used.</p>
      </header>
      <form class="card mb-6" (submit)="$event.preventDefault(); search(query.value)">
        <label for="query">Search query</label>
        <div class="mt-1.5 flex flex-col gap-2 sm:flex-row">
          <input #query id="query" type="search" maxlength="500" required placeholder="Search document passages" />
          <button type="submit" [disabled]="loading()">Search</button>
        </div>
      </form>

      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      @if (loading()) { <p class="loading">Searching…</p> }
      @else if (searched() && results().length === 0) { <p class="empty">No matching passages found.</p> }

      <ol class="space-y-3">
        @for (result of results(); track result.chunkId) {
          <li>
            <article class="card transition hover:shadow-md">
              <header class="flex flex-wrap items-center justify-between gap-2">
                <strong class="text-base text-slate-900">{{ result.documentTitle }}</strong>
                <span class="badge !bg-indigo-50 !text-indigo-700 !ring-indigo-200">Passage {{ result.ordinal + 1 }} · Similarity {{ (result.score * 100).toFixed(1) }}%</span>
              </header>
              <p class="mt-3 whitespace-pre-wrap leading-relaxed text-slate-700">{{ result.snippet }}</p>
              <small class="mt-3 block text-xs font-medium text-slate-500">{{ source(result) }}</small>
            </article>
          </li>
        }
      </ol>
    </main>
  `,
})
export class SearchComponent {
  private readonly api = inject(SearchService);

  readonly results = signal<SearchResult[]>([]);
  readonly loading = signal(false);
  readonly searched = signal(false);
  readonly error = signal('');

  search(query: string): void {
    if (!query.trim()) {
      this.error.set('Enter a search query.');
      this.results.set([]);
      return;
    }

    this.loading.set(true);
    this.searched.set(true);
    this.error.set('');
    this.api.search(query.trim()).subscribe({
      next: (response) => {
        this.results.set(response.results);
        this.loading.set(false);
      },
      error: (e: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(e.status === 400 && typeof e.error?.detail === 'string'
          ? e.error.detail
          : e.status === 403
            ? 'You do not have permission to search documents.'
            : 'Search failed. Please try again.');
      },
    });
  }

  source(result: SearchResult): string {
    if (result.pageStart !== null) {
      return result.pageStart === result.pageEnd
        ? `Page ${result.pageStart}`
        : `Pages ${result.pageStart}–${result.pageEnd}`;
    }
    if (result.sheetName !== null && result.rowStart !== null) {
      const rows = result.rowStart === result.rowEnd
        ? `row ${result.rowStart}`
        : `rows ${result.rowStart}–${result.rowEnd}`;
      return `Sheet ${result.sheetName}, ${rows}`;
    }
    return 'Source location unavailable';
  }
}
