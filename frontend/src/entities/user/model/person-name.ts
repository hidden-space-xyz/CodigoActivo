/** Joins first and last name, or `—` when both are empty. */
export function fullName(person: { firstName?: string | null; lastName?: string | null }): string {
  return `${person.firstName ?? ''} ${person.lastName ?? ''}`.trim() || '—'
}
