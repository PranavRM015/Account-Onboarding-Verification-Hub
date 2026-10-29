import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Home } from './home';

describe('Home', () => {
  it('offers a login and a signup for users and for admins', async () => {
    await TestBed.configureTestingModule({ imports: [Home], providers: [provideRouter([])] }).compileComponents();
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();
    const href = (id: string) => fixture.nativeElement.querySelector(id).getAttribute('href');

    expect(href('#userLoginButton')).toBe('/user/login');
    expect(href('#userSignupButton')).toBe('/user/signup');
    expect(href('#adminLoginButton')).toBe('/admin/login');
    expect(href('#adminSignupButton')).toBe('/admin/signup');
  });
});
