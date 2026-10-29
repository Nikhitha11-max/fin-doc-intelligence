import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AccessService, TenantRole } from '../core/access.service';
import { DocumentsComponent } from './documents.component';
import { DocumentItem } from './documents.service';

const doc = (id: string, title: string): DocumentItem => ({
  id, title, fileName: `${title}.pdf`, extension: '.pdf', sizeBytes: 2048, createdAt: '2026-01-01T00:00:00Z', updatedAt: null,
  status: 'Completed', failureCode: null, failureMessage: null,
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
    input.type = 'file';
    const file = new File(['x'], 'malware.exe');
    const transfer = new DataTransfer();
    transfer.items.add(file);
    input.files = transfer.files;

    fixture.componentInstance.upload(input, document.createElement('input'));
    http.expectNone('/api/documents');
    expect(fixture.componentInstance.error()).toContain('Only PDF, CSV and XLSX');
  });

  const failed: DocumentItem = { ...doc('d9', 'Scan'), status: 'Failed', failureCode: 'no_extractable_text', failureMessage: 'The file contains no extractable text.' };

  const setRole = (role: TenantRole) => {
    TestBed.inject(AccessService).me.set({ hasAccess: true, tenantName: 'Org', role, isPlatformAdmin: false });
    fixture.detectChanges();
  };

  it('shows each status and the safe failure reason', () => {
    flushList([doc('d1', 'Q3 Report'), { ...doc('d2', 'Pending one'), status: 'Pending' }, failed]);
    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('Completed');
    expect(text).toContain('Pending');
    expect(text).toContain('Failed');
    expect(text).toContain('The file contains no extractable text.');
  });

  const retryButtons = () =>
    Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button')).filter((b) => b.textContent?.trim() === 'Retry');

  it('offers Retry on failed documents to managers only', () => {
    flushList([doc('d1', 'Fine'), failed]);
    setRole('Analyst');
    expect(retryButtons().length).toBe(0);
    setRole('DocumentManager');
    expect(retryButtons().length).toBe(1);
  });

  it('retries a failed document and reloads the list', () => {
    flushList([failed]);
    setRole('TenantAdmin');
    retryButtons()[0].click();
    http.expectOne({ url: '/api/documents/d9/retry', method: 'POST' }).flush({ ...failed, status: 'Pending', failureCode: null, failureMessage: null });
    http.expectOne((r) => r.url === '/api/documents' && r.method === 'GET').flush({ items: [{ ...failed, status: 'Pending' }], total: 1, page: 1, pageSize: 10 });
    fixture.detectChanges();
    expect(retryButtons().length).toBe(0);
    expect(fixture.componentInstance.message()).toContain('retried');
  });
});
