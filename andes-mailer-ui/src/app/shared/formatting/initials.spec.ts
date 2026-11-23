import { initials } from './initials';

describe('initials', () => {
  it('takes the first and last words', () => {
    expect(initials('Maria José Lopez')).toBe('ML');
  });

  it('takes one letter from a single word', () => {
    expect(initials('corporate')).toBe('C');
  });

  it('returns nothing for a blank name', () => {
    expect(initials('   ')).toBe('');
  });
});
