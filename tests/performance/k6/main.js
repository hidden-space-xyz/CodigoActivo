import exec from 'k6/execution'
import { PROFILE } from './lib/profiles.js'
import { RUN_PHASE, thresholds } from './lib/thresholds.js'
import { report } from './lib/summary.js'
import { bootstrapAdmin, register, takeOverDemoMembers } from './lib/accounts.js'
import { createFixtures, seedSignups } from './lib/fixtures.js'
import { email, LOOKUPS } from './lib/data.js'
import { household } from './lib/pages.js'
import { countArrival } from './lib/windows.js'
import { visitors } from './scenarios/visitors.js'
import { members } from './scenarios/members.js'
import { signups } from './scenarios/signups.js'
import { logins } from './scenarios/logins.js'
import { newcomers } from './scenarios/newcomers.js'
import { backoffice } from './scenarios/backoffice.js'
import { reports } from './scenarios/reports.js'
import { uploads } from './scenarios/uploads.js'
import { mailing } from './scenarios/mailing.js'

const SCENARIOS = {
  visitors,
  members,
  signups,
  logins,
  newcomers,
  backoffice,
  reports,
  uploads,
  mailing,
}

const FIRST_IP = { demoMembers: 1000, admins: 2000, members: 3000 }

function scenario({ iterations, vus, stages }) {
  const common = { exec: 'run', tags: { phase: RUN_PHASE } }
  if (iterations) {
    return {
      ...common,
      executor: 'shared-iterations',
      vus: iterations,
      iterations,
      maxDuration: '20m',
    }
  }
  return {
    ...common,
    executor: 'ramping-arrival-rate',
    startRate: 0,
    timeUnit: '1m',
    preAllocatedVUs: Math.ceil(vus / 2),
    maxVUs: vus,
    stages,
    gracefulStop: '1m',
  }
}

export const options = {
  scenarios: Object.fromEntries(
    Object.entries(PROFILE.scenarios).map(([name, config]) => [name, scenario(config)]),
  ),
  thresholds: thresholds(PROFILE),
  setupTimeout: '20m',
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max', 'count'],
  batch: 16,
  batchPerHost: 16,
  userAgent: 'codigoactivo-k6',
}

function account({ session, email: address, dependents = [] }) {
  return { id: session.userId, email: address, dependents, session: session.snapshot() }
}

function adminSpecs(runId, pools) {
  return Object.entries(pools)
    .flatMap(([pool, size]) =>
      Array.from({ length: size }, (_, index) => ({
        pool,
        email: email('admin', runId, pool, index),
      })),
    )
    .map((spec, index) => ({
      ...spec,
      grantAdmin: true,
      userTypeId: LOOKUPS.userTypes.member,
      actor: 'admin',
      ipIndex: FIRST_IP.admins + index,
    }))
}

function memberSpecs(runId, size) {
  return Array.from({ length: size }, (_, index) => ({
    email: email('socio', runId, index),
    minors: index % 3,
    userTypeId: LOOKUPS.userTypes.member,
    actor: 'member',
    ipIndex: FIRST_IP.members + index,
  }))
}

/**
 * Prepares the accounts and data every scenario shares. Compose starts k6 once `volume.sql` has
 * filled the database, which it runs when the API reports healthy, after seeding the demo data.
 */
export function setup() {
  const runId = Date.now().toString(36)
  const { pools } = PROFILE
  const admin = bootstrapAdmin()
  const admins = register(adminSpecs(runId, pools.admins), admin)
  const memberAccounts = register(memberSpecs(runId, pools.members), admin)
  memberAccounts.forEach(
    (member) => (member.dependents = household(member.session).map(({ id }) => id)),
  )
  const demoMembers = takeOverDemoMembers(admin, pools.demoMembers, FIRST_IP.demoMembers)
  const fixtures = createFixtures(admin, runId)
  const roster = seedSignups(admin, memberAccounts, fixtures)
  return {
    runId,
    admins: Object.fromEntries(
      Object.keys(pools.admins).map((pool) => [
        pool,
        admins.filter((entry) => entry.pool === pool).map(account),
      ]),
    ),
    members: memberAccounts.map(account),
    demoMembers: demoMembers.map(account),
    auditor: account(admin),
    fixtures: { ...fixtures, roster },
  }
}

/** Runs the scenario k6 is iterating and counts the arrival for the window it falls in. */
export function run(data) {
  countArrival()
  SCENARIOS[exec.scenario.name](data)
}

/** Replaces k6's end-of-test summary with the report of the run. */
export function handleSummary(data) {
  return report(data)
}
