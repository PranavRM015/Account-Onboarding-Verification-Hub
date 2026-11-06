import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../app';

describe('Login for each role', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()] });
    spyOn(window, 'alert');
    localStorage.removeItem('authToken');
    localStorage.removeItem('authRole');
    harness = await RouterTestingHarness.create();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const submit = async (url: string) => {
    const component = await harness.navigateByUrl(url);
    const root: HTMLElement = harness.routeNativeElement!;
    const set = (selector: string, value: string) => {
      const input = root.querySelector<HTMLInputElement>(selector)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };
    set('#Username', 'someone');
    set('#Password', 'Str0ng!Passw0rd#2026');
    root.querySelector<HTMLButtonElement>('button[type=submit]')!.click();
    await harness.fixture.whenStable();
    return component;
  };

  it('shows who you are logging in as', async () => {
    await harness.navigateByUrl('/admin/login');
    expect(harness.routeNativeElement!.querySelector('h1')!.textContent).toContain('admin');

    await harness.navigateByUrl('/user/login');
    expect(harness.routeNativeElement!.querySelector('h1')!.textContent).toContain('customer');
  });

  it('has a Forgot my password link that goes to the recovery screen for that role', async () => {
    await harness.navigateByUrl('/user/login');
    const link = harness.routeNativeElement!.querySelector<HTMLAnchorElement>('#forgotPassword')!;

    expect(link.textContent).toContain('Forgot my password');
    expect(link.getAttribute('href')).toBe('/forgot-password/user');
  });

  it('logs an admin in through the admin endpoint only', async () => {
    await submit('/admin/login');

    http.expectOne('/api/admins/login').flush({ role: 'Admin', token: 't', username: 'someone', message: 'ok', expiresAt: '' });
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/admin/home');
    http.expectOne('/api/admins/pending-customers').flush([]);
    http.expectOne('/api/admins/tickets').flush([]);
    expect(localStorage.getItem('authRole')).toBe('Admin');
  });

  it('logs a user in through the user endpoint only', async () => {
    await submit('/user/login');

    http.expectOne('/api/users/login').flush({ role: 'User', token: 't', username: 'someone', message: 'ok', expiresAt: '' });
    await harness.fixture.whenStable();
    http.expectOne('/api/users/me').flush({ username: 'someone', role: 'User' });

    expect(TestBed.inject(Router).url).toBe('/user/home');
    expect(localStorage.getItem('authRole')).toBe('User');
  });

  it('shows the lockout message for a locked user and stores no token', async () => {
    await submit('/user/login');

    http.expectOne('/api/users/login').flush('This account is locked for 15 more minutes.', { status: 423, statusText: 'Locked' });
    await harness.fixture.whenStable();

    harness.detectChanges();
    expect(harness.routeNativeElement!.textContent).toContain('This account is locked for 15 more minutes.');
    expect(window.alert).not.toHaveBeenCalled();
    expect(localStorage.getItem('authToken')).toBeNull();
  });

  it('blocks repeated submits while pending and allows retry after an error', async () => {
    await submit('/admin/login');
    harness.detectChanges();
    const button = harness.routeNativeElement!.querySelector<HTMLButtonElement>('button[type=submit]')!;
    expect(button.disabled).toBeTrue();
    expect(button.textContent).toContain('Signing in...');

    // Submitting with Enter must be guarded too, even when the button is disabled.
    harness.routeNativeElement!.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    http.expectOne('/api/admins/login').flush('Unavailable', { status: 503, statusText: 'Unavailable' });
    harness.detectChanges();
    expect(button.disabled).toBeFalse();
    expect(harness.routeNativeElement!.querySelector('[role=alert]')!.textContent).toContain('Unable to connect to the server. Please try again shortly.');

    button.click();
    http.expectOne('/api/admins/login').flush('Invalid', { status: 401, statusText: 'Unauthorized' });
    harness.detectChanges();
    expect(button.disabled).toBeFalse();
    expect(window.alert).not.toHaveBeenCalled();
  });
});