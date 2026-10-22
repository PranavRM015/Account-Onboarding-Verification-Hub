import { Component, signal } from '@angular/core';
import { RouterOutlet, Routes } from '@angular/router';
import { Home } from './home/home';
import { Signup } from './signup/signup';
import { Login } from './login/login';
import { Submit } from './submit/submit';
import { UserFormComponent } from './inputForm/inputForm';

export const routes: Routes = [
  { path: '', component: Home },
  { path: 'signup', component: Signup },
  { path: 'login', component: Login },
  { path: 'submit', component: Submit },
  { path: 'inputForm', component: UserFormComponent },
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
