import { provideRouter, Router } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';

import { HomePage } from './home.page';

describe('HomePage', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HomePage],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('normalizes the room code to five uppercase characters', async () => {
    const fixture = TestBed.createComponent(HomePage);
    await fixture.whenStable();

    const input = fixture.nativeElement.querySelector('#room-code') as HTMLInputElement;
    input.value = 'a-bc12x';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(input.value).toBe('ABC12');
  });

  it('shows validation when trying to enter an incomplete room code', async () => {
    const fixture = TestBed.createComponent(HomePage);
    await fixture.whenStable();

    fixture.componentInstance.joinRoom();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Digite os 5 caracteres da sala.');
  });

  it('navigates to a valid normalized room code', () => {
    const fixture = TestBed.createComponent(HomePage);
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    fixture.componentInstance.roomCode.setValue('ABC12');
    fixture.componentInstance.joinRoom();

    expect(navigate).toHaveBeenCalledWith(['/sala', 'ABC12']);
  });
});
