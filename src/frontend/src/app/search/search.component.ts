import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { SearchResult, SearchService } from './search.service';

@Component({
  selector: 'app-search',
  template: `
    <main>
      <h1>Search documents</h1>
      <p class="note">Local demo search uses word overlap only. No model or external provider is used.</p>
      <form (submit)="$event.preventDefault(); search(query.value)">
        <label for="query">Search query</label>
        <div class="search-box">
          <input #query id="query" type="search" maxlength="500" required placeholder="Search document passages" />
          <button type="submit" [disabled]="loading()">Search</button>
        </div>
      </form>

      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      @if (loading()) { <p>Searching…</p> }
      @else if (searched() && results().length === 0) { <p>No matching passages found.</p> }

      <ol class="results">
        @for (result of results(); track result.chunkId) {
          <li>
            <article>
              <header>
                <strong>{{ result.documentTitle }}</strong>
                <span>Passage {{ result.ordinal + 1 }} · Similarity {{ (result.score * 100).toFixed(1) }}%</span>
              </header>
              <p>{{ result.snippet }}</p>
              <small>{{ source(result) }}</small>
            </article>
          </li>
        }
      </ol>
    </main>
  `,
  styles: `
    :host { display: block; max-width: 60rem; margin: 1rem auto; padding: 0 1rem; font-family: system-ui, sans-serif; }
    main { padding-bottom: 2rem; }
    .note { color: #555; }
    form { margin: 1.5rem 0; }
    label { display: block; margin-bottom: 0.4rem; }
    .search-box { display: flex; gap: 0.5rem; }
    input { flex: 1; padding: 0.6rem; }
    .results { padding-left: 1.5rem; }
    article { border: 1px solid #ddd; border-radius: 0.4rem; padding: 1rem; margin: 0.8rem 0; }
    header { display: flex; justify-content: space-between; gap: 1rem; flex-wrap: wrap; }
    article p { white-space: pre-wrap; }
    small { color: #555; }
    .error { color: #b00020; }
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
