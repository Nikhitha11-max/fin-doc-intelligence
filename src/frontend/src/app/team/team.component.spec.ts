import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TeamComponent } from './team.component';

describe('TeamComponent', () => {
  let fixture: ComponentFixture<TeamComponent>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(TeamComponent);
    fixture.detectChanges();
    http.expectOne((r) => r.url === '/api/members').flush({
      items: [{ userId: 'u1', email: 'a@x.test', role: 'Analyst', createdAt: '2026-01-01T00:00:00Z' }],
      total: 1, page: 1, pageSize: 10,
    });
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('lists members', () => {
    expect(fixture.nativeElement.textContent).toContain('a@x.test');
  });

  it('shows the last-admin conflict message', () => {
    fixture.componentInstance.changeRole(fixture.componentInstance.items()[0], 'Auditor');
    http.expectOne('/api/members/u1/role').flush({ detail: 'An organization must keep at least one administrator.' }, { status: 409, statusText: 'Conflict' });
    http.expectOne((r) => r.url === '/api/members').flush({ items: [], total: 0, page: 1, pageSize: 10 });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role=alert]').textContent).toContain('at least one administrator');
  });
});
