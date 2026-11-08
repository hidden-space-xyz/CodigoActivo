import type { RegisterRequest } from '@contract/registerRequest'
import type { StaffRole } from '@contract/staffRole'
import type { UserResponse } from '@contract/userResponse'
import type { UserResponsePagedResult } from '@contract/userResponsePagedResult'

import { PASSWORD } from '../config/environment'
import { newMinor, newPerson, type Minor, type Person } from '../data/people'
import {
  accountLinkParts,
  codeIn,
  linkIn,
  SUBJECTS,
  type EmailQuery,
  type Mailbox,
} from '../mail/mailbox'
import type { ApiClient } from './api-client'
import { CATALOG, type UserTypeName } from './catalogs'

/** The email and password an account signs in with. */
export interface Credentials {
  readonly email: string
  readonly password: string
}

/** A dependent of an account, created with the registration. */
export interface Dependent extends Minor {
  readonly id: string
}

/** An independent account the suite registered or took over, with what it knows about it. */
export interface Account extends Person, Credentials {
  readonly id: string
  readonly dependents: readonly Dependent[]
}

/** What to register: a label for the address and how many minors the household brings. */
export interface Registration {
  readonly label: string
  readonly email?: string
  readonly minors?: number
  readonly newsConsent?: boolean
}

const CODE_REUSE_MS = 60_000

/** The registration request the SPA sends for `person` and their `minors`. */
function registrationOf(
  person: Person,
  minors: readonly Minor[],
  newsConsent: boolean,
): RegisterRequest {
  return {
    ...person,
    password: PASSWORD,
    declaresAdult: true,
    newsConsent,
    minors: minors.map((minor) => ({ ...minor, declaresGuardianship: true })),
  }
}

/** A registration the API accepted, before its address is verified. */
export interface PendingRegistration {
  readonly person: Person
  readonly minors: readonly Minor[]
  readonly sentAfter: Date
}

/** Registers a new person through the API without verifying the address. */
export async function registerUnverified(
  api: ApiClient,
  registration: Registration,
): Promise<PendingRegistration> {
  const generated = newPerson(registration.label)
  const person = registration.email ? { ...generated, email: registration.email } : generated
  const minors: Minor[] = []
  for (let index = 0; index < (registration.minors ?? 0); index++) {
    minors.push(newMinor(person, 8 + index, minors))
  }
  const sentAfter = new Date()
  await api.post('/api/auth/register', registrationOf(person, minors, !!registration.newsConsent))
  return { person, minors, sentAfter }
}

/**
 * Registers a new person, opens the verification link they receive and returns the active
 * account. `admin`, when given, reads the ids of the dependents the registration created.
 */
export async function registerAccount(
  api: ApiClient,
  mailbox: Mailbox,
  registration: Registration,
  admin?: ApiClient,
): Promise<Account> {
  const { person, minors, sentAfter } = await registerUnverified(api, registration)
  const email = await mailbox.waitFor({
    to: person.email,
    subject: SUBJECTS.verification,
    sentAfter,
  })
  const { userId, code } = accountLinkParts(linkIn(email, '/verify-account'))
  await api.patch(`/api/auth/${userId}/verify`, { otp: code })
  const dependents = minors.length && admin ? await dependentsOf(admin, userId, minors) : []
  return { ...person, password: PASSWORD, id: userId, dependents }
}

async function dependentsOf(
  admin: ApiClient,
  guardianId: string,
  minors: readonly Minor[],
): Promise<Dependent[]> {
  const { items } = await admin.get<UserResponsePagedResult>('/api/users', {
    scope: 'Dependent',
    parentId: guardianId,
    pageSize: 100,
  })
  return minors.map((minor) => {
    const match = items.find((item) => item.birthDate?.startsWith(minor.birthDate))
    if (!match) throw new Error(`The dependent ${minor.firstName} was not created`)
    return { ...minor, id: match.id }
  })
}

/**
 * The login code sent to `email` after `sentAfter`. The API reuses a code sent less than a minute
 * earlier instead of sending another, so that one is accepted too.
 */
export function loginCodeQuery(email: string, sentAfter: Date): EmailQuery {
  return {
    to: email,
    subject: SUBJECTS.loginCode,
    sentAfter,
    reusedAfter: new Date(sentAfter.getTime() - CODE_REUSE_MS),
  }
}

/** Completes both login steps through the API: the password, then the emailed code. */
export async function signIn(
  api: ApiClient,
  mailbox: Mailbox,
  credentials: Credentials,
): Promise<UserResponse> {
  const sentAfter = new Date()
  await api.post('/api/auth/login', {
    identifier: credentials.email,
    password: credentials.password,
    keepSignedIn: true,
  })
  const email = await mailbox.waitFor(loginCodeQuery(credentials.email, sentAfter))
  const user = await api.post<UserResponse>('/api/auth/login/two-factor', { code: codeIn(email) })
  api.forgetCsrfToken()
  return user
}

/** Sets a new password through the emailed reset link, which also lifts a password lockout. */
export async function resetPassword(
  api: ApiClient,
  mailbox: Mailbox,
  credentials: Credentials,
): Promise<void> {
  const sentAfter = new Date()
  await api.post('/api/auth/forgot-password', { email: credentials.email })
  const email = await mailbox.waitFor({
    to: credentials.email,
    subject: SUBJECTS.passwordReset,
    sentAfter,
  })
  const { userId, code } = accountLinkParts(linkIn(email, '/reset-password'))
  await api.patch(`/api/auth/${userId}/reset-password`, {
    otp: code,
    newPassword: credentials.password,
  })
}

/** Gives `userId` a staff role; the initial administrator confirms with its own password. */
export async function grantStaffRole(
  initialAdmin: ApiClient,
  userId: string,
  staffRole: StaffRole,
): Promise<void> {
  await initialAdmin.patch(`/api/users/${userId}/staff-role`, {
    staffRole,
    currentPassword: PASSWORD,
  })
}

/**
 * Designates `userId` for data protection; the initial administrator confirms with its password.
 */
export async function designatePrivacyOfficer(
  initialAdmin: ApiClient,
  userId: string,
): Promise<void> {
  await initialAdmin.post(`/api/users/${userId}/privacy-officer`, { currentPassword: PASSWORD })
}

/** Changes the type of the account `userId`. */
export async function changeUserType(
  admin: ApiClient,
  userId: string,
  type: UserTypeName,
): Promise<void> {
  await admin.patch(`/api/users/${userId}/change-type`, undefined, {
    userTypeId: CATALOG.userTypes[type],
  })
}

/** The account with exactly `email`, as an administrator lists it. */
export async function findUser(admin: ApiClient, email: string): Promise<UserResponse | undefined> {
  const { items } = await admin.get<UserResponsePagedResult>('/api/users', {
    scope: 'Independent',
    email,
    pageSize: 25,
  })
  return items.find((item) => item.email?.toLowerCase() === email.toLowerCase())
}

/** The first minor in the care of `account`, for tests whose account registered one. */
export function firstDependent(account: Account): Dependent {
  const [dependent] = account.dependents
  if (!dependent) throw new Error(`${account.email} has no minor in their care`)
  return dependent
}
