/** Up to two letters for an avatar: the first and last words' initials ("Maria Lopez" → "ML"). */
export function initials(name: string): string {
  const words = name.trim().split(/\s+/).filter(Boolean);

  if (words.length === 0) {
    return '';
  }

  const first = words[0].charAt(0);
  const last = words.length > 1 ? words[words.length - 1].charAt(0) : '';

  return (first + last).toLocaleUpperCase();
}
