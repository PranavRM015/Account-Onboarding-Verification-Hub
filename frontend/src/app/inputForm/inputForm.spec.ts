import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Form } from './form';
import { FormsModule } from '@angular/forms';
import { Express } from 'express';

describe('Form', () => {
  let component: Form;
  let fixture: ComponentFixture<Form>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FormsModule],
    }).compileComponents();

    fixture = TestBed.createComponent(Form);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
