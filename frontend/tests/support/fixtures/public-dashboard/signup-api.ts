import type {
  ActivityResponse,
  AssignedActivityResponse,
  EventTermsDocumentStateResponse,
  HouseholdMemberAssignmentResponse,
  HouseholdSignupRolesResponse,
  TimeOverlapResponse,
  UserResponse,
} from '@/shared/api/generated/models'

import { apiError, http, HttpResponse, paged, server } from '../../server'
import { buildUserResponse } from '../user'
import { buildActivityResponse } from './builders'

/** State served by the fake signup API; tests mutate it to change later responses. */
export interface SignupApiState {
  activities: ActivityResponse[]
  assigned: AssignedActivityResponse[]
  children: UserResponse[]
  household: HouseholdMemberAssignmentResponse[]
  signupRoles: HouseholdSignupRolesResponse[] | 'error'
  /**
   * Whether the single default terms document (`TERMS_DOCUMENT`) is already accepted; ignored
   * once `termsDocuments` is set explicitly.
   */
  termsAccepted: boolean
  /** Overrides the full documents list served by `/terms`; unset derives one from `termsAccepted`. */
  termsDocuments?: EventTermsDocumentStateResponse[]
  overlap: TimeOverlapResponse | 'error'
  assignError: (() => Response) | null
  householdError: (() => Response) | null
  unassignError: (() => Response) | null
}

/**
 * The one terms document served by `/api/events/:eventId/terms` by default; its `accepted`/
 * `decidedAt` fields are filled in from `state.termsAccepted`.
 */
export const TERMS_DOCUMENT = {
  termsDocumentId: 'terms-1',
  name: 'Normas del campamento',
  description: 'Respeta a los demás.',
  required: true,
  displayOrder: 0,
}

/** A second, optional terms document for tests that need more than one document to decide on. */
export const OPTIONAL_TERMS_DOCUMENT = {
  termsDocumentId: 'terms-2',
  name: 'Boletín informativo',
  description: 'Recibe noticias del evento por correo.',
  required: false,
  displayOrder: 1,
}

/** Request bodies and counters recorded by the fake signup API. */
interface SignupApiCalls {
  counts: Record<string, number>
  activityQueries: string[]
  childrenQueries: string[]
  overlapChecks: string[]
  assign: { path: string; body: unknown; csrf: string | null }[]
  household: { path: string; body: unknown }[]
  unassign: string[]
}

/** Signup roles; the signed-in user may only take the first one, their child any of them. */
export const ROLES = [
  { id: 'role-participant', name: 'Participante' },
  { id: 'role-volunteer', name: 'Voluntario' },
]

/** Minor managed by the default signed-in user. */
export const CHILD = buildUserResponse({ id: 'child-1', firstName: 'Byron', lastName: 'King' })

/**
 * Registers MSW handlers for every endpoint the activity signup flow uses and returns the mutable
 * state behind them plus the recorded calls.
 */
export function serveSignupApi(overrides: Partial<SignupApiState> = {}) {
  const state: SignupApiState = {
    activities: [buildActivityResponse()],
    assigned: [],
    children: [],
    household: [],
    signupRoles: [
      { userId: 'user-1', roles: [ROLES[0] ?? {}] },
      { userId: 'child-1', roles: ROLES },
    ],
    termsAccepted: true,
    overlap: { hasOverlaps: false, overlaps: [] },
    assignError: null,
    householdError: null,
    unassignError: null,
    ...overrides,
  }
  const calls: SignupApiCalls = {
    counts: {},
    activityQueries: [],
    childrenQueries: [],
    overlapChecks: [],
    assign: [],
    household: [],
    unassign: [],
  }
  const count = (name: string) => {
    calls.counts[name] = (calls.counts[name] ?? 0) + 1
  }

  server.use(
    http.get('/api/activities', ({ request }) => {
      count('activities')
      calls.activityQueries.push(new URL(request.url).search)
      return HttpResponse.json(paged(state.activities))
    }),
    http.get('/api/me/assigned-activities', () => {
      count('assigned')
      return HttpResponse.json(state.assigned)
    }),
    http.get('/api/users', ({ request }) => {
      count('children')
      calls.childrenQueries.push(new URL(request.url).search)
      return HttpResponse.json(paged(state.children))
    }),
    http.get('/api/activities/household-assignments/:eventId', () => {
      count('household')
      return HttpResponse.json(state.household)
    }),
    http.get('/api/activities/signup-roles', () => {
      count('signupRoles')
      return state.signupRoles === 'error' ? apiError(500) : HttpResponse.json(state.signupRoles)
    }),
    http.get('/api/events/:eventId/terms', () => {
      count('terms')
      const documents =
        state.termsDocuments ??
        ([
          {
            ...TERMS_DOCUMENT,
            accepted: state.termsAccepted ? true : null,
            decidedAt: state.termsAccepted ? '2026-01-01T00:00:00Z' : null,
          },
        ] satisfies EventTermsDocumentStateResponse[])
      const signupBlocked = documents.some((document) => document.required && !document.accepted)
      return HttpResponse.json({ documents, signupBlocked })
    }),
    http.get('/api/activities/:activityId/overlaps/:userId', ({ params }) => {
      calls.overlapChecks.push(`${String(params.activityId)}/${String(params.userId)}`)
      return state.overlap === 'error' ? apiError(500) : HttpResponse.json(state.overlap)
    }),
    http.patch('/api/activities/:activityId/:userId/assign', async ({ request, params }) => {
      calls.assign.push({
        path: `${String(params.activityId)}/${String(params.userId)}`,
        body: await request.json(),
        csrf: request.headers.get('X-CSRF-TOKEN'),
      })
      return state.assignError?.() ?? new HttpResponse(null, { status: 204 })
    }),
    http.post('/api/activities/:activityId/assign-household', async ({ request, params }) => {
      calls.household.push({ path: String(params.activityId), body: await request.json() })
      return state.householdError?.() ?? new HttpResponse(null, { status: 204 })
    }),
    http.patch('/api/activities/:activityId/:userId/unassign', ({ params }) => {
      calls.unassign.push(`${String(params.activityId)}/${String(params.userId)}`)
      return state.unassignError?.() ?? new HttpResponse(null, { status: 204 })
    }),
  )

  return { state, calls }
}
