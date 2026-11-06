import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

// The page after the front page: whichever of Customer or Admin was picked, it offers that role's login and signup.
// (The route and role id stay "user", matching the backend's /api/users; only what people read says "Customer".)
@Component({
  imports: [RouterLink],
  selector: 'app-role-choice',
  styleUrl: '../home/home.css',
  templateUrl: './role-choice.html',
})
export class RoleChoice {
  readonly role: 'user' | 'admin' = inject(ActivatedRoute).snapshot.data['role'] === 'admin' ? 'admin' : 'user';
  readonly title = this.role === 'admin' ? 'Admin' : 'Customer';
  readonly blurb = this.role === 'admin'
    ? 'Staff who review and approve onboarding.'
    : 'Customers opening an account.';
}
