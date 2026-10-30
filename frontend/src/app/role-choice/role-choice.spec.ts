import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../app';

describe('RoleChoice', () => {
  const open = async (url: string) => {
    TestBed.configureTestingModule({ providers: [provideRouter(routes)] });
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url);
    const root: HTMLElement = harness.routeNativeElement!;
    return { title: root.querySelector('h1')!.textContent, href: (id: string) => root.querySelector(id)!.getAttribute('href') };
  };

  it('offers the user login and signup after picking User', async () => {
    const page = await open('/user');

    expect(page.title).toContain('User');
    expect(page.href('#loginButton')).toBe('/user/login');
    expect(page.href('#signupButton')).toBe('/user/signup');
  });

  it('offers the admin login and signup after picking Admin', async () => {
    const page = await open('/admin');

    expect(page.title).toContain('Admin');
    expect(page.href('#loginButton')).toBe('/admin/login');
    expect(page.href('#signupButton')).toBe('/admin/signup');
  });

  it('goes back to the front page', async () => {
    const page = await open('/admin');

    expect(page.href('#backButton')).toBe('/');
  });
});
