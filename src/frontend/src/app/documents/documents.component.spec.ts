import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { DocumentsComponent } from './documents.component';

const doc = (id: string, title: string) => ({
  id, title, fileName: `${title}.pdf`, extension: '.pdf', sizeBytes: 2048, createdAt: '2026-01-01T00:00:00Z', updatedAt: null,
});

describe('DocumentsComponent', () => {
  let fixture: ComponentFixture<DocumentsComponent>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(DocumentsComponent);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const flushList = (items: ReturnType<typeof doc>[], total = items.length) => {
    http.expectOne((r) => r.url === '/api/documents' && r.method === 'GET').flush({ items, total, page: 1, pageSize: 10 });
    fixture.detectChanges();
  };

  it('lists the documents returned by the API', () => {
    flushList([doc('1', 'Q3 Report'), doc('2', 'Budget')]);
    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('Q3 Report');
    expect(text).toContain('Budget');
  });

  it('shows an empty state', () => {
    flushList([]);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No documents yet.');
  });

  it('shows a permission message on 403', () => {
    http.expectOne('/api/documents?page=1&pageSize=10').flush({}, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('You do not have permission');
  });

  it('rejects disallowed files before calling the API', () => {
    flushList([]);
    const input = document.createElement('input');
    const file = new File(['x'], 'malware.exe');
    const transfer = new DataTransfer();
    transfer.items.add(file);
    input.files = transfer.files;

    fixture.componentInstance.upload(input, document.createElement('input'));
    http.expectNone('/api/documents');
    expect(fixture.componentInstance.error()).toContain('Only PDF, CSV and XLSX');
  });
});
