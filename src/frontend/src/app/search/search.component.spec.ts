import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SearchComponent } from './search.component';

describe('SearchComponent', () => {
  let fixture: ComponentFixture<SearchComponent>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SearchComponent);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('shows search results with page citation metadata', () => {
    fixture.componentInstance.search('  quarterly revenue  ');
    const request = http.expectOne('/api/search');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ query: 'quarterly revenue', limit: 10 });
    request.flush({
      results: [{
        documentId: 'd1', documentTitle: 'Quarterly report', chunkId: 'c1', ordinal: 2,
        pageStart: 3, pageEnd: 4, sheetName: null, rowStart: null, rowEnd: null,
        score: 0.92, snippet: 'Revenue increased this quarter.',
      }],
    });
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('Quarterly report');
    expect(text).toContain('Passage 3');
    expect(text).toContain('Revenue increased this quarter.');
    expect(text).toContain('Pages 3–4');
  });

  it('shows sheet and row citation metadata', () => {
    expect(fixture.componentInstance.source({
      documentId: 'd1', documentTitle: 'Financials', chunkId: 'c1', ordinal: 0,
      pageStart: null, pageEnd: null, sheetName: 'Balance Sheet', rowStart: 5, rowEnd: 8,
      score: 0.8, snippet: 'Cash and equivalents',
    })).toBe('Sheet Balance Sheet, rows 5–8');
  });

  it('does not send an empty query', () => {
    fixture.componentInstance.search('  ');

    http.expectNone('/api/search');
    expect(fixture.componentInstance.error()).toBe('Enter a search query.');
  });

  it('shows safe API validation errors', () => {
    fixture.componentInstance.search('x'.repeat(501));
    http.expectOne('/api/search').flush(
      { detail: 'The search query must be 500 characters or fewer.' },
      { status: 400, statusText: 'Bad Request' },
    );
    expect(fixture.componentInstance.error()).toContain('500 characters');
  });
});
