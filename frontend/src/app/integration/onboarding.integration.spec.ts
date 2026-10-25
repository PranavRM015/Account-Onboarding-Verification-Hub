import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../app';
import { flagLabel, TicketDetails } from '../ticket/ticketDetails';

interface Scenario {
  name: string;
  lastName?: string;
  middleName?: string;
  firstName?: string;
  residence: string;
  citizenship: string;
  occupation: string;
  employer: string;
  flags: string[];
  rating: 'Low' | 'Medium' | 'High';
}

const keywords = ['cannabis', 'marijuana', 'dispensary', 'hemp', 'thc', 'cbd'];

const base = { residence: 'US', citizenship: 'US', occupation: 'Teacher', employer: 'City Public School' };

const scenarios: Scenario[] = [
  { ...base, name: 'clean customer', flags: [], rating: 'Low' },
  { ...base, name: 'OFAC match', firstName: 'James', middleName: 'Robert', lastName: 'Anderson', flags: ['OfacMatch'], rating: 'High' },
  { ...base, name: 'citizen of IN living in US', citizenship: 'IN', residence: 'US', flags: ['ClosedJurisdiction'], rating: 'Medium' },
  { ...base, name: 'citizen of IN living in MX', citizenship: 'IN', residence: 'MX', flags: ['ClosedJurisdiction'], rating: 'Medium' },
  { ...base, name: 'citizen of IN living in GB', citizenship: 'IN', residence: 'GB', flags: ['ClosedJurisdiction'], rating: 'Medium' },
  { ...base, name: 'citizen of US living in GB', citizenship: 'US', residence: 'GB', flags: ['ClosedJurisdiction'], rating: 'Medium' },
  ...keywords.map((keyword): Scenario => ({
    ...base,
    name: `occupation contains ${keyword}`,
    occupation: `Senior ${keyword} specialist`,
    flags: ['ProhibitedIndustry'],
    rating: 'High',
  })),
  ...keywords.map((keyword): Scenario => ({
    ...base,
    name: `employer contains ${keyword}`,
    employer: `Northern ${keyword} Holdings`,
    flags: ['ProhibitedIndustry'],
    rating: 'High',
  })),
];

const credentials = { username: 'reviewer_one', password: 'Str0ng!Passw0rd#2026' };
const address = '1633 Main Street, Springfield, IL, 62701';
const ticketId = 41;
const customerId = 17;

type Field = HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement;

function ticketFor(
  s: Scenario,
  status: string,
  clearedCount: number,
  firstApprovedBy: string | null = null,
  decision: { type: string; by: string; reason: string } | null = null,
): TicketDetails {
  return {
    ticketId,
    customerId,
    ticketStatus: status,
    riskRating: s.rating,
    accountType: 'Individual',
    createdAt: '2026-10-24T10:00:00Z',
    verificationDeadline: '2026-12-23T10:00:00Z',
    assignedTo: credentials.username,
    firstApprovedBy,
    verified: s.flags.length === 0,
    customer: {
      firstName: s.firstName ?? 'Casey',
      middleName: s.middleName ?? null,
      lastName: s.lastName ?? 'Testcase',
      dateOfBirth: '1985-04-12T00:00:00',
      gender: 'Other',
      taxIdType: 'SSN',
      residentialAddress: address,
      countryOfResidence: s.residence,
      countryOfCitizenship: s.citizenship,
      occupation: s.occupation,
      employer: s.employer,
    },
    jointCustomerName: null,
    flags: s.flags.map((violationType, i) => ({
      flagId: i + 1,
      violationType,
      raisedAt: '2026-10-24T10:00:01Z',
      cleared: i < clearedCount,
      clearedAt: i < clearedCount ? '2026-10-24T10:05:00Z' : null,
      clearedBy: i < clearedCount ? credentials.username : null,
      clearedReason: i < clearedCount ? 'Reviewed the documents.' : null,
    })),
    vendorResponse: {
      nameMatch: 'Match',
      dateOfBirthMatch: 'Match',
      addressMatch: 'Match',
      taxIdMatch: 'Match',
      score: 50,
      riskCodes: '',
      vrTimeStamp: '2026-10-24T10:00:01Z',
    },
    decision: decision && {
      decisionType: decision.type,
      decidedBy: decision.by,
      reason: decision.reason,
      decidedAt: '2026-10-24T10:10:00Z',
    },
  };
}

describe('Onboarding integration with mocked HTTP', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;

  const settle = async () => {
    await harness.fixture.whenStable();
    harness.detectChanges();
  };

  const field = (selector: string): Field => {
    const element = harness.routeNativeElement?.querySelector<Field>(selector);
    if (!element) {
      throw new Error(`Could not find ${selector} on the page.`);
    }
    return element;
  };

  const enter = (selector: string, value: string) => {
    const element = field(selector);
    element.value = value;
    element.dispatchEvent(new Event(element instanceof HTMLSelectElement ? 'change' : 'input'));
  };

  const clickButton = async (label: string, scope = '') => {
    const buttons = Array.from(harness.routeNativeElement!.querySelectorAll<HTMLButtonElement>(`${scope} button`.trim()));
    const button = buttons.find((b) => b.textContent?.trim() === label);
    if (!button) {
      throw new Error(`Could not find a "${label}" button.`);
    }
    button.click();
    await settle();
  };

  const text = () => harness.routeNativeElement?.textContent ?? '';

  const signUpAndLogIn = async () => {
    await harness.navigateByUrl('/signup');
    enter('#Username', credentials.username);
    enter('#Password', credentials.password);
    await clickButton('Sign up');
    const signup = http.expectOne('/api/admins/signup');
    expect(signup.request.body).toEqual(credentials);
    signup.flush('created');
    await settle();

    await harness.navigateByUrl('/login');
    enter('#Username', credentials.username);
    enter('#Password', credentials.password);
    await clickButton('Log in');
    const login = http.expectOne('/api/admins/login');
    expect(login.request.body).toEqual(credentials);
    login.flush('ok');
    await settle();
    expect(TestBed.inject(Router).url).toBe('/inputForm');
  };

  const fillAndSubmitForm = async (s: Scenario) => {
    enter('#FirstName', s.firstName ?? 'Casey');
    enter('#MiddleName', s.middleName ?? '');
    enter('#LastName', s.lastName ?? 'Testcase');
    enter('#DateOfBirth', '1985-04-12');
    field('#other').click();
    enter('#taxIdType', 'SSN');
    enter('#typeOfAccount', 'Individual');
    enter('#taxIdNumber', '123-45-6789');
    enter('#residentialAddress', address);
    enter('#countryOfResidence', s.residence);
    enter('#countryOfCitizenship', s.citizenship);
    enter('#occupation', s.occupation);
    enter('#Employer', s.employer);
    await settle();
    expect(field('button[type=submit]').hasAttribute('disabled')).toBeFalse();
    await clickButton('Submit');

    const submit = http.expectOne('/api/admins/inputForm');
    expect(submit.request.method).toBe('POST');
    expect(submit.request.body).toEqual(jasmine.objectContaining({
      FirstName: s.firstName ?? 'Casey',
      LastName: s.lastName ?? 'Testcase',
      taxIdType: 'SSN',
      taxIdNumber: '123-45-6789',
      countryOfResidence: s.residence,
      countryOfCitizenship: s.citizenship,
      occupation: s.occupation,
      Employer: s.employer,
      typeOfAccount: 'Individual',
      jointCustomerId: null,
    }));
    submit.flush({ message: 'Customer saved', customerId, ticketId, riskRating: s.rating, flags: s.flags });
    await settle();
    expect(TestBed.inject(Router).url).toBe(`/review/${ticketId}`);
    http.expectOne(`/api/admins/tickets/${ticketId}`).flush(ticketFor(s, 'Review', 0));
    await settle();
  };

  const confirmAction = async (url: string, reason: string, message: string) => {
    enter('#reason', reason);
    await settle();
    await clickButton('Confirm', '#reasonModal');
    const request = http.expectOne(url);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ reason });
    request.flush({ message });
    await settle();
  };

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    });
    spyOn(window, 'alert');
    harness = await RouterTestingHarness.create();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  for (const scenario of scenarios) {
    describe(scenario.name, () => {
      it('shows the flags and the risk rating on the review page', async () => {
        await signUpAndLogIn();
        await fillAndSubmitForm(scenario);

        expect(text()).toContain(scenario.rating);
        for (const flag of scenario.flags) {
          expect(text()).toContain(flagLabel(flag));
        }
        if (scenario.flags.length === 0) {
          expect(text()).toContain('No flags were raised.');
        } else {
          expect(text()).toContain('Clear every flag before approving.');
          expect(field('.actions button').hasAttribute('disabled')).toBeTrue();
        }
      });

      it('can be accepted after every flag is cleared', async () => {
        await signUpAndLogIn();
        await fillAndSubmitForm(scenario);

        for (let i = 0; i < scenario.flags.length; i++) {
          await clickButton('Clear', '.flag');
          await confirmAction(
            `/api/admins/tickets/${ticketId}/flags/${i + 1}/clear`,
            'Reviewed the customer documents and cleared the flag.',
            'Flag cleared',
          );
          http.expectOne(`/api/admins/tickets/${ticketId}`).flush(ticketFor(scenario, 'Review', i + 1));
          await settle();
          expect(text()).toContain('Flag cleared');
        }

        const approveUrl = `/api/admins/tickets/${ticketId}/approve`;
        expect(field('.actions button').hasAttribute('disabled')).toBeFalse();
        await clickButton('Approve', '.actions');
        if (scenario.rating === 'High') {
          await confirmAction(approveUrl, 'First reviewer is satisfied.', 'First approval recorded.');
          http.expectOne(`/api/admins/tickets/${ticketId}`)
            .flush(ticketFor(scenario, 'Review', scenario.flags.length, credentials.username));
          await settle();
          expect(text()).toContain('A different reviewer must give the second approval.');
          await clickButton('Approve', '.actions');
        }
        await confirmAction(approveUrl, 'All checks passed.', 'Ticket approved');
        const approved = ticketFor(scenario, 'Approved', scenario.flags.length,
          scenario.rating === 'High' ? credentials.username : null,
          { type: 'Approve', by: credentials.username, reason: 'All checks passed.' });
        http.expectOne(`/api/admins/tickets/${ticketId}`).flush(approved);
        await settle();
        expect(TestBed.inject(Router).url).toBe(`/result/${ticketId}`);
        http.expectOne(`/api/admins/tickets/${ticketId}`).flush(approved);
        await settle();

        expect(text()).toContain('This customer has been accepted.');
        expect(text()).toContain('All checks passed.');
        for (const flag of scenario.flags) {
          expect(text()).toContain(flagLabel(flag));
        }
        expect(text()).not.toContain('(not cleared)');
      });

      it('can be rejected without clearing the flags', async () => {
        await signUpAndLogIn();
        await fillAndSubmitForm(scenario);

        await clickButton('Reject', '.actions');
        await confirmAction(`/api/admins/tickets/${ticketId}/reject`, 'Could not verify the customer.', 'Ticket rejected');
        const rejected = ticketFor(scenario, 'Rejected', 0, null,
          { type: 'Reject', by: credentials.username, reason: 'Could not verify the customer.' });
        http.expectOne(`/api/admins/tickets/${ticketId}`).flush(rejected);
        await settle();
        expect(TestBed.inject(Router).url).toBe(`/result/${ticketId}`);
        http.expectOne(`/api/admins/tickets/${ticketId}`).flush(rejected);
        await settle();

        expect(text()).toContain('Rejected by');
        expect(text()).toContain('Could not verify the customer.');
        expect(text()).not.toContain('This customer has been accepted.');
        if (scenario.flags.length > 0) {
          expect(text()).toContain('(not cleared)');
        }
      });
    });
  }

  describe('signup and login', () => {
    it('does not send a weak password to the server', async () => {
      await harness.navigateByUrl('/signup');
      enter('#Username', credentials.username);
      enter('#Password', 'password');
      await clickButton('Sign up');
      http.expectNone('/api/admins/signup');
    });

    it('tells the admin when the username is already taken', async () => {
      await harness.navigateByUrl('/signup');
      enter('#Username', credentials.username);
      enter('#Password', credentials.password);
      await clickButton('Sign up');
      http.expectOne('/api/admins/signup').flush('taken', { status: 409, statusText: 'Conflict' });
      await settle();
      expect(window.alert).toHaveBeenCalledWith('That username is already taken.');
    });

    it('stays on the login page when the password is wrong', async () => {
      await harness.navigateByUrl('/login');
      enter('#Username', credentials.username);
      enter('#Password', credentials.password);
      await clickButton('Log in');
      http.expectOne('/api/admins/login').flush('no', { status: 401, statusText: 'Unauthorized' });
      await settle();
      expect(window.alert).toHaveBeenCalledWith('Invalid username or password.');
      expect(TestBed.inject(Router).url).toBe('/login');
    });
  });
});
