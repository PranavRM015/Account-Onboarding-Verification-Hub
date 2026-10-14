import { Component, Input, OnChanges, SimpleChanges, inject  } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RedirectCommand } from '@angular/router';
import { viewChild, ElementRef } from '@angular/core';
import { FormControl, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';

@Component({
  imports: [FormsModule, ReactiveFormsModule],
  selector: 'app-signup',
  styleUrl: './signup.css',
  templateUrl: './signup.html',
})
export class Signup {
  isModalVisible = false;
  protected username = new FormControl<string>('', { validators: [Validators.required], nonNullable: true });
  protected password: string = '';
  private http = inject(HttpClient);
  
  showModal(): void {
    this.isModalVisible = true;
  }
  hideModal(): void {
    this.isModalVisible = false;
  }
  checkPassword(password: string = this.password): boolean {
    return (
      password.length >= 8 &&
      /[A-Z]/.test(password) &&
      /[a-z]/.test(password) &&
      /[0-9]/.test(password) &&
      /[^A-Za-z0-9]/.test(password)
    );
  }
  acceptAdminCredentials(username: string, password: string): void {
    console.log('Form submitted!');
    console.log('Username:', username);
    console.log('Password entered:', password.length > 0);

    const validPassword = this.checkPassword(this.password);
    if(!username.trim() || !password || !validPassword){
      this.isModalVisible = true;
      return;
    }
    const requestBody = { username: username.trim(), password };
    console.log('Submitting signup request:', {
      username: requestBody.username,
      passwordPresent: !!requestBody.password
    });

    this.http.post('/api/users', requestBody).subscribe(
      {next: (response) => {
        console.log('User created successfully:', response);
      },
      error: (error) =>{
        console.error('Signup failed:', error);
        this.isModalVisible = true; 
      }
    });
    alert(username + ', ' + password);

  }
  /*acceptAdminCredentials(username: string, password: string): void {
    const validPassword = this.checkPassword(this.password);
    if(!username.trim() || !password || !validPassword){
      this.isModalVisible = true;
      return;
    }
    const requestBody = { username: username.trim(), password };
    console.log('Submitting signup request:', {
      username: requestBody.username,
      passwordPresent: !!requestBody.password
    });
    this.http.post('http://localhost:5278/api/users', requestBody).subscribe(
      {next: (response) => {
        console.log('User created successfully:', response);
      },
      error: (error) =>{
        console.error('Signup failed:', error);
        this.isModalVisible = true; 
      }
    });
  }*/
}
