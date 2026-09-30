const NATIONAL_ID_CONTROL_LETTERS = 'TRWAGMYFPDXBNJZSQVHLCKE'
const NIE_PREFIXES: Readonly<Record<string, string>> = { X: '0', Y: '1', Z: '2' }
const NATIONAL_ID_SHAPE = /^[XYZ\d]\d{7}[A-Z]$/

/**
 * Normalizes a Spanish DNI or NIE the way the API stores it: uppercase, without spaces or hyphens.
 * Two entries that differ only in those details are the same identifier.
 */
export function normalizeNationalId(value: string): string {
  return value.replace(/[\s-]/g, '').toUpperCase()
}

/**
 * Checks a Spanish DNI (eight digits and a control letter) or NIE (X, Y or Z, seven digits and a
 * control letter) after normalizing it, with the Ministry of the Interior algorithm the API also
 * applies: the number, reading a NIE's X, Y or Z as 0, 1 or 2, modulo 23 picks the control letter.
 * Any single mistyped digit or swap of two adjacent digits changes that letter, so the value never
 * needs to be typed twice. Returns `'format'` when the value is not shaped like a DNI or NIE,
 * `'letter'` when its control letter does not match the number, and `null` when it is valid.
 */
export function nationalIdError(value: string): 'format' | 'letter' | null {
  const normalized = normalizeNationalId(value)
  if (!NATIONAL_ID_SHAPE.test(normalized)) return 'format'
  const first = normalized.charAt(0)
  const digits = `${NIE_PREFIXES[first] ?? first}${normalized.slice(1, 8)}`
  const expected = NATIONAL_ID_CONTROL_LETTERS.charAt(
    Number(digits) % NATIONAL_ID_CONTROL_LETTERS.length,
  )
  return normalized.charAt(8) === expected ? null : 'letter'
}
