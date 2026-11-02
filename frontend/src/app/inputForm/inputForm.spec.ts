import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { UserFormComponent } from './inputForm';

describe('UserFormComponent', () => {
  let component: UserFormComponent;
  let fixture: ComponentFixture<UserFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UserFormComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(UserFormComponent);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('reveals missing fields on submit and validates the joint customer only when needed', async () => {
    fixture.detectChanges();
    const submit: HTMLButtonElement = fixture.nativeElement.querySelector('button[type=submit]');
    expect(submit.disabled).toBeFalse();
    submit.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#FirstNameError').textContent).toContain('First name is required.');
    expect(fixture.nativeElement.querySelector('#GenderError').textContent).toContain('Gender is required.');
    expect(fixture.nativeElement.querySelector('#jointCustomerIdError')).toBeNull();

    component.typeOfAccount.setValue('Joint');
    component.onSubmit();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#jointCustomerIdError').textContent).toContain('Joint customer ID is required.');

    const jointInput: HTMLInputElement = fixture.nativeElement.querySelector('#jointCustomerId');
    jointInput.value = '1.5';
    jointInput.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#jointCustomerIdError').textContent).toContain('positive whole-number');
    component.typeOfAccount.setValue('Individual');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#jointCustomerIdError')).toBeNull();
  });
});
