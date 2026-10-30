import { Component, signal } from '@angular/core';
import { RouterOutlet, Routes } from '@angular/router';
import { Home } from './home/home';
import { Signup } from './signup/signup';
import { Login } from './login/login';
import { Submit } from './submit/submit';
import { UserFormComponent } from './inputForm/inputForm';
import { Review } from './review/review';
import { Result } from './result/result';
import { UserSignup } from './user-signup/user-signup';
import { UserHome } from './user-home/user-home';
import { ForgotPassword } from './forgot-password/forgot-password';
import { RoleChoice } from './role-choice/role-choice';
import { AdminHome } from './admin-home/admin-home';
import { PendingOnboarding } from './pending-onboarding/pending-onboarding';
import { adminGuard, userGuard } from './auth/auth.guard';

export const routes: Routes = [
  { path: '', component: Home },
  { path: 'user', component: RoleChoice, data: { role: 'user' } },
  { path: 'admin', component: RoleChoice, data: { role: 'admin' } },
  { path: 'admin/home', component: AdminHome, canActivate: [adminGuard] },
  { path: 'admin/pending/:customerId', component: PendingOnboarding, canActivate: [adminGuard] },
  { path: 'admin/signup', component: Signup },
  { path: 'admin/login', component: Login, data: { role: 'admin' } },
  { path: 'user/signup', component: UserSignup },
  { path: 'user/login', component: Login, data: { role: 'user' } },
  { path: 'user/home', component: UserHome, canActivate: [userGuard] },
  { path: 'forgot-password/:role', component: ForgotPassword },
  { path: 'submit', component: Submit },
  { path: 'inputForm', component: UserFormComponent, canActivate: [adminGuard] },
  { path: 'review/:ticketId', component: Review, canActivate: [adminGuard] },
  { path: 'result/:ticketId', component: Result, canActivate: [adminGuard] },
  { path: '**', redirectTo: '' },
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
