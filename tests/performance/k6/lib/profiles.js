import { sleep } from 'k6'
import {
  everyEndpointExercised,
  everyEndpointReliable,
  everyEndpointWithinBudget,
  objectiveMetIn,
} from './thresholds.js'

const PAGE_SECONDS = 10
const ITERATION_SECONDS = 15
const RAMP_DOWN_SECONDS = 30
const RUSH_FACTOR = 10
const CAPACITY_USERS = [1000, 2000, 3000, 4000, 5000, 6000, 8000, 10000]

const peak = {
  visitors: { perMinute: 120, vus: 80 },
  members: { perMinute: 30, vus: 40 },
  signups: { perMinute: 30, vus: 30 },
  newcomers: { perMinute: 4, vus: 10 },
  backoffice: { perMinute: 6, vus: 10 },
  reports: { perMinute: 3, vus: 6 },
  uploads: { perMinute: 12, vus: 6 },
  mailing: { perMinute: 1, vus: 2 },
}

function rush({ perMinute, vus }) {
  return { perMinute: (level) => perMinute * level, vus: vus * RUSH_FACTOR }
}

function people(share, pages) {
  const perMinute = (users) => (users * share * 60) / (pages * PAGE_SECONDS)
  const top = perMinute(Math.max(...CAPACITY_USERS))
  return { perMinute, vus: Math.ceil((top / 60) * ITERATION_SECONDS) }
}

const profiles = {
  smoke: {
    think: [0, 0],
    pools: {
      members: 2,
      demoMembers: 2,
      admins: { backoffice: 1, reports: 1, uploads: 1, mailing: 1 },
    },
    scenarios: {
      visitors: { iterations: 1 },
      members: { iterations: 2 },
      signups: { iterations: 2 },
      newcomers: { iterations: 1 },
      backoffice: { iterations: 1 },
      reports: { iterations: 1 },
      uploads: { iterations: 1 },
      mailing: { iterations: 1 },
    },
    gates: [everyEndpointExercised, everyEndpointReliable],
  },
  load: {
    think: [1, 3],
    phases: [{ ramp: 60, hold: 480 }],
    pools: {
      members: 20,
      demoMembers: 20,
      admins: { backoffice: 2, reports: 2, uploads: 1, mailing: 1 },
    },
    scenarios: peak,
    gates: [everyEndpointExercised, everyEndpointWithinBudget, everyEndpointReliable],
  },
  spike: {
    think: [1, 3],
    phases: [
      { ramp: 60, hold: 120, window: { name: 'baseline' } },
      { ramp: 10, hold: 180, level: RUSH_FACTOR, window: { name: 'rush' } },
      { ramp: 10, hold: 30 },
      { ramp: 0, hold: 150, window: { name: 'recovery' } },
    ],
    pools: {
      members: 100,
      demoMembers: 20,
      admins: { backoffice: 2, reports: 2, uploads: 1, mailing: 1 },
    },
    scenarios: { ...peak, signups: rush(peak.signups), newcomers: rush(peak.newcomers) },
    gates: [everyEndpointExercised, everyEndpointReliable, objectiveMetIn('recovery')],
  },
  capacity: {
    think: [0.3, 1],
    phases: CAPACITY_USERS.map((users) => ({
      ramp: 30,
      hold: 120,
      level: users,
      window: { name: `${users}`, users },
    })),
    pools: {
      members: 400,
      demoMembers: 20,
      admins: { backoffice: 2, reports: 2, uploads: 1, mailing: 1 },
    },
    scenarios: {
      visitors: people(0.7, 8),
      members: people(0.25, 6),
      signups: people(0.05, 5),
      newcomers: { perMinute: 4, vus: 10 },
      backoffice: { perMinute: 4, vus: 10 },
      reports: { perMinute: 2, vus: 4 },
      uploads: { perMinute: 6, vus: 4 },
      mailing: { perMinute: 1, vus: 2 },
    },
    gates: [everyEndpointExercised],
  },
}

function stages(phases, perMinute) {
  const rate = (level = 1) =>
    Math.round(typeof perMinute === 'function' ? perMinute(level) : perMinute)
  return phases
    .flatMap(({ ramp, hold, level }) => [
      { target: rate(level), duration: `${ramp}s` },
      { target: rate(level), duration: `${hold}s` },
    ])
    .filter(({ duration }) => duration !== '0s')
    .concat({ target: 0, duration: `${RAMP_DOWN_SECONDS}s` })
}

function windows(phases) {
  let elapsed = 0
  return phases.flatMap(({ ramp, hold, window }) => {
    const from = elapsed + ramp
    elapsed = from + hold
    return window ? [{ ...window, from, to: elapsed }] : []
  })
}

function normalize(name, { phases = [], scenarios, ...profile }) {
  return {
    ...profile,
    name,
    windows: windows(phases),
    scenarios: Object.fromEntries(
      Object.entries(scenarios).map(([scenario, { perMinute, vus, iterations }]) => [
        scenario,
        iterations ? { iterations } : { vus, stages: stages(phases, perMinute) },
      ]),
    ),
  }
}

const requested = __ENV.PROFILE || 'load'

if (!profiles[requested]) {
  throw new Error(`Unknown PROFILE "${requested}"; use ${Object.keys(profiles).join(', ')}`)
}

export const PROFILE = normalize(requested, profiles[requested])

export function think() {
  const [min, max] = PROFILE.think
  if (max > 0) {
    sleep(min + Math.random() * (max - min))
  }
}
