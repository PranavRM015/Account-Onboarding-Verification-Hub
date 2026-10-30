import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { adminGuard, userGuard } from './auth.guard';
import { AuthService } from './auth.service';

describe('role guards', () => {
  let auth: AuthService;
  let router: Router;

  const run = (guard: typeof adminGuard) =>
    TestBed.runInInjectionContext(() => guard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot));
  const redirect = (guard: typeof adminGuard) => router.serializeUrl(run(guard) as UrlTree);

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    auth = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
    auth.clearToken();
  });

  afterEach(() => auth.clearToken());

  it('sends a signed-out visitor to the login for that area', () => {
    expect(redirect(adminGuard)).toBe('/admin/login');
    expect(redirect(userGuard)).toBe('/user/login');
  });

  it('lets an admin into the admin area only', () => {
    auth.saveSession('token', 'Admin');

    expect(run(adminGuard)).toBeTrue();
    expect(redirect(userGuard)).toBe('/admin/home');
  });

  it('lets a user into the user area only', () => {
    auth.saveSession('token', 'User');

    expect(run(userGuard)).toBeTrue();
    expect(redirect(adminGuard)).toBe('/user/home');
  });

  it('treats a token with no role (an old sign-in) as signed out', () => {
    localStorage.setItem('authToken', 'old-token');

    expect(redirect(adminGuard)).toBe('/admin/login');
  });

  it('forgets both the token and the role when cleared', () => {
    auth.saveSession('token', 'Admin');

    auth.clearToken();

    expect(auth.token).toBeNull();
    expect(auth.role).toBeNull();
  });
});
