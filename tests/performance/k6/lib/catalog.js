const BUDGETS = {
  cached: 50,
  read: 200,
  list: 300,
  write: 500,
  heavy: 1000,
  credential: 1000,
  upload: 2000,
}

const endpoints = {
  cached: [
    'GET /api/activities [anonymous]',
    'GET /api/activities/{activityId} [anonymous]',
    'GET /api/events [anonymous]',
    'GET /api/events/past-categories [anonymous]',
    'GET /api/events/past-years [anonymous]',
    'GET /api/events/{eventId} [anonymous]',
    'GET /api/files/{fileId} [anonymous]',
    'GET /api/files/{fileId}/content [anonymous]',
    'GET /api/files/{fileId}/thumbnail [anonymous]',
    'GET /api/legal-policies/{kind} [anonymous]',
    'GET /api/news [anonymous]',
    'GET /api/news/years [anonymous]',
    'GET /api/news/{newsItemId} [anonymous]',
    'GET /api/partners [anonymous]',
    'GET /api/partners/{partnerId} [anonymous]',
    'GET /api/resources [anonymous]',
    'GET /api/resources/{resourceId} [anonymous]',
    'GET /api/robots.txt',
    'GET /api/sitemap.xml',
  ],
  read: [
    'GET /api/activities/assignment-status-types',
    'GET /api/activities/household-assignments/{eventId}',
    'GET /api/activities/modality-types',
    'GET /api/activities/roleType',
    'GET /api/activities/signup-roles',
    'GET /api/activities/{activityId}',
    'GET /api/activities/{activityId}/overlaps/{userId}',
    'GET /api/auth/csrf',
    'GET /api/auth/login/two-factor',
    'GET /api/auth/me',
    'GET /api/auth/me [anonymous]',
    'GET /api/blocked-data/access',
    'GET /api/events/{eventId}',
    'GET /api/events/{eventId}/leader-roster',
    'GET /api/events/{eventId}/terms',
    'GET /api/events/{eventId}/terms/decisions/{userId}/{termsDocumentId}/signature',
    'GET /api/files/{fileId}',
    'GET /api/files/{fileId}/content',
    'GET /api/files/{fileId}/thumbnail',
    'GET /api/legal-entity',
    'GET /api/legal-policies/{kind}',
    'GET /api/legal-policies/{kind}/versions',
    'GET /api/legal-policies/{kind}/versions/{version}',
    'GET /api/logs/diagnostics/{id}',
    'GET /api/me/assigned-activities',
    'GET /api/me/certificates',
    'GET /api/me/consents',
    'GET /api/me/deletion',
    'GET /api/me/event-history',
    'GET /api/news/years',
    'GET /api/news/{newsItemId}',
    'GET /api/partners/{partnerId}',
    'GET /api/resources/types',
    'GET /api/resources/{resourceId}',
    'GET /api/users/status-types',
    'GET /api/users/types',
    'GET /api/users/{userId}',
    'GET /api/{unknown}',
  ],
  list: [
    'GET /api/activities',
    'GET /api/blocked-data/consultations',
    'GET /api/emails/events/{eventId}/attendees/audience',
    'GET /api/emails/users/audience',
    'GET /api/events',
    'GET /api/events/categoryType',
    'GET /api/events/termsDocument',
    'GET /api/events/{eventId}/ratings',
    'GET /api/events/{eventId}/terms/decisions',
    'GET /api/news',
    'GET /api/partners',
    'GET /api/resources',
    'GET /api/users',
  ],
  write: [
    'DELETE /api/activities/{activityId}',
    'DELETE /api/events/categoryType/{eventCategoryTypeId}',
    'DELETE /api/events/termsDocument/{termsDocumentId}',
    'DELETE /api/events/{eventId}',
    'DELETE /api/events/{eventId}/rating',
    'DELETE /api/files/{fileId}',
    'DELETE /api/news/{newsItemId}',
    'DELETE /api/partners/{partnerId}',
    'DELETE /api/resources/{resourceId}',
    'DELETE /api/users/{userId}',
    'PATCH /api/activities/{activityId}/{userId}/assign',
    'PATCH /api/activities/{activityId}/{userId}/change-role',
    'PATCH /api/activities/{activityId}/{userId}/change-status',
    'PATCH /api/activities/{activityId}/{userId}/unassign',
    'PATCH /api/events/{eventId}/feature',
    'PATCH /api/news/{newsItemId}/feature',
    'PATCH /api/users/{userId}/change-type',
    'POST /api/activities/{activityId}/assign-household',
    'POST /api/activities/{eventId}',
    'POST /api/auth/logout',
    'POST /api/events',
    'POST /api/events/categoryType',
    'POST /api/events/termsDocument',
    'POST /api/events/{eventId}/terms/decisions/{userId}/{termsDocumentId}/withdraw',
    'POST /api/news',
    'POST /api/partners',
    'POST /api/resources',
    'POST /api/users/{userId}/children',
    'PUT /api/activities/{activityId}',
    'PUT /api/events/categoryType/{eventCategoryTypeId}',
    'PUT /api/events/termsDocument/{termsDocumentId}',
    'PUT /api/events/{eventId}',
    'PUT /api/events/{eventId}/rating',
    'PUT /api/legal-policies/{kind}',
    'PUT /api/news/{newsItemId}',
    'PUT /api/partners/{partnerId}',
    'PUT /api/resources/{resourceId}',
  ],
  heavy: [
    'GET /api/logs/audit',
    'GET /api/logs/diagnostics',
    'GET /api/me/personal-data',
    'GET /api/reports/dashboard',
    'GET /api/reports/dashboard/analytics',
    'GET /api/reports/events/{eventId}/attendees',
    'GET /api/reports/events/{eventId}/badges',
    'GET /api/reports/events/{eventId}/roster',
    'GET /api/reports/events/{eventId}/summary',
    'POST /api/blocked-data/consultations/{consultationId}/extracts',
    'POST /api/blocked-data/consultations/{consultationId}/searches',
    'POST /api/emails/events/{eventId}/attendees',
    'POST /api/emails/users',
    'POST /api/emails/users/{userId}',
  ],
  credential: [
    'PATCH /api/auth/{userId}/confirm-email',
    'PATCH /api/auth/{userId}/reset-password',
    'PATCH /api/auth/{userId}/verify',
    'PATCH /api/users/{userId}/admin',
    'PATCH /api/users/{userId}/password',
    'POST /api/auth/forgot-password',
    'POST /api/auth/login',
    'POST /api/auth/login/two-factor',
    'POST /api/auth/login/two-factor/resend',
    'POST /api/auth/register',
    'POST /api/auth/resend-verification',
    'POST /api/auth/two-factor/authenticator/confirm',
    'POST /api/auth/two-factor/authenticator/setup',
    'POST /api/auth/two-factor/email',
    'POST /api/blocked-data/consultations',
    'POST /api/blocked-data/consultations/code',
    'POST /api/emails/unsubscribe',
    'POST /api/emails/unsubscribe/one-click',
    'POST /api/me/deletion',
    'POST /api/me/deletion/code',
    'POST /api/users/{userId}/two-factor/reset',
    'PUT /api/users/{userId}',
  ],
  upload: ['POST /api/files'],
}

export const CATALOG_ENDPOINTS = Object.entries(endpoints).flatMap(([kind, names]) =>
  names.map((name) => ({ name, kind, budget: BUDGETS[kind] })),
)

export const KINDS = Object.fromEntries(CATALOG_ENDPOINTS.map(({ name, kind }) => [name, kind]))

export const ENDPOINT_KINDS = Object.keys(BUDGETS)

export const CAPACITY_SLO = { p95: 500, p99: 1500, failures: 0.01 }

const RUN_TAG = 'phase:run'

export const runMetric = (metric, filter) => `${metric}{${filter},${RUN_TAG}}`

export const stepMetric = (metric, users, filter = 'target:api') =>
  runMetric(metric, `${filter},users:${users}`)

export function thresholds({ steps = [], budgets = true }) {
  const entries = {
    [runMetric('http_req_duration', 'target:api')]: ['p(95)<1000'],
    [runMetric('http_req_failed', 'target:api')]: ['rate<0.01'],
    [runMetric('checks', 'target:api')]: ['rate>0.99'],
    [runMetric('rate_limited', 'target:api')]: ['count>=0'],
    [runMetric('unexpected_responses', 'target:api')]: ['count>=0'],
  }
  for (const { name, budget } of CATALOG_ENDPOINTS) {
    entries[runMetric('http_req_duration', `name:${name}`)] = [
      budgets ? `p(95)<${budget}` : 'p(95)>=0',
    ]
    entries[runMetric('http_req_failed', `name:${name}`)] = ['rate<0.01']
  }
  for (const users of steps) {
    entries[stepMetric('http_req_duration', users)] = ['p(95)>=0']
    entries[stepMetric('http_req_failed', users)] = ['rate>=0']
    entries[stepMetric('rate_limited', users)] = ['count>=0']
    for (const kind of ENDPOINT_KINDS) {
      entries[stepMetric('http_req_duration', users, `kind:${kind}`)] = ['p(95)>=0']
    }
  }
  return entries
}
