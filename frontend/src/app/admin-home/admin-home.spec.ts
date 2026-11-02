import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../app';
import { AuthService } from '../auth/auth.service';

describe('Admin ticket list', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()] });
    TestBed.inject(AuthService).saveSession('test-token', 'Admin');
    harness = await RouterTestingHarness.create();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    TestBed.inject(AuthService).clearToken();
  });

  it('loads the review queue and links to the selected ticket and customer form', async () => {
    await harness.navigateByUrl('/admin/home');
    expect(harness.routeNativeElement!.textContent).toContain('Loading tickets...');
    http.expectOne('/api/admins/tickets').flush([
      { ticketId: 41, customerName: 'Casey Morgan', riskRating: 'Medium', verificationDeadline: '2026-11-12T00:00:00Z' },
      { ticketId: 42, customerName: 'Jordan Lee', riskRating: null, verificationDeadline: '2026-11-16T00:00:00Z' },
    ]);
    harness.detectChanges();
    const root = harness.routeNativeElement!;
    expect(root.textContent).toContain('Casey Morgan');
    expect(root.textContent).toContain('Under review');
    expect(root.textContent).toContain('Not rated');
    expect(root.textContent).toContain('2 tickets awaiting review');
    expect(root.querySelector('a[href="/inputForm"]')).not.toBeNull();
    root.querySelector<HTMLAnchorElement>('a[href="/review/41"]')!.click();
    await harness.fixture.whenStable();
    expect(TestBed.inject(Router).url).toBe('/review/41');
    http.expectOne('/api/admins/tickets/41').flush('Missing', { status: 404, statusText: 'Not Found' });
  });

  it('shows an empty state only after the request succeeds', async () => {
    await harness.navigateByUrl('/admin/home');
    expect(harness.routeNativeElement!.textContent).not.toContain("You're caught up");
    http.expectOne('/api/admins/tickets').flush([]);
    harness.detectChanges();
    expect(harness.routeNativeElement!.textContent).toContain('No tickets awaiting review');
  });

  it('keeps admin navigation consistent between tickets and onboarding', async () => {
    await harness.navigateByUrl('/admin/home');
    http.expectOne('/api/admins/tickets').flush([]);
    harness.detectChanges();
    let navigation = harness.routeNativeElement!.querySelector('nav[aria-label="Admin navigation"]')!;
    expect(navigation.querySelector('[aria-current=page]')!.textContent).toContain('Tickets');
    navigation.querySelector<HTMLAnchorElement>('a[href="/inputForm"]')!.click();
    await harness.fixture.whenStable();
    expect(TestBed.inject(Router).url).toBe('/inputForm');
    navigation = harness.routeNativeElement!.querySelector('nav[aria-label="Admin navigation"]')!;
    expect(navigation.querySelector('[aria-current=page]')!.textContent).toContain('New customer');
    navigation.querySelector<HTMLAnchorElement>('a[href="/admin/home"]')!.click();
    await harness.fixture.whenStable();
    http.expectOne('/api/admins/tickets').flush([]);
    expect(TestBed.inject(Router).url).toBe('/admin/home');
  });

  it('logs out through shared navigation and blocks repeated clicks', async () => {
    await harness.navigateByUrl('/admin/home');
    http.expectOne('/api/admins/tickets').flush([]);
    harness.detectChanges();
    const logout = harness.routeNativeElement!.querySelector<HTMLButtonElement>('nav button')!;
    logout.click();
    harness.detectChanges();
    expect(logout.disabled).toBeTrue();
    logout.click();
    http.expectOne('/api/admins/logout').flush({ message: 'Logged out' });
    await harness.fixture.whenStable();
    expect(TestBed.inject(Router).url).toBe('/');
    expect(TestBed.inject(AuthService).token).toBeNull();
  });

  it('combines name or ID search with risk filtering and can clear no matches', async () => {
    await harness.navigateByUrl('/admin/home');
    http.expectOne('/api/admins/tickets').flush([
      { ticketId: 41, customerName: 'Casey Morgan', riskRating: 'Medium', verificationDeadline: '2026-11-12T00:00:00Z' },
      { ticketId: 42, customerName: 'Jordan Lee', riskRating: 'High', verificationDeadline: '2026-11-16T00:00:00Z' },
      { ticketId: 43, customerName: 'Casey Brooks', riskRating: null, verificationDeadline: '2026-11-18T00:00:00Z' },
    ]);
    harness.detectChanges();
    const root = harness.routeNativeElement!;
    const search = root.querySelector<HTMLInputElement>('#ticketSearch')!;
    const risk = root.querySelector<HTMLSelectElement>('#ticketRisk')!;
    search.value = '  CASEY  ';
    search.dispatchEvent(new Event('input'));
    harness.detectChanges();
    expect(root.querySelectorAll('tbody tr').length).toBe(2);
    risk.value = 'Medium';
    risk.dispatchEvent(new Event('change'));
    harness.detectChanges();
    expect(root.querySelectorAll('tbody tr').length).toBe(1);
    expect(root.textContent).toContain('Showing 1 of 3');
    search.value = '#42';
    search.dispatchEvent(new Event('input'));
    harness.detectChanges();
    expect(root.textContent).toContain('No matching tickets');
    expect(root.textContent).not.toContain("You're caught up");
    risk.value = 'High';
    risk.dispatchEvent(new Event('change'));
    harness.detectChanges();
    expect(root.querySelector('tbody')!.textContent).toContain('Jordan Lee');
    search.value = 'missing';
    search.dispatchEvent(new Event('input'));
    harness.detectChanges();
    root.querySelector<HTMLButtonElement>('.empty-state button')!.click();
    harness.detectChanges();
    expect(search.value).toBe('');
    expect(risk.value).toBe('All');
    expect(root.querySelectorAll('tbody tr').length).toBe(3);
    http.expectNone('/api/admins/tickets');
  });

  it('refreshes once while pending and retains the filters for new data', async () => {
    await harness.navigateByUrl('/admin/home');
    http.expectOne('/api/admins/tickets').flush([]);
    harness.detectChanges();
    const root = harness.routeNativeElement!;
    const search = root.querySelector<HTMLInputElement>('#ticketSearch')!;
    search.value = 'Casey';
    search.dispatchEvent(new Event('input'));
    const refresh = root.querySelector<HTMLButtonElement>('.queue-controls button')!;
    refresh.click();
    harness.detectChanges();
    expect(refresh.disabled).toBeTrue();
    refresh.click();
    http.expectOne('/api/admins/tickets').flush([
      { ticketId: 41, customerName: 'Casey Morgan', riskRating: 'Low', verificationDeadline: '2026-11-12T00:00:00Z' },
      { ticketId: 42, customerName: 'Jordan Lee', riskRating: 'High', verificationDeadline: '2026-11-16T00:00:00Z' },
    ]);
    harness.detectChanges();
    expect(refresh.disabled).toBeFalse();
    expect(search.value).toBe('Casey');
    expect(root.querySelectorAll('tbody tr').length).toBe(1);
    expect(root.textContent).toContain('Showing 1 of 2');
  });

  it('shows a connection error and can retry', async () => {
    await harness.navigateByUrl('/admin/home');
    http.expectOne('/api/admins/tickets').flush('Unavailable', { status: 502, statusText: 'Bad Gateway' });
    harness.detectChanges();
    expect(harness.routeNativeElement!.querySelector('[role=alert]')!.textContent).toContain('Unable to load tickets');
    harness.routeNativeElement!.querySelector<HTMLButtonElement>('.page > button')!.click();
    http.expectOne('/api/admins/tickets').flush([]);
    harness.detectChanges();
    expect(harness.routeNativeElement!.textContent).toContain('No tickets awaiting review');
  });

  it('sends an expired session back to login with a visible explanation', async () => {
    await harness.navigateByUrl('/admin/home');
    http.expectOne('/api/admins/tickets').flush('Expired', { status: 401, statusText: 'Unauthorized' });
    await harness.fixture.whenStable();
    expect(TestBed.inject(Router).url).toBe('/admin/login');
    expect(harness.routeNativeElement!.textContent).toContain('Your session has expired');
  });

  it('keeps users and signed-out visitors out of the queue', async () => {
    TestBed.inject(AuthService).saveSession('user-token', 'User');
    await harness.navigateByUrl('/admin/home');
    expect(TestBed.inject(Router).url).toBe('/user/home');
    http.expectOne('/api/users/me').flush({ username: 'customer' });
    http.expectNone('/api/admins/tickets');
    TestBed.inject(AuthService).clearToken();
    await harness.navigateByUrl('/admin/home');
    expect(TestBed.inject(Router).url).toBe('/admin/login');
  });
});
