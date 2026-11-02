import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { recoveryQuestions } from '../shared/form-rules';
import { Signup } from './signup';

describe('Admin signup profile fields', () => {
  let component: Signup;
  let fixture: ComponentFixture<Signup>;
  let http: HttpTestingController;

  const fillEverything = () => {
    component.username.setValue('reviewer_one');
    component.password.setValue('Str0ng!Passw0rd#2026');
    component.firstName.setValue('Alex');
    component.lastName.setValue('Reviewer');
    component.dateOfBirth.setValue('1990-05-20');
    component.gender.setValue('Other');
    component.ssn.setValue('123-45-6789');
    component.isCitizen.setValue(true);
    component.countryOfCitizenship.setValue('US');
    component.passwordRecoveryQuestion.setValue(recoveryQuestions[1]);
    component.passwordRecoveryAnswer.setValue('Lincoln Elementary');
  };

  beforeEach(async () => {
    spyOn(window, 'alert');
    await TestBed.configureTestingModule({
      imports: [Signup],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    fixture = TestBed.createComponent(Signup);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('sends the profile and the recovery question and answer', () => {
    fillEverything();

    component.acceptAdminCredentials();

    const request = http.expectOne('/api/admins/signup');
    expect(request.request.body).toEqual(jasmine.objectContaining({
      firstName: 'Alex',
      lastName: 'Reviewer',
      dateOfBirth: '1990-05-20',
      gender: 'Other',
      ssn: '123-45-6789',
      isCitizen: true,
      countryOfCitizenship: 'US',
      passwordRecoveryQuestion: recoveryQuestions[1],
      passwordRecoveryAnswer: 'Lincoln Elementary',
    }));
    request.flush('created');
  });

  it('never sends a hire date, because the system sets it', () => {
    fillEverything();

    component.acceptAdminCredentials();

    const request = http.expectOne('/api/admins/signup');
    expect(Object.keys(request.request.body)).not.toContain('dateHired');
    request.flush('created');
  });

  it('needs every required field before it sends anything', () => {
    for (const control of [
      component.firstName, component.lastName, component.dateOfBirth, component.gender, component.ssn,
      component.countryOfCitizenship, component.passwordRecoveryQuestion, component.passwordRecoveryAnswer,
    ]) {
      fillEverything();
      control.setValue('');
      component.acceptAdminCredentials();
    }

    http.expectNone('/api/admins/signup');
  });

  it('rejects an SSN that is not nine digits and a date of birth under 18', () => {
    component.ssn.setValue('1234');
    component.dateOfBirth.setValue(`${new Date().getFullYear() - 10}-01-01`);

    expect(component.ssn.hasError('taxIdFormat')).toBeTrue();
    expect(component.dateOfBirth.hasError('underage')).toBeTrue();
  });

  it('offers the four recovery questions in a drop down', () => {
    const options = fixture.nativeElement.querySelectorAll('#RecoveryQuestion option');

    expect(options.length).toBe(5); // the placeholder plus the four questions
    expect(options[1].textContent).toContain('favorite food');
  });

  it('shows connection feedback without clearing details or exposing the gateway response', () => {
    fillEverything();
    component.acceptAdminCredentials();
    http.expectOne('/api/admins/signup').flush('<html>Bad Gateway</html>', { status: 502, statusText: 'Bad Gateway' });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role=alert]').textContent)
      .toContain('Unable to connect to the server. Please try again shortly.');
    expect(component.username.value).toBe('reviewer_one');
    expect(component.passwordRecoveryAnswer.value).toBe('Lincoln Elementary');
    expect(component.submitting()).toBeFalse();
    expect(fixture.nativeElement.textContent).not.toContain('<html>');
  });

  it('shows required fields after submitting and clears a corrected field error', () => {
    expect(fixture.nativeElement.querySelector('#FirstNameError')).toBeNull();
    component.acceptAdminCredentials();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('#FirstNameError').textContent).toContain('First name is required.');
    expect(fixture.nativeElement.querySelector('#UsernameError').textContent).toContain('Username is required.');
    expect(fixture.nativeElement.querySelector('#FirstName').getAttribute('aria-invalid')).toBe('true');
    expect(fixture.nativeElement.querySelector('#FirstName').getAttribute('aria-describedby')).toBe('FirstNameError');
    expect(fixture.nativeElement.querySelector('#MiddleNameError')).toBeNull();
    http.expectNone('/api/admins/signup');

    component.firstName.setValue('Alex');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#FirstNameError')).toBeNull();
    expect(fixture.nativeElement.querySelector('#FirstName').getAttribute('aria-invalid')).toBe('false');
  });
});
