import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { authGuard } from './auth.guard';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('auth', () => {
  let http: HttpTestingController;
  let auth: AuthService;
  let client: HttpClient;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    client = TestBed.inject(HttpClient);
  });

  afterEach(() => http.verify());

  it('stores the token on login without sending an Authorization header', () => {
    auth.login('a@b.test', 'pw').subscribe();
    const login = http.expectOne('/api/auth/login');
    expect(login.request.headers.has('Authorization')).toBeFalse();
    login.flush({ accessToken: 'tok' });

    expect(auth.isLoggedIn()).toBeTrue();
    expect(sessionStorage.getItem('findoc.token')).toBe('tok');
  });

  it('attaches the bearer token to API calls', () => {
    auth.login('a@b.test', 'pw').subscribe();
    http.expectOne('/api/auth/login').flush({ accessToken: 'tok' });

    client.get('/api/documents').subscribe();
    const req = http.expectOne('/api/documents');
    expect(req.request.headers.get('Authorization')).toBe('Bearer tok');
    req.flush({});
  });

  it('signs out when the API returns 401', () => {
    auth.login('a@b.test', 'pw').subscribe();
    http.expectOne('/api/auth/login').flush({ accessToken: 'tok' });

    client.get('/api/documents').subscribe({ error: () => undefined });
    http.expectOne('/api/documents').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(auth.isLoggedIn()).toBeFalse();
    expect(sessionStorage.getItem('findoc.token')).toBeNull();
  });

  it('guard sends anonymous users to login and lets signed-in users through', () => {
    const run = () => TestBed.runInInjectionContext(() => authGuard({} as never, {} as never));
    expect(TestBed.inject(Router).serializeUrl(run() as never)).toBe('/login');

    auth.login('a@b.test', 'pw').subscribe();
    http.expectOne('/api/auth/login').flush({ accessToken: 'tok' });
    expect(run()).toBeTrue();
  });
});
