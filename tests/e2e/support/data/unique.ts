import { randomInt } from 'node:crypto'

const LETTERS = 'abcdefghijklmnopqrstuvwxyz'

/** Lowercase letters that make a name or an address unique to one test run. */
export function uniqueLetters(length = 6): string {
  return Array.from({ length }, () => LETTERS[randomInt(LETTERS.length)]).join('')
}

/** A title that no other test or run uses, such as `Taller e2e qwerty`. */
export function uniqueTitle(prefix: string): string {
  return `${prefix} e2e ${uniqueLetters()}`
}

/** A string of `count` random decimal digits. */
export function randomDigits(count: number): string {
  return Array.from({ length: count }, () => String(randomInt(10))).join('')
}
