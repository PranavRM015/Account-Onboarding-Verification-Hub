import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Home } from './home';

describe('Home', () => {
  it('offers just a user button and an admin button', async () => {
    await TestBed.configureTestingModule({ imports: [Home], providers: [provideRouter([])] }).compileComponents();
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();
    const root: HTMLElement = fixture.nativeElement;
    const href = (id: string) => root.querySelector(id)!.getAttribute('href');

    const buttons = Array.from(root.querySelectorAll('a, button'));
    expect(buttons.map((b) => b.textContent!.trim())).toEqual(['User', 'Admin']);
    expect(href('#userButton')).toBe('/user');
    expect(href('#adminButton')).toBe('/admin');
  });
});
