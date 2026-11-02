import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { HistoryComponent } from './history.component';

describe('HistoryComponent', () => {
  let fixture: ComponentFixture<HistoryComponent>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(HistoryComponent);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('lists private conversations and supports paging', () => {
    const request = http.expectOne('/api/conversations?page=1&pageSize=20');
    request.flush({
      items: [{
        id: 'c1', title: 'Revenue report', createdAt: '2026-11-02T00:00:00Z',
        updatedAt: '2026-11-02T00:00:00Z', messageCount: 4,
      }],
      total: 25,
      page: 1,
      pageSize: 20,
    });
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Revenue report');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('2 questions');
    fixture.componentInstance.load(2);
    http.expectOne('/api/conversations?page=2&pageSize=20').flush({
      items: [], total: 25, page: 2, pageSize: 20,
    });
    fixture.detectChanges();
    expect(fixture.componentInstance.page()).toBe(2);
  });

  it('shows an empty state and safe authorization errors', () => {
    http.expectOne('/api/conversations?page=1&pageSize=20').flush(
      { detail: 'Access denied.' },
      { status: 403, statusText: 'Forbidden' },
    );
    fixture.detectChanges();

    expect(fixture.componentInstance.error()).toContain('permission');
  });
});
