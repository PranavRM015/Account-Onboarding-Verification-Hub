import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { Home } from './home';

describe('Home', () => {
  it('offers only user and admin entry points on the front page', async () => {
    await TestBed.configureTestingModule({ imports: [Home], providers: [provideRouter([])] }).compileComponents();
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();
    const href = (id: string) => fixture.nativeElement.querySelector(id).getAttribute('href');

    expect(href('#usersButton')).toBe('/user');
    expect(href('#adminsButton')).toBe('/admin');
    expect(fixture.nativeElement.querySelectorAll('a').length).toBe(2);
  });

  for (const role of ['user', 'admin']) {
    it(`offers matching login and signup links in the ${role} area`, async () => {
      await TestBed.configureTestingModule({
        imports: [Home],
        providers: [provideRouter([]), { provide: ActivatedRoute, useValue: { snapshot: { data: { role } } } }],
      }).compileComponents();
      const fixture = TestBed.createComponent(Home);
      fixture.detectChanges();
      const href = (id: string) => fixture.nativeElement.querySelector(id).getAttribute('href');

      expect(href(`#${role}LoginButton`)).toBe(`/${role}/login`);
      expect(href(`#${role}SignupButton`)).toBe(`/${role}/signup`);
      expect(href('.back-link')).toBe('/');
    });
  }
});
