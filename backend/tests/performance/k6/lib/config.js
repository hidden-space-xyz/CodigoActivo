export const API_URL = __ENV.API_URL || 'http://localhost:5150'
export const MAILPIT_URL = __ENV.MAILPIT_URL || 'http://localhost:8025'
export const ADMIN_EMAIL = __ENV.BOOTSTRAP_ADMIN_EMAIL || 'admin@performance.codigoactivo.test'
export const PASSWORD = 'Rendimiento-k6-2026'
export const NEW_PASSWORD = 'Rendimiento-k6-2026-bis'
export const EMAIL_DOMAIN = 'performance.codigoactivo.test'
export const DEMO_EMAIL_DOMAIN = 'demo.codigoactivo.es'

const steady = (rate) => [
  { target: rate, duration: '1m' },
  { target: rate, duration: '8m' },
  { target: 0, duration: '30s' },
]

const escalating = (rate) => [
  { target: rate, duration: '2m' },
  { target: rate * 2, duration: '3m' },
  { target: rate * 3, duration: '3m' },
  { target: rate * 4, duration: '3m' },
  { target: 0, duration: '1m' },
]

const level = (rate) => [
  { target: rate, duration: '1m' },
  { target: rate, duration: '11m' },
]

export const STEP = { ramp: 30, hold: 120 }
const CAPACITY_STEPS = [1000, 2000, 3000, 4000, 5000, 6000, 8000, 10000]
const capacitySeconds = CAPACITY_STEPS.length * (STEP.ramp + STEP.hold)

const throughout = (rate) => [
  { target: rate, duration: `${STEP.ramp}s` },
  { target: rate, duration: `${capacitySeconds - STEP.ramp}s` },
]

function people(share, visitSeconds) {
  const rates = CAPACITY_STEPS.map((users) => Math.round((users * share * 60) / visitSeconds))
  return {
    stages: rates.flatMap((rate) => [
      { target: rate, duration: `${STEP.ramp}s` },
      { target: rate, duration: `${STEP.hold}s` },
    ]),
    vus: Math.ceil((Math.max(...rates) / 60) * 15),
  }
}

const profiles = {
  smoke: {
    think: [0, 0],
    budgets: false,
    pools: { demoMembers: 2, members: 2, backoffice: 1, reports: 1, uploads: 1, mailing: 1 },
    scenarios: {
      visitors: { iterations: 1 },
      members: { iterations: 2 },
      signups: { iterations: 2 },
      backoffice: { iterations: 1 },
      reports: { iterations: 1 },
      uploads: { iterations: 1 },
      mailing: { iterations: 1 },
      privacy: { iterations: 2 },
      accounts: { iterations: 2 },
    },
  },
  load: {
    think: [1, 3],
    shape: steady,
    pools: { demoMembers: 20, members: 20, backoffice: 2, reports: 2, uploads: 1, mailing: 1 },
    scenarios: {
      visitors: { perMinute: 120, vus: 80 },
      members: { perMinute: 30, vus: 40 },
      signups: { perMinute: 30, vus: 30 },
      backoffice: { perMinute: 6, vus: 10 },
      reports: { perMinute: 3, vus: 6 },
      uploads: { perMinute: 12, vus: 6 },
      mailing: { perMinute: 2, vus: 2, shape: steady },
      privacy: { perMinute: 4, vus: 1, shape: steady },
      accounts: { perMinute: 2, vus: 16, shape: steady },
    },
  },
  stress: {
    think: [0.3, 1],
    shape: escalating,
    pools: { demoMembers: 20, members: 60, backoffice: 6, reports: 6, uploads: 4, mailing: 1 },
    scenarios: {
      visitors: { perMinute: 600, vus: 1000 },
      members: { perMinute: 60, vus: 300 },
      signups: { perMinute: 40, vus: 200 },
      backoffice: { perMinute: 6, vus: 40 },
      reports: { perMinute: 3, vus: 16 },
      uploads: { perMinute: 24, vus: 32 },
      mailing: { perMinute: 2, vus: 2, shape: level },
      privacy: { perMinute: 4, vus: 1, shape: level },
      accounts: { perMinute: 2, vus: 16, shape: level },
    },
  },
  capacity: {
    think: [0.3, 1],
    steps: CAPACITY_STEPS,
    pools: { demoMembers: 20, members: 200, backoffice: 2, reports: 2, uploads: 1, mailing: 1 },
    scenarios: {
      visitors: people(0.7, 80),
      members: people(0.25, 70),
      signups: people(0.05, 60),
      backoffice: { perMinute: 4, vus: 10, shape: throughout },
      reports: { perMinute: 2, vus: 4, shape: throughout },
      uploads: { perMinute: 6, vus: 4, shape: throughout },
      mailing: { perMinute: 1, vus: 2, shape: throughout },
      privacy: { perMinute: 2, vus: 1, shape: throughout },
      accounts: { perMinute: 1, vus: 8, shape: throughout },
    },
  },
}

export const PROFILE_NAME = __ENV.PROFILE || 'load'
export const PROFILE = profiles[PROFILE_NAME]

if (!PROFILE) {
  throw new Error(`Unknown PROFILE "${PROFILE_NAME}"; use ${Object.keys(profiles).join(', ')}`)
}

const adminPools = ['backoffice', 'reports', 'uploads', 'mailing']

export const ADMIN_POOL_SIZE = adminPools.reduce((sum, name) => sum + PROFILE.pools[name], 0)

export function adminSlice(name) {
  const start = adminPools
    .slice(0, adminPools.indexOf(name))
    .reduce((sum, key) => sum + PROFILE.pools[key], 0)
  return [start, start + PROFILE.pools[name]]
}
