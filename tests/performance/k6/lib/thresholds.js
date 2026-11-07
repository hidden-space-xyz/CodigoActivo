import { ENDPOINT_KINDS, ENDPOINTS } from './catalog.js'

/** What a window must reach: API latency and failures, and the share of its load k6 delivered. */
export const OBJECTIVE = { p95: 500, p99: 1500, failures: 0.01, delivered: 0.95 }

/** p95 in milliseconds from asking for a code to reading it in Mailpit. */
const MAIL_DELAY_BUDGET = 5000

/** Tag of the scenario samples, which leaves the requests of setup out of the results. */
export const RUN_PHASE = 'run'

const API = 'target:api'
const TRACK = { trend: ['p(95)>=0'], rate: ['rate>=0'], count: ['count>=0'] }

export const runMetric = (metric, filter) => `${metric}{${filter},phase:${RUN_PHASE}}`

export const windowMetric = (metric, window, filter = API) =>
  runMetric(metric, `${filter},window:${window}`)

export const arrivalsMetric = (window) => runMetric('arrivals', `window:${window}`)

const perEndpoint = (entry) => Object.fromEntries(ENDPOINTS.map(entry))

/** Gate: every catalogued endpoint receives at least one request. */
export function everyEndpointExercised() {
  return perEndpoint(({ name }) => [runMetric('http_reqs', `name:${name}`), ['count>0']])
}

/** Gate: every catalogued endpoint keeps the p95 budget of its kind. */
export function everyEndpointWithinBudget() {
  return perEndpoint(({ name, budget }) => [
    runMetric('http_req_duration', `name:${name}`),
    [`p(95)<${budget}`],
  ])
}

/** Gate: no catalogued endpoint fails more requests than the objective allows. */
export function everyEndpointReliable() {
  return perEndpoint(({ name }) => [
    runMetric('http_req_failed', `name:${name}`),
    [`rate<${OBJECTIVE.failures}`],
  ])
}

/** Gate: every window receives the iterations its profile promises, so a pass proves the load. */
export function everyWindowDelivered({ windows }) {
  return Object.fromEntries(
    windows.map(({ name, arrivals }) => [
      arrivalsMetric(name),
      [`count>=${Math.floor(arrivals * OBJECTIVE.delivered)}`],
    ]),
  )
}

/** Gate: emailed codes reach their reader within the budget. */
export function codesArriveInTime() {
  return { mail_delay: [`p(95)<${MAIL_DELAY_BUDGET}`] }
}

/** Builds the gate that holds the API requests of a window to the objective. */
export function objectiveMetIn(window) {
  return () => ({
    [windowMetric('http_req_duration', window)]: [
      `p(95)<${OBJECTIVE.p95}`,
      `p(99)<${OBJECTIVE.p99}`,
    ],
    [windowMetric('http_req_failed', window)]: [`rate<${OBJECTIVE.failures}`],
  })
}

function tracked(windows) {
  const entries = {
    [runMetric('http_req_duration', API)]: TRACK.trend,
    [runMetric('http_req_failed', API)]: TRACK.rate,
    [runMetric('rate_limited', API)]: TRACK.count,
    [runMetric('http_reqs', `${API},status:0`)]: TRACK.count,
  }
  for (const { name } of ENDPOINTS) {
    entries[runMetric('http_req_duration', `name:${name}`)] = TRACK.trend
    entries[runMetric('http_req_failed', `name:${name}`)] = TRACK.rate
  }
  for (const { name } of windows) {
    entries[windowMetric('http_req_duration', name)] = TRACK.trend
    entries[windowMetric('http_req_failed', name)] = TRACK.rate
    entries[windowMetric('rate_limited', name)] = TRACK.count
    entries[arrivalsMetric(name)] = TRACK.count
    for (const kind of ENDPOINT_KINDS) {
      entries[windowMetric('http_req_duration', name, `kind:${kind}`)] = TRACK.trend
    }
  }
  return entries
}

/**
 * Declares the gates of a profile. A threshold is also what makes k6 keep a sub-metric, so `tracked`
 * adds ones that always pass for everything the summary reports.
 */
export function thresholds(profile) {
  return Object.assign(tracked(profile.windows), ...profile.gates.map((gate) => gate(profile)))
}
