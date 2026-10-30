import { DatePipe } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
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
    fixture = TestBed.createComponent(AdminHome);
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
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
