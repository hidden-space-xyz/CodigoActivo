import { ADMIN_POOL_SIZE, adminSlice, PROFILE } from './lib/config.js'
import { thresholds } from './lib/catalog.js'
import { report } from './lib/summary.js'
import {
  bootstrapAdmin,
  household,
  register,
  takeOverDemoMembers,
  waitForApi,
} from './lib/accounts.js'
import { createFixtures, seedSignups } from './lib/fixtures.js'
import { CATALOG, email } from './lib/data.js'

export { visitor } from './scenarios/visitors.js'
export { member } from './scenarios/members.js'
export { signup } from './scenarios/signups.js'
export { backoffice } from './scenarios/backoffice.js'
export { reports } from './scenarios/reports.js'
export { upload } from './scenarios/uploads.js'
export { mailing } from './scenarios/mailing.js'
export { privacy } from './scenarios/privacy.js'
export { accountLifecycle } from './scenarios/accounts.js'

const functions = {
  visitors: 'visitor',
  members: 'member',
  signups: 'signup',
  backoffice: 'backoffice',
  reports: 'reports',
  uploads: 'upload',
  mailing: 'mailing',
  privacy: 'privacy',
  accounts: 'accountLifecycle',
}

const singleVu = new Set(['privacy'])
const tags = { phase: 'run' }

function scenario(name) {
  const config = PROFILE.scenarios[name]
  const exec = functions[name]
  if (config.iterations) {
    return {
      executor: 'shared-iterations',
      exec,
      tags,
      vus: singleVu.has(name) ? 1 : config.iterations,
      iterations: config.iterations,
      maxDuration: '20m',
    }
  }
  return {
    executor: 'ramping-arrival-rate',
    exec,
    tags,
    startRate: 0,
    timeUnit: '1m',
    preAllocatedVUs: Math.ceil(config.vus / 2),
    maxVUs: config.vus,
    stages: config.stages ?? (config.shape ?? PROFILE.shape)(config.perMinute),
    gracefulStop: name === 'accounts' ? '6m' : '1m',
  }
}

export const options = {
  scenarios: Object.fromEntries(Object.keys(functions).map((name) => [name, scenario(name)])),
  thresholds: thresholds({ steps: PROFILE.steps, budgets: PROFILE.budgets !== false }),
  setupTimeout: '20m',
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max', 'count'],
  batch: 16,
  batchPerHost: 16,
  userAgent: 'codigoactivo-k6',
}

function account(entry) {
  return {
    id: entry.session.userId,
    email: entry.email,
    codeAt: entry.codeAt,
    dependents: entry.dependents ?? [],
    session: entry.session.snapshot(),
  }
}

export function setup() {
  const runId = Date.now().toString(36)
  waitForApi()
  const admin = bootstrapAdmin()
  const admins = register(
    Array.from({ length: ADMIN_POOL_SIZE }, (_, index) => ({
      email: email('admin', runId, index),
      grantAdmin: true,
      actor: 'admin',
      ipIndex: 2000 + index,
    })),
    admin,
  )
  const members = register(
    Array.from({ length: PROFILE.pools.members }, (_, index) => ({
      email: email('socio', runId, index),
      minors: 1 + (index % 2),
      userTypeId: CATALOG.userTypes.member,
      actor: 'member',
      ipIndex: 3000 + index,
    })),
    admin,
  )
  members.forEach((member) => (member.dependents = household(member.session)))
  const demoMembers = takeOverDemoMembers(admin, PROFILE.pools.demoMembers)
  const fixtures = createFixtures(admin, runId)
  const roster = seedSignups(admin, members, fixtures)
  return {
    runId,
    admin: account(admin),
    admins: Object.fromEntries(
      ['backoffice', 'reports', 'uploads', 'mailing'].map((name) => [
        name,
        admins.slice(...adminSlice(name)).map(account),
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
