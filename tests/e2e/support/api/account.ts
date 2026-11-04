import { expect } from '@playwright/test'
import type { EventCertificateResponse } from '@contract/eventCertificateResponse'
import type { EventHistoryResponse } from '@contract/eventHistoryResponse'
import type { MyConsentResponse } from '@contract/myConsentResponse'
import type { UserResponse } from '@contract/userResponse'
import type { UserResponsePagedResult } from '@contract/userResponsePagedResult'
import { Secret, TOTP } from 'otpauth'

import type { ApiClient } from './api-client'

const STEP_SECONDS = 30
const NEXT_STEP_TIMEOUT_MS = (STEP_SECONDS + 5) * 1_000

/** The signed-in user of `api`, as `/api/auth/me` reports it. */
export function currentUser(api: ApiClient): Promise<UserResponse> {
  return api.get<UserResponse>('/api/auth/me')
}

/** The minors in the care of `guardianId`, read the way the account page lists them. */
export async function minorsOf(api: ApiClient, guardianId: string): Promise<UserResponse[]> {
  const { items } = await api.get<UserResponsePagedResult>('/api/users', {
    parentId: guardianId,
    pageSize: 100,
  })
  return items
}

/** The entry of the event `eventId` in the household history of the signed-in user, if any. */
export async function historyEntry(
  api: ApiClient,
  eventId: string,
): Promise<EventHistoryResponse | undefined> {
  const entries = await api.get<EventHistoryResponse[]>('/api/me/event-history')
  return entries.find((entry) => entry.eventId === eventId)
}

/** The diplomas the household of the signed-in user holds for the event `eventId`. */
export async function certificatesFor(
  api: ApiClient,
  eventId: string,
): Promise<EventCertificateResponse[]> {
  const certificates = await api.get<EventCertificateResponse[]>('/api/me/certificates')
  return certificates.filter((certificate) => certificate.eventId === eventId)
}

/** The consents decided for the household of the signed-in user about the document `documentId`. */
export async function consentsFor(
  api: ApiClient,
  documentId: string,
): Promise<MyConsentResponse[]> {
  const consents = await api.get<MyConsentResponse[]>('/api/me/consents')
  return consents.filter((consent) => consent.termsDocumentId === documentId)
}

/**
 * An authenticator application enrolled with the shared key the account page shows, generating its
 * codes through `otpauth`. The API refuses a time step it already accepted, so every code it hands
 * out belongs to a newer 30-second step than the previous one, waiting for it when needed.
 */
export class AuthenticatorApp {
  private readonly totp: TOTP
  private lastStep = Number.NEGATIVE_INFINITY
  private last: string | undefined

  constructor(sharedKey: string) {
    this.totp = new TOTP({
      secret: Secret.fromBase32(sharedKey.replace(/\s+/g, '')),
      algorithm: 'SHA1',
      digits: 6,
      period: STEP_SECONDS,
    })
  }

  /** The code of the current time step, once that step is newer than the last code handed out. */
  async nextCode(): Promise<string> {
    await expect
      .poll(() => this.totp.counter(), {
        message: 'a time step newer than the last code',
        timeout: NEXT_STEP_TIMEOUT_MS,
        intervals: [500],
      })
      .toBeGreaterThan(this.lastStep)
    const timestamp = Date.now()
    this.lastStep = this.totp.counter({ timestamp })
    this.last = this.totp.generate({ timestamp })
    return this.last
  }

  /** The last code handed out, to show that the API refuses it a second time. */
  lastCode(): string {
    if (this.last === undefined) throw new Error('The application has not generated a code yet')
    return this.last
  }
}
