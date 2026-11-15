import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, UrlTree, provideRouter } from '@angular/router';
import { AuthService } from './auth';
import { authInterceptor } from './auth.interceptor';
import { authGuard, roleGuard } from './guards';
import { LoginResponse } from './models';

const session = (role: 'Employee' | 'Manager' | 'HrAdmin'): LoginResponse => ({
  token: 'tok-123',
  user: { id: 1, name: 'ทดสอบ', email: 'a@b.c', role, department: 'IT' },
});

describe('auth core', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'login', children: [] }]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
  });

  function signIn(role: 'Employee' | 'Manager' | 'HrAdmin') {
    const auth = TestBed.inject(AuthService);
    const http = TestBed.inject(HttpTestingController);
    const done = auth.login('a@b.c', 'x');
    http.expectOne('/api/auth/login').flush(session(role));
    return done;
  }

  const runGuard = (guard: typeof authGuard) =>
    TestBed.runInInjectionContext(() => guard({} as never, {} as never)) as boolean | UrlTree;

  it('guard sends anonymous users to /login', () => {
    const result = runGuard(authGuard);
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/login');
  });

  it('guard lets signed-in users through and persists the session', async () => {
    await signIn('Employee');
    expect(runGuard(authGuard)).toBe(true);
    expect(localStorage.getItem('leaveflow.session')).toContain('tok-123');
  });

  it('role guard sends an employee away from HR pages to their home', async () => {
    await signIn('Employee');
    const result = runGuard(roleGuard('HrAdmin'));
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/me');
  });

  it('interceptor adds the bearer token to API calls only', async () => {
    await signIn('Manager');
    const client = TestBed.inject(HttpClient);
    const http = TestBed.inject(HttpTestingController);
    client.get('/api/balances').subscribe();
    client.get('/assets/x.json').subscribe();
    expect(http.expectOne('/api/balances').request.headers.get('Authorization')).toBe('Bearer tok-123');
    expect(http.expectOne('/assets/x.json').request.headers.has('Authorization')).toBe(false);
  });

  it('a 401 from the API signs the user out', async () => {
    await signIn('Manager');
    const auth = TestBed.inject(AuthService);
    TestBed.inject(HttpClient).get('/api/balances').subscribe({ error: () => undefined });
    TestBed.inject(HttpTestingController).expectOne('/api/balances').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(auth.isLoggedIn()).toBe(false);
  });
});
