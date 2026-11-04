import { PROFILE } from './lib/profiles.js'
import { RUN_PHASE, thresholds } from './lib/thresholds.js'
import { report } from './lib/summary.js'
import { bootstrapAdmin, register, takeOverDemoMembers, waitForApi } from './lib/accounts.js'
import { createFixtures, seedSignups } from './lib/fixtures.js'
import { email, LOOKUPS } from './lib/data.js'
import { household } from './lib/pages.js'

export { visitors } from './scenarios/visitors.js'
export { members } from './scenarios/members.js'
export { signups } from './scenarios/signups.js'
export { newcomers } from './scenarios/newcomers.js'
export { backoffice } from './scenarios/backoffice.js'
export { reports } from './scenarios/reports.js'
export { uploads } from './scenarios/uploads.js'
export { mailing } from './scenarios/mailing.js'

const FIRST_IP = { demoMembers: 1000, admins: 2000, members: 3000 }

function scenario(name, { iterations, vus, stages }) {
  const common = { exec: name, tags: { phase: RUN_PHASE } }
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
    Object.entries(PROFILE.scenarios).map(([name, config]) => [name, scenario(name, config)]),
  ),
  thresholds: thresholds(PROFILE),
  setupTimeout: '20m',
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max', 'count'],
  batch: 16,
  batchPerHost: 16,
  userAgent: 'codigoactivo-k6',
}

function account({ session, dependents = [] }) {
  return { id: session.userId, dependents, session: session.snapshot() }
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
      actor: 'admin',
      ipIndex: FIRST_IP.admins + index,
    }))
}

function memberSpecs(runId, size) {
  return Array.from({ length: size }, (_, index) => ({
    email: email('socio', runId, index),
    minors: 1 + (index % 2),
    userTypeId: LOOKUPS.userTypes.member,
    actor: 'member',
    ipIndex: FIRST_IP.members + index,
  }))
}

export function setup() {
  const runId = Date.now().toString(36)
  const { pools } = PROFILE
  waitForApi()
  const admin = bootstrapAdmin()
  const admins = register(adminSpecs(runId, pools.admins), admin)
  const members = register(memberSpecs(runId, pools.members), admin)
  members.forEach((member) => (member.dependents = household(member.session).map(({ id }) => id)))
  const demoMembers = takeOverDemoMembers(admin, pools.demoMembers, FIRST_IP.demoMembers)
  const fixtures = createFixtures(admin, runId)
  const roster = seedSignups(admin, members, fixtures)
  return {
    runId,
    admins: Object.fromEntries(
      Object.keys(pools.admins).map((pool) => [
        pool,
        admins.filter((entry) => entry.pool === pool).map(account),
      ]),
    ),
    members: members.map(account),
    demoMembers: demoMembers.map(account),
    fixtures: { ...fixtures, roster },
  }
}

export function handleSummary(data) {
  return report(data)
}
