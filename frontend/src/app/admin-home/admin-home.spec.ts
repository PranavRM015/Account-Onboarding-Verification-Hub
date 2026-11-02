import { DatePipe } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../app';
import { AuthService } from '../auth/auth.service';
import { AdminHome, PendingCustomer } from './admin-home';

describe('AdminHome', () => {
  let fixture: ComponentFixture<AdminHome>;
  let http: HttpTestingController;

  const customer = (customerId: number, firstName: string, lastName: string, createdAt: string, middleName: string | null = null): PendingCustomer =>
    ({ customerId, firstName, middleName, lastName, createdAt });

  const load = (customers: PendingCustomer[]) => {
    http.expectOne('/api/admins/pending-customers').flush(customers);
    fixture.detectChanges();
  };

  const root = (): HTMLElement => fixture.nativeElement;
  const links = () => Array.from(root().querySelectorAll<HTMLAnchorElement>('.pending-link'));
  const shown = (iso: string) => new DatePipe('en-US').transform(iso, 'medium');

  beforeEach(async () => {
    spyOn(window, 'alert');
    await TestBed.configureTestingModule({
      imports: [AdminHome],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    TestBed.inject(AuthService).clearToken();
    fixture = TestBed.createComponent(AdminHome);
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    http.expectOne('/api/admins/tickets').flush([]);
  });

  afterEach(() => {
    http.verify();
    TestBed.inject(AuthService).clearToken();
  });

  it('greets the admin by name and says these are the customers they are responsible for onboarding', () => {
    http.expectOne('/api/admins/pending-customers').flush([]); // the page the suite opened before anyone signed in
    TestBed.inject(AuthService).saveSession('token', 'Admin', 'Dana');
    const greeted = TestBed.createComponent(AdminHome);
    greeted.detectChanges();
    http.expectOne('/api/admins/tickets').flush([]);
    http.expectOne('/api/admins/pending-customers').flush([]);
    greeted.detectChanges();
    const text = greeted.nativeElement as HTMLElement;

    expect(text.querySelector('#welcome')!.textContent!.trim()).toBe('Welcome, Dana!');
    expect(text.querySelector('p')!.textContent).toContain('Here are the remaining customers you are responsible for onboarding.');
  });

  it('still welcomes an admin whose sign-in has no name saved', () => {
    load([]);

    expect(root().querySelector('#welcome')!.textContent!.trim()).toBe('Welcome!');
  });

  it('says so when nobody is waiting', () => {
    load([]);

    expect(links().length).toBe(0);
    expect(root().querySelector('#pendingEmpty')).not.toBeNull();
  });

  it('shows one user who filled out the form, with their name and when they signed up, as a link to their form', () => {
    load([customer(5, 'Alma', 'First', '2026-10-30T12:00:00Z')]);

    expect(links().length).toBe(1);
    expect(links()[0].textContent).toContain('Alma First');
    expect(links()[0].querySelector('.when')!.textContent).toContain(shown('2026-10-30T12:00:00Z')!);
    expect(links()[0].getAttribute('href')).toBe('/admin/pending/5');
    expect(root().querySelector('#pendingEmpty')).toBeNull();
  });

  it('shows several users who filled out the form at different times, each with their own time, in the order the server sent', () => {
    load([
      customer(5, 'Alma', 'First', '2026-10-29T09:00:00Z'),
      customer(9, 'Bryn', 'Second', '2026-10-30T12:00:00Z', 'Ann'),
      customer(2, 'Cleo', 'Third', '2026-10-30T20:15:00Z'),
    ]);

    expect(links().map((l) => l.getAttribute('href'))).toEqual(['/admin/pending/5', '/admin/pending/9', '/admin/pending/2']);
    expect(links().map((l) => l.textContent)).toEqual([
      jasmine.stringMatching(/Alma First/),
      jasmine.stringMatching(/Bryn Ann Second/),
      jasmine.stringMatching(/Cleo Third/),
    ]);
    const times = links().map((l) => l.querySelector('.when')!.textContent!.trim());
    expect(new Set(times).size).toBe(3);
    expect(times[0]).toContain(shown('2026-10-29T09:00:00Z')!);
    expect(times[1]).toContain(shown('2026-10-30T12:00:00Z')!);
    expect(times[2]).toContain(shown('2026-10-30T20:15:00Z')!);
  });

  it('goes to the customer\'s form when their link is clicked', async () => {
    load([customer(5, 'Alma', 'First', '2026-10-30T12:00:00Z')]);
    const navigate = spyOn(TestBed.inject(Router), 'navigateByUrl');

    links()[0].click();

    expect(navigate.calls.mostRecent().args[0].toString()).toBe('/admin/pending/5');
  });

  it('offers a way to enter a new customer', () => {
    load([]);

    expect(root().querySelector('#newCustomerButton')!.getAttribute('href')).toBe('/inputForm');
  });

  it('shows a message when the list cannot be loaded', () => {
    http.expectOne('/api/admins/pending-customers').flush('boom', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(root().querySelector('#pendingFailed')).not.toBeNull();
    expect(links().length).toBe(0);
  });

  it('sends an expired session to the admin login', () => {
    const navigate = spyOn(TestBed.inject(Router), 'navigate');

    http.expectOne('/api/admins/pending-customers').flush('no', { status: 401, statusText: 'Unauthorized' });

    expect(window.alert).toHaveBeenCalledWith('Your session has expired. Please log in again.');
    expect(navigate).toHaveBeenCalledWith(['/admin/login']);
  });
});

describe('Admin ticket list', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;

  const openQueue = async () => {
    await harness.navigateByUrl('/admin/home');
    http.expectOne('/api/admins/pending-customers').flush([]);
    harness.detectChanges();
  };

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()] });
    TestBed.inject(AuthService).clearToken();
    TestBed.inject(AuthService).saveSession('test-token', 'Admin');
    harness = await RouterTestingHarness.create();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    TestBed.inject(AuthService).clearToken();
  });

  it('loads the review queue and links to the selected ticket and customer form', async () => {
    await openQueue();
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
    await openQueue();
    expect(harness.routeNativeElement!.querySelector('.page .empty-state')).toBeNull();
    http.expectOne('/api/admins/tickets').flush([]);
    harness.detectChanges();
    expect(harness.routeNativeElement!.textContent).toContain('No tickets awaiting review');
  });

  it('opens the onboarding form through the new customer link', async () => {
    await openQueue();
    http.expectOne('/api/admins/tickets').flush([]);
    harness.detectChanges();
    harness.routeNativeElement!.querySelector<HTMLAnchorElement>('#newCustomerButton')!.click();
    await harness.fixture.whenStable();
    expect(TestBed.inject(Router).url).toBe('/inputForm');
  });

  it('logs out through the admin home button', async () => {
    await openQueue();
    http.expectOne('/api/admins/tickets').flush([]);
    harness.detectChanges();
    harness.routeNativeElement!.querySelector<HTMLButtonElement>('#logoutButton')!.click();
    http.expectOne('/api/admins/logout').flush({ message: 'Logged out' });
    await harness.fixture.whenStable();
    expect(TestBed.inject(Router).url).toBe('/');
    expect(TestBed.inject(AuthService).token).toBeNull();
  });

  it('combines name or ID search with risk filtering and can clear no matches', async () => {
    await openQueue();
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
    expect(root.textContent).not.toContain('No tickets awaiting review');
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
    await openQueue();
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
    await openQueue();
    http.expectOne('/api/admins/tickets').flush('Unavailable', { status: 502, statusText: 'Bad Gateway' });
    harness.detectChanges();
    expect(harness.routeNativeElement!.querySelector('[role=alert]')!.textContent).toContain('Unable to load tickets');
    harness.routeNativeElement!.querySelector<HTMLButtonElement>('.page > button')!.click();
    http.expectOne('/api/admins/tickets').flush([]);
    harness.detectChanges();
    expect(harness.routeNativeElement!.textContent).toContain('No tickets awaiting review');
  });

  it('sends an expired ticket session to login with an explanation in navigation state', async () => {
    await openQueue();
    const navigate = spyOn(TestBed.inject(Router), 'navigate');
    http.expectOne('/api/admins/tickets').flush('Expired', { status: 401, statusText: 'Unauthorized' });
    expect(navigate).toHaveBeenCalledWith(['/admin/login'], {
      state: { errorMessage: 'Your session has expired. Please log in again.' },
    });
  });

  it('keeps users and signed-out visitors out of the queue', async () => {
    TestBed.inject(AuthService).saveSession('user-token', 'User');
    await harness.navigateByUrl('/admin/home');
    expect(TestBed.inject(Router).url).toBe('/user/home');
    http.expectOne('/api/users/me').flush({ username: 'customer' });
    http.expectNone('/api/admins/tickets');
    http.expectNone('/api/admins/pending-customers');
    TestBed.inject(AuthService).clearToken();
    await harness.navigateByUrl('/admin/home');
    expect(TestBed.inject(Router).url).toBe('/admin/login');
    http.expectNone('/api/admins/tickets');
    http.expectNone('/api/admins/pending-customers');
  });
});