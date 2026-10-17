import { ComponentFixture, TestBed} from '@angular/core/testing';
import { Login } from '../login';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import {
  Credential,
  blankCredentials,
  blankUsernamePwdViolation1,
  blankUsernamePwdViolation2,
  blankUsernamePwdViolation3,
  blankUsernamePwdViolation4,
  blankUsernamePwdViolation5,
  shortUsernamePwdViolation1,
  shortUsernamePwdViolation2,
  shortUsernamePwdViolation3,
  shortUsernamePwdViolation4,
  shortUsernamePwdViolation5,
  validUsernamePwdViolation1,
  validUsernamePwdViolation2,
  validUsernamePwdViolation3,
  validUsernamePwdViolation4,
  validUsernamePwdViolation5,
  longUsernamePwdViolation1,
  longUsernamePwdViolation2,
  longUsernamePwdViolation3,
  longUsernamePwdViolation4,
  longUsernamePwdViolation5,
  validUsernamePwd,
} from '../../credentials/blankCredentials';

interface Scenario {
    description: string;
    payload: Credential;
    usernameError: 'required' | 'minlength' | 'maxlength' | null;
    passwordError: 'required' | 'minlength' | 'pattern' | null;
}

describe('LoginComponent', () => {
  let component: Login;
  let fixture: ComponentFixture<Login>;

  beforeEach(async () => {
    // 1. Configure the testing module
    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();  // Compiles external templates/styles
  });
  // 2. Create a fresh instance for the test
  beforeEach(() => {
    fixture = TestBed.createComponent( Login );
    component = fixture.componentInstance;
    fixture.detectChanges();
  });
  // 3. Note each type of violation
  const violationScenarios: Scenario[] = [
    // Blank username: always 'required'
    {
      description: 'both username and password are completely blank',
      payload: blankCredentials,
      usernameError: 'required', passwordError: 'required',
    },
    {
      description: 'username is blank and password is too short (Violation 1)',
      payload: blankUsernamePwdViolation1,
      usernameError: 'required', passwordError: 'minlength',
    },
    {
      description: 'username is blank and password has no uppercase letter (Violation 2)',
      payload: blankUsernamePwdViolation2,
      usernameError: 'required', passwordError: 'pattern',
    },
    {
      description: 'username is blank and password has no lowercase letter (Violation 3)',
      payload: blankUsernamePwdViolation3,
      usernameError: 'required', passwordError: 'pattern',
    },
    {
      description: 'username is blank and password has no digit (Violation 4)',
      payload: blankUsernamePwdViolation4,
      usernameError: 'required', passwordError: 'pattern',
    },
    {
      description: 'username is blank and password has no special character (Violation 5)',
      payload: blankUsernamePwdViolation5,
      usernameError: 'required', passwordError: 'pattern',
    },

    // 'abc' is exactly the 3-character minimum, so the username itself is valid
    {
      description: 'username is at the minimum length and password is too short (Violation 1)',
      payload: shortUsernamePwdViolation1,
      usernameError: null, passwordError: 'minlength',
    },
    {
      description: 'username is at the minimum length and password has no uppercase letter (Violation 2)',
      payload: shortUsernamePwdViolation2,
      usernameError: null, passwordError: 'pattern',
    },
    {
      description: 'username is at the minimum length and password has no lowercase letter (Violation 3)',
      payload: shortUsernamePwdViolation3,
      usernameError: null, passwordError: 'pattern',
    },
    {
      description: 'username is at the minimum length and password has no digit (Violation 4)',
      payload: shortUsernamePwdViolation4,
      usernameError: null, passwordError: 'pattern',
    },
    {
      description: 'username is at the minimum length and password has no special character (Violation 5)',
      payload: shortUsernamePwdViolation5,
      usernameError: null, passwordError: 'pattern',
    },

    // Valid username, bad password
    {
      description: 'username is valid and password is too short (Violation 1)',
      payload: validUsernamePwdViolation1,
      usernameError: null, passwordError: 'minlength',
    },
    {
      description: 'username is valid and password has no uppercase letter (Violation 2)',
      payload: validUsernamePwdViolation2,
      usernameError: null, passwordError: 'pattern',
    },
    {
      description: 'username is valid and password has no lowercase letter (Violation 3)',
      payload: validUsernamePwdViolation3,
      usernameError: null, passwordError: 'pattern',
    },
    {
      description: 'username is valid and password has no digit (Violation 4)',
      payload: validUsernamePwdViolation4,
      usernameError: null, passwordError: 'pattern',
    },
    {
      description: 'username is valid and password has no special character (Violation 5)',
      payload: validUsernamePwdViolation5,
      usernameError: null, passwordError: 'pattern',
    },

    // Username over 40 characters: 'maxlength'
    {
      description: 'username is too long and password is too short (Violation 1)',
      payload: longUsernamePwdViolation1,
      usernameError: 'maxlength', passwordError: 'minlength',
    },
    {
      description: 'username is too long and password has no uppercase letter (Violation 2)',
      payload: longUsernamePwdViolation2,
      usernameError: 'maxlength', passwordError: 'pattern',
    },
    {
      description: 'username is too long and password has no lowercase letter (Violation 3)',
      payload: longUsernamePwdViolation3,
      usernameError: 'maxlength', passwordError: 'pattern',
    },
    {
      description: 'username is too long and password has no digit (Violation 4)',
      payload: longUsernamePwdViolation4,
      usernameError: 'maxlength', passwordError: 'pattern',
    },
    {
      description: 'username is too long and password has no special character (Violation 5)',
      payload: longUsernamePwdViolation5,
      usernameError: 'maxlength', passwordError: 'pattern',
    },
  ];
  const successScenario = [
    {
      description: 'both username and password follow the expected format',
      payload: validUsernamePwd
    }
  ]
  violationScenarios.forEach(( {description, payload, usernameError, passwordError}) => {
    it(`should flag the expected errors when ${description}`, () => {
      component.username.setValue(payload.username);
      component.password.setValue(payload.password);
      fixture.detectChanges();

      if (usernameError) {
        expect(component.username.hasError(usernameError)).toBeTrue();
      } else {
        expect(component.username.valid).toBeTrue();
      }
      if (passwordError) {
        expect(component.password.hasError(passwordError)).toBeTrue();
      } else {
        expect(component.password.valid).toBeTrue();
      }
    });
  });
  
  successScenario.forEach(( {description, payload}) => {
    it(`should mark both fields as valid when ${description}`, () => {
      component.username.setValue(payload.username);
      component.password.setValue(payload.password);
      fixture.detectChanges();

      expect(component.username.valid).toBeTrue();
      expect(component.password.valid).toBeTrue();
    });
  });
});
