import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export interface PlayerMentionSegment {
  readonly text: string;
  readonly isPlayer: boolean;
}

@Component({
  selector: 'app-player-mention-text',
  template: `
    @for (segment of segments(); track $index) {
      @if (segment.isPlayer) {
        <strong class="player-mention" title="Jogador da partida">{{ segment.text }}</strong>
      } @else {
        <ng-container>{{ segment.text }}</ng-container>
      }
    }
  `,
  styles: `
    :host {
      display: inline;
    }

    .player-mention {
      color: inherit;
      font: inherit;
      font-weight: 800;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlayerMentionTextComponent {
  readonly text = input.required<string>();
  readonly playerNames = input<readonly string[]>([]);

  readonly segments = computed(() => splitPlayerMentions(this.text(), this.playerNames()));
}

export function splitPlayerMentions(
  text: string,
  playerNames: readonly string[],
): readonly PlayerMentionSegment[] {
  const uniqueNames = new Map<string, string>();
  for (const playerName of playerNames) {
    const trimmedName = playerName.trim();
    if (trimmedName) {
      uniqueNames.set(trimmedName.toLocaleLowerCase('pt-BR'), trimmedName);
    }
  }

  const names = [...uniqueNames.values()].sort((left, right) => right.length - left.length);
  if (!text || names.length === 0) {
    return text ? [{ text, isPlayer: false }] : [];
  }

  const matcher = new RegExp(names.map(escapeRegularExpression).join('|'), 'giu');
  const segments: PlayerMentionSegment[] = [];
  let cursor = 0;

  for (const match of text.matchAll(matcher)) {
    const matchIndex = match.index;
    const matchedName = match[0];
    if (!hasValidBoundaries(text, matchIndex, matchedName)) {
      continue;
    }

    if (matchIndex > cursor) {
      segments.push({ text: text.slice(cursor, matchIndex), isPlayer: false });
    }

    segments.push({ text: matchedName, isPlayer: true });
    cursor = matchIndex + matchedName.length;
  }

  if (cursor < text.length) {
    segments.push({ text: text.slice(cursor), isPlayer: false });
  }

  return segments.length > 0 ? segments : [{ text, isPlayer: false }];
}

function hasValidBoundaries(text: string, index: number, matchedName: string): boolean {
  const firstCharacter = matchedName.at(0);
  const lastCharacter = matchedName.at(-1);
  const previousCharacter = index > 0 ? text[index - 1] : undefined;
  const nextCharacter = text[index + matchedName.length];

  return (
    (!isWordCharacter(firstCharacter) || !isWordCharacter(previousCharacter)) &&
    (!isWordCharacter(lastCharacter) || !isWordCharacter(nextCharacter))
  );
}

function isWordCharacter(character: string | undefined): boolean {
  return Boolean(character && /[\p{L}\p{N}_]/u.test(character));
}

function escapeRegularExpression(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
