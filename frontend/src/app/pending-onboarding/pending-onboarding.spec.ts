import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../app';
import { AuthService } from '../auth/auth.service';

describe('PendingOnboarding', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;

  const details = {
    customerId: 5, firstName: 'Alma', middleName: null, lastName: 'First', dateOfBirth: '1979-08-03', gender: 'Female',
    taxIdType: 'ITIN', taxIdLastFourDigits: '6789', residentialAddress: '9 Elm Street, Albany, NY, 12207',
    countryOfResidence: 'CA', countryOfCitizenship: 'MX', occupation: 'Nurse', employer: 'General Hospital',
  };

  const root = (): HTMLElement => harness.routeNativeElement!;
  const input = (selector: string) => root().querySelector<HTMLInputElement | HTMLSelectElement>(selector)!;
  const settle = async () => {
    await harness.fixture.whenStable();
    harness.detectChanges();
  };
  const choose = async (selector: string, value: string) => {
    const element = input(selector);
    element.value = value;
    element.dispatchEvent(new Event(element instanceof HTMLSelectElement ? 'change' : 'input'));
    await settle();
  };

  const open = async (body: object | string = details, status = 200) => {
    await harness.navigateByUrl('/admin/pending/5');
    const request = http.expectOne('/api/admins/pending-customers/5');
    if (status === 200) {
      request.flush(body);
    } else {
      request.flush(body, { status, statusText: 'Error' });
    }
    await settle();
  };

  beforeEach(async () => {
    spyOn(window, 'alert');
    TestBed.configureTestingModule({ providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()] });
    TestBed.inject(AuthService).saveSession('token', 'Admin');
    harness = await RouterTestingHarness.create();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    TestBed.inject(AuthService).clearToken();
  });

  it('pre-fills the form with what the customer entered', async () => {
    await open();

    expect(input('#field-firstName').value).toBe('Alma');
    expect(input('#field-lastName').value).toBe('First');
    expect(input('#field-dateOfBirth').value).toBe('1979-08-03');
    expect(input('#field-gender').value).toBe('Female');
    expect(input('#field-taxIdType').value).toBe('ITIN');
    expect(input('#field-residentialAddress').value).toBe('9 Elm Street, Albany, NY, 12207');
    expect(input('#field-countryOfResidence').value).toBe('Canada');
    expect(input('#field-countryOfCitizenship').value).toBe('Mexico');
    expect(input('#field-occupation').value).toBe('Nurse');
    expect(input('#field-employer').value).toBe('General Hospital');
  });

  it('shows only the last four digits of the tax ID, because the full number was never stored', async () => {
    await open();

    expect(input('#field-taxId').value).toBe('•••••6789');
  });

  it('leaves the middle name empty when the customer gave none', async () => {
    await open({ ...details, middleName: null });

    expect(input('#field-middleName').value).toBe('');
  });

  it('fills the middle name when the customer gave one', async () => {
    await open({ ...details, middleName: 'Lee' });

    expect(input('#field-middleName').value).toBe('Lee');
  });

  it('shows the customer\'s details read-only, so nothing the customer entered can be changed here', async () => {
    await open();

    const shown = Array.from(root().querySelectorAll<HTMLInputElement>('input[id^="field-"]'));
    expect(shown.length).toBe(12);
    expect(shown.every((i) => i.readOnly)).toBeTrue();
  });

  it('shows no login details, since the customer already has their own', async () => {
    await open();

    expect(root().querySelector('#CustomerUsername')).toBeNull();
    expect(root().querySelector('#CustomerPassword')).toBeNull();
    expect(root().querySelector('#recoveryQuestion')).toBeNull();
  });

  it('leaves the account type blank and will not submit until one is chosen', async () => {
    await open();

    expect(input('#typeOfAccount').value).toBe('');
    expect(input('#openTicketButton').hasAttribute('disabled')).toBeTrue();

    await choose('#typeOfAccount', 'Individual');
    expect(input('#openTicketButton').hasAttribute('disabled')).toBeFalse();
  });

  it('sends only the account type for an individual account, never the customer\'s details', async () => {
    await open();
    await choose('#typeOfAccount', 'Individual');

    input('#openTicketButton').click();
    await settle();

    const request = http.expectOne('/api/admins/pending-customers/5/submit');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ typeOfAccount: 'Individual', jointCustomerId: null });
    request.flush({ message: 'Customer saved', customerId: 5, ticketId: 41 });
    await settle();

    expect(TestBed.inject(Router).url).toBe('/review/41');
    http.expectOne('/api/admins/tickets/41').flush({});
  });

  it('asks for the second holder\'s ID on a joint account, empty at first, and sends it', async () => {
    await open();
    await choose('#typeOfAccount', 'Joint');

    expect(input('#jointCustomerId').value).toBe('');
    expect(input('#openTicketButton').hasAttribute('disabled')).toBeTrue();

    await choose('#jointCustomerId', '12');
    input('#openTicketButton').click();
    await settle();

    const request = http.expectOne('/api/admins/pending-customers/5/submit');
    expect(request.request.body).toEqual({ typeOfAccount: 'Joint', jointCustomerId: 12 });
    request.flush({ ticketId: 41 });
    await settle();
    http.expectOne('/api/admins/tickets/41').flush({});
  });

  it('shows the server\'s message and stays on the form when the ticket cannot be opened', async () => {
    await open();
    await choose('#typeOfAccount', 'Joint');
    await choose('#jointCustomerId', '999');

    input('#openTicketButton').click();
    await settle();
    http.expectOne('/api/admins/pending-customers/5/submit').flush('The joint customer does not exist.', { status: 400, statusText: 'Bad Request' });
    await settle();

    expect(root().querySelector('#pendingSubmitError')!.textContent).toContain('The joint customer does not exist.');
    expect(TestBed.inject(Router).url).toBe('/admin/pending/5');
    expect(input('#openTicketButton').hasAttribute('disabled')).toBeFalse(); // and they can fix it and try again
  });

  it('says so when another admin already opened this customer', async () => {
    await open('That customer has already been onboarded.', 409);

    expect(root().querySelector('#pendingLoadError')!.textContent).toContain('already been onboarded');
    expect(root().querySelector('#backToList')!.getAttribute('href')).toBe('/admin/home');
    expect(root().querySelector('#typeOfAccount')).toBeNull();
  });

  it('says so when the customer does not exist', async () => {
    await open('That customer does not exist.', 404);

    expect(root().querySelector('#pendingLoadError')!.textContent).toContain('does not exist');
  });
});
