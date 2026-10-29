import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { recoveryQuestions } from '../shared/form-rules';
import { UserSignup } from './user-signup';

describe('UserSignup', () => {
  let component: UserSignup;
  let fixture: ComponentFixture<UserSignup>;
  let http: HttpTestingController;

  const fillEverything = () => {
    component.username.setValue('casey_customer');
    component.password.setValue('Cust0mer!Passw0rd#1');
    component.firstName.setValue('Casey');
    component.lastName.setValue('Customer');
    component.dateOfBirth.setValue('1985-04-12');
    component.gender.setValue('Other');
    component.taxIdType.setValue('SSN');
    component.taxIdNumber.setValue('123-45-6789');
    component.residentialAddress.setValue('1633 Main Street, Springfield, IL, 62701');
    component.countryOfResidence.setValue('US');
    component.countryOfCitizenship.setValue('US');
    component.occupation.setValue('Teacher');
    component.employer.setValue('City Public School');
    component.passwordRecoveryQuestion.setValue(recoveryQuestions[0]);
    component.passwordRecoveryAnswer.setValue('Pizza');
  };

  beforeEach(async () => {
    spyOn(window, 'alert');
    await TestBed.configureTestingModule({
      imports: [UserSignup],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    fixture = TestBed.createComponent(UserSignup);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('offers the four recovery questions', () => {
    expect(component.questions).toEqual([
      'What is your favorite food?',
      'What was the name of the elementary school you went to?',
      'What is the name of your first pet?',
      'Where was your last vacation?',
    ]);
    const options = fixture.nativeElement.querySelectorAll('#RecoveryQuestion option');
    expect(options.length).toBe(5); // the placeholder plus the four questions
  });

  it('sends the whole form, including the recovery question and answer', () => {
    fillEverything();

    component.signUp();

    const request = http.expectOne('/api/users/signup');
    expect(request.request.body).toEqual(jasmine.objectContaining({
      username: 'casey_customer',
      passwordRecoveryQuestion: recoveryQuestions[0],
      passwordRecoveryAnswer: 'Pizza',
      taxIdNumber: '123-45-6789',
    }));
    request.flush('created');
  });

  it('does not send anything without a recovery question and answer', () => {
    fillEverything();
    component.passwordRecoveryQuestion.setValue('');
    component.signUp();
    component.passwordRecoveryQuestion.setValue(recoveryQuestions[0]);
    component.passwordRecoveryAnswer.setValue('   ');
    component.signUp();

    http.expectNone('/api/users/signup');
  });

  it('does not send a weak password or an under-age birth date', () => {
    fillEverything();
    component.password.setValue('password');
    component.signUp();
    component.password.setValue('Cust0mer!Passw0rd#1');
    component.dateOfBirth.setValue(`${new Date().getFullYear() - 5}-01-01`);
    component.signUp();

    http.expectNone('/api/users/signup');
    expect(component.dateOfBirth.hasError('underage')).toBeTrue();
  });

  it('rejects a tax ID that is not nine digits and an address in the wrong format', () => {
    component.taxIdNumber.setValue('12345');
    component.residentialAddress.setValue('somewhere');

    expect(component.taxIdNumber.hasError('taxIdFormat')).toBeTrue();
    expect(component.residentialAddress.hasError('pattern')).toBeTrue();
  });

  it('tells the person when the username is taken', () => {
    fillEverything();
    component.signUp();

    http.expectOne('/api/users/signup').flush('That username is already taken.', { status: 409, statusText: 'Conflict' });

    expect(window.alert).toHaveBeenCalledWith('That username is already taken.');
  });

  it('shows the username hint while the username box is focused and hides it afterwards', () => {
    const modal: HTMLElement = fixture.nativeElement.querySelector('#usernameModal');
    const input: HTMLInputElement = fixture.nativeElement.querySelector('#Username');

    input.dispatchEvent(new Event('focus'));
    fixture.detectChanges();
    expect(modal.classList.contains('show')).toBeTrue();

    input.dispatchEvent(new Event('blur'));
    fixture.detectChanges();
    expect(modal.classList.contains('show')).toBeFalse();
  });
});
