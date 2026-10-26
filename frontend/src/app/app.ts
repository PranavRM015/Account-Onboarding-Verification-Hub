import { Component, signal } from '@angular/core';
import { RouterOutlet, Routes } from '@angular/router';
import { Home } from './home/home';
import { Signup } from './signup/signup';
import { Login } from './login/login';
import { Submit } from './submit/submit';
import { UserFormComponent } from './inputForm/inputForm';
import { Review } from './review/review';
import { Result } from './result/result';
import { authGuard } from './auth/auth.guard';

export const routes: Routes = [
  { path: '', component: Home },
  { path: 'signup', component: Signup },
  { path: 'login', component: Login },
  { path: 'submit', component: Submit },
  { path: 'inputForm', component: UserFormComponent, canActivate: [authGuard] },
  { path: 'review/:ticketId', component: Review, canActivate: [authGuard] },
  { path: 'result/:ticketId', component: Result, canActivate: [authGuard] },
];

@Component({
  imports: [RouterOutlet],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  protected readonly title = signal('frontend');
}
