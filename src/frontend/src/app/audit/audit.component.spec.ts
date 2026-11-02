import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AuditComponent } from './audit.component';

describe('AuditComponent', () => {
  let fixture: ComponentFixture<AuditComponent>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(AuditComponent);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('lists events, applies filters and pages', () => {
    http.expectOne('/api/audit?page=1&pageSize=50').flush({
      items: [{
        id: 'e1', actorUserId: 'u1', action: 'document.upload', documentId: null,
        targetUserId: null, detail: null, occurredAt: '2026-11-02T00:00:00Z',
      }],
      total: 120, page: 1, pageSize: 50,
    });
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('document.upload');
    expect(text).toContain('Page 1 of 3');

    fixture.componentInstance.capture('', '', ' u1 ', 'document.upload');
    fixture.componentInstance.apply(new Event('submit'));
    http.expectOne('/api/audit?page=1&pageSize=50&actor=u1&action=document.upload')
      .flush({ items: [], total: 0, page: 1, pageSize: 50 });
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No audit events match.');
  });

  it('shows a safe message when access is denied', () => {
    http.expectOne('/api/audit?page=1&pageSize=50').flush({}, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('do not have permission');
  });
});
