import { randomInt } from 'node:crypto'
import type { Gender } from '@contract/gender'

import { EMAIL_DOMAIN } from '../config/environment'
import { birthDateForAge } from './time'
import { randomDigits, uniqueLetters } from './unique'

const FIRST_NAMES = ['Lucía', 'Hugo', 'Martina', 'Mateo', 'Sofía', 'Leo', 'Julia', 'Pablo', 'Vega']
const LAST_NAMES = ['García', 'Fernández', 'López', 'Martín', 'Díaz', 'Prieto', 'Soto', 'Vidal']
const GENDERS: readonly Gender[] = ['Male', 'Female', 'Other', 'PreferNotToSay']
const NATIONAL_ID_LETTERS = 'TRWAGMYFPDXBNJZSQVHLCKE'

/** Who an independent account belongs to, as the registration form asks for it. */
export interface Person {
  readonly firstName: string
  readonly lastName: string
  readonly email: string
  readonly phone: string
  readonly nationalId: string
  readonly gender: Gender
}

/** A minor declared by their guardian. */
export interface Minor {
  readonly firstName: string
  readonly lastName: string
  readonly birthDate: string
  readonly gender: Gender
}

function pick<T>(values: readonly T[]): T {
  const value = values[randomInt(values.length)]
  if (value === undefined) throw new Error('Cannot pick from an empty list')
  return value
}

function capitalized(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1)
}

/** A unique address in the suite's domain, such as `e2e-member-qwerty@e2e.codigoactivo.test`. */
export function emailAddress(label: string): string {
  return `e2e-${label}-${uniqueLetters(8)}@${EMAIL_DOMAIN}`
}

/** A valid Spanish DNI: eight digits and their control letter. */
export function nationalId(): string {
  const digits = randomDigits(7).padStart(8, String(randomInt(1, 10)))
  return `${digits}${NATIONAL_ID_LETTERS[Number(digits) % 23]}`
}

/** A Spanish mobile number. */
export function phoneNumber(): string {
  return `6${randomDigits(8)}`
}

/** A unique last name, so tables and lists can find the person by it. */
export function uniqueLastName(): string {
  return `${pick(LAST_NAMES)} ${capitalized(uniqueLetters())}`
}

/** A new adult with a unique address and last name. */
export function newPerson(label: string): Person {
  return {
    firstName: pick(FIRST_NAMES),
    lastName: uniqueLastName(),
    email: emailAddress(label),
    phone: phoneNumber(),
    nationalId: nationalId(),
    gender: pick(GENDERS),
  }
}

/**
 * A minor of `age` with the guardian's last name and a first name that neither the guardian nor
 * the `siblings` already have, so every member of a household reads differently.
 */
export function newMinor(
  guardian: Pick<Person, 'firstName' | 'lastName'>,
  age = 10,
  siblings: readonly Pick<Minor, 'firstName'>[] = [],
): Minor {
  const taken = new Set([guardian.firstName, ...siblings.map((sibling) => sibling.firstName)])
  return {
    firstName: pick(FIRST_NAMES.filter((name) => !taken.has(name))),
    lastName: guardian.lastName,
    birthDate: birthDateForAge(age, -30),
    gender: pick(GENDERS),
  }
}

/** Someone the site names by first and last name. */
export type Named = Pick<Person, 'firstName' | 'lastName'>

/** How the site writes a person's name: the first name, a space and the last name. */
export function fullName(person: Named): string {
  return `${person.firstName} ${person.lastName}`
}
