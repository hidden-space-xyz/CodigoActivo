import { expect } from '@playwright/test'
import type { AuditLogEntryResponse } from '@contract/auditLogEntryResponse'
import type { AuditLogEntryResponsePagedResult } from '@contract/auditLogEntryResponsePagedResult'
import type { DiagnosticLogEntryResponse } from '@contract/diagnosticLogEntryResponse'
import type { DiagnosticLogEntryResponsePagedResult } from '@contract/diagnosticLogEntryResponsePagedResult'
import type { GetApiLogsAuditParams } from '@contract/getApiLogsAuditParams'
import type { HouseholdSignupRolesResponse } from '@contract/householdSignupRolesResponse'
import type { UserResponse } from '@contract/userResponse'

import { PASSWORD } from '../config/environment'
import { ApiClient, ApiRequestError, type Query } from './api-client'

const WRONG_PASSWORDS_TO_LOCK = 5
const LOG_DELIVERY = { timeout: 20_000, intervals: [2_500, 1_500] }

/** The names of the roles the signed-in person of `api` may sign `userId` up with. */
export async function signupRoleNames(api: ApiClient, userId: string): Promise<string[]> {
  const household = await api.get<HouseholdSignupRolesResponse[]>('/api/activities/signup-roles')
  return household.find((member) => member.userId === userId)?.roles.map((role) => role.name) ?? []
}

/**
 * Changes the main phone of the signed-in person of `api` from «Mi cuenta», keeping every other
 * field, so the data history keeps the previous number.
 */
export async function changeOwnPhone(api: ApiClient, phone: string): Promise<void> {
  const me = await api.get<UserResponse>('/api/auth/me')
  await api.put(`/api/users/${me.id}`, {
    firstName: me.firstName,
    lastName: me.lastName,
    email: me.email ?? null,
    phone,
    secondaryPhone: me.secondaryPhone ?? null,
    birthDate: me.birthDate ?? null,
    nationalId: me.nationalId ?? null,
    newsConsent: me.newsConsent,
    gender: me.gender,
    parentId: null,
    currentPassword: PASSWORD,
  })
}

/**
 * Sends wrong passwords for `email` from `clientAddress` until the account locks and returns the
 * trace the API gave the attempt that locked it, the one its diagnostic entry carries.
 */
export async function lockOutWithWrongPasswords(
  clientAddress: string,
  email: string,
): Promise<string> {
  const api = await ApiClient.open({ clientAddress })
  try {
    let traceId: string | undefined
    for (let attempt = 0; attempt < WRONG_PASSWORDS_TO_LOCK; attempt += 1) {
      const refusal: unknown = await api
        .post('/api/auth/login', {
          identifier: email,
          password: `${PASSWORD}-wrong`,
          keepSignedIn: false,
        })
        .then(
          () => undefined,
          (error: unknown) => error,
        )
      if (!(refusal instanceof ApiRequestError) || refusal.status !== 401) {
        throw new Error(`A wrong password for ${email} was not refused`)
      }
      traceId = refusal.traceId
    }
    if (!traceId) throw new Error(`The lockout of ${email} carried no trace`)
    return traceId
  } finally {
    await api.dispose()
  }
}

/** Waits until the diagnostic log holds the entries of `traceId` and returns them. */
export async function diagnosticsOfTrace(
  admin: ApiClient,
  traceId: string,
): Promise<DiagnosticLogEntryResponse[]> {
  let items: DiagnosticLogEntryResponse[] = []
  await expect
    .poll(async () => {
      const page = await admin.get<DiagnosticLogEntryResponsePagedResult>('/api/logs/diagnostics', {
        traceId,
      })
      items = page.items
      return items.length
    }, LOG_DELIVERY)
    .toBeGreaterThan(0)
  return items
}

/** Waits until the audit log holds an entry matching `params` that `accepts` and returns it. */
export async function auditEntry(
  reader: ApiClient,
  params: GetApiLogsAuditParams,
  accepts: (entry: AuditLogEntryResponse) => boolean,
): Promise<AuditLogEntryResponse> {
  let found: AuditLogEntryResponse | undefined
  const query: Query = { ...params }
  await expect
    .poll(async () => {
      const page = await reader.get<AuditLogEntryResponsePagedResult>('/api/logs/audit', query)
      found = page.items.find(accepts)
      return found !== undefined
    }, LOG_DELIVERY)
    .toBe(true)
  if (!found) throw new Error('The audit entry did not arrive')
  return found
}
