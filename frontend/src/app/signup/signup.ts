import { Component, Input, OnChanges, SimpleChanges, inject  } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RedirectCommand } from '@angular/router';
import { viewChild, ElementRef } from '@angular/core';

@Component({
  imports: [],
  selector: 'app-signup',
  styleUrl: './signup.css',
  templateUrl: './signup.html',
})
export class Signup {
  isModalVisible = false;
  @Input() username: string = '';
  @Input() password: string = '';
  private http = inject(HttpClient);
  
  showModal(): void {
    this.isModalVisible = true;
  }
  hideModal(): void {
    this.isModalVisible = false;
  }
  checkPassword(password: string = this.password): boolean {
    
    if((this.password.length < 8) || !/[A-Z]/.test(this.password) || !/[a-z]/.test(this.password) || !/[0-9]/.test(this.password)) {
      return false;
    }
    // Implementation for password validation
    return true;
  }

  acceptAdminCredentials(username: string, password: string): void {
    const validPassword = this.checkPassword(this.password);
    if(!username || !password || !validPassword){
      this.isModalVisible = true;
      return;
    }
    if(username && password && validPassword){
      this.http.post('http://localhost:4200/api/users', { username, password }).subscribe(
        {next: (response) => {
          console.log('User created successfully:', response);
        },
        error: (error) =>{
          this.isModalVisible = true;
          console.error('Signup failed:', error);
        }
      });
    }
  }
}
