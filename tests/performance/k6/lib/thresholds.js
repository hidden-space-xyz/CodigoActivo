import { ENDPOINT_KINDS, ENDPOINTS } from './catalog.js'

export const OBJECTIVE = { p95: 500, p99: 1500, failures: 0.01 }
export const RUN_PHASE = 'run'

const API = 'target:api'
const TRACK = { trend: ['p(95)>=0'], rate: ['rate>=0'], count: ['count>=0'] }

export const runMetric = (metric, filter) => `${metric}{${filter},phase:${RUN_PHASE}}`

export const windowMetric = (metric, window, filter = API) =>
  runMetric(metric, `${filter},window:${window}`)

const perEndpoint = (entry) => Object.fromEntries(ENDPOINTS.map(entry))

export function everyEndpointExercised() {
  return perEndpoint(({ name }) => [runMetric('http_reqs', `name:${name}`), ['count>0']])
}

export function everyEndpointWithinBudget() {
  return perEndpoint(({ name, budget }) => [
    runMetric('http_req_duration', `name:${name}`),
    [`p(95)<${budget}`],
  ])
}

export function everyEndpointReliable() {
  return perEndpoint(({ name }) => [
    runMetric('http_req_failed', `name:${name}`),
    [`rate<${OBJECTIVE.failures}`],
  ])
}

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
  }
  for (const { name } of ENDPOINTS) {
    entries[runMetric('http_req_duration', `name:${name}`)] = TRACK.trend
    entries[runMetric('http_req_failed', `name:${name}`)] = TRACK.rate
  }
  for (const { name } of windows) {
    entries[windowMetric('http_req_duration', name)] = TRACK.trend
    entries[windowMetric('http_req_failed', name)] = TRACK.rate
    entries[windowMetric('rate_limited', name)] = TRACK.count
    for (const kind of ENDPOINT_KINDS) {
      entries[windowMetric('http_req_duration', name, `kind:${kind}`)] = TRACK.trend
    }
  }
  return entries
}

export function thresholds({ windows, gates }) {
  return Object.assign(tracked(windows), ...gates.map((gate) => gate()))
}
