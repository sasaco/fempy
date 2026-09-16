import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ActivateSessionComponent } from './activate-session.component';

describe('ActivateSessionComponent', () => {
  let component: ActivateSessionComponent;
  let fixture: ComponentFixture<ActivateSessionComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ ActivateSessionComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ActivateSessionComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
