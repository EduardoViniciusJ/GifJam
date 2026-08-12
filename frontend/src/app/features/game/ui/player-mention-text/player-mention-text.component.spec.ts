import { TestBed } from '@angular/core/testing';

import { PlayerMentionTextComponent, splitPlayerMentions } from './player-mention-text.component';

describe('PlayerMentionTextComponent', () => {
  it('bolds only complete player names and preserves the phrase', async () => {
    await TestBed.configureTestingModule({
      imports: [PlayerMentionTextComponent],
    }).compileComponents();
    const fixture = TestBed.createComponent(PlayerMentionTextComponent);
    fixture.componentRef.setInput(
      'text',
      'Quando Ana Clara encontra Ana e BRUNO depois da partida.',
    );
    fixture.componentRef.setInput('playerNames', ['Ana', 'Ana Clara', 'Bruno']);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const mentions = [...element.querySelectorAll('strong.player-mention')].map(
      (mention) => mention.textContent,
    );

    expect(mentions).toEqual(['Ana Clara', 'Ana', 'BRUNO']);
    expect(element.textContent?.trim()).toBe(
      'Quando Ana Clara encontra Ana e BRUNO depois da partida.',
    );
  });

  it('does not highlight a player name inside another word', () => {
    const segments = splitPlayerMentions('A banana caiu.', ['Ana']);

    expect(segments).toEqual([{ text: 'A banana caiu.', isPlayer: false }]);
  });

  it('treats player names as text instead of regular expressions', () => {
    const segments = splitPlayerMentions('O plano de A.* deu certo.', ['A.*']);

    expect(segments.some((segment) => segment.isPlayer && segment.text === 'A.*')).toBe(true);
  });
});
