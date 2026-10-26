import exec from 'k6/execution'
import { Session } from './http.js'

const sessions = {}

export function nextAccount(pool) {
  const iteration = exec.scenario.iterationInTest
  return {
    account: pool[iteration % pool.length],
    round: Math.floor(iteration / pool.length),
    iteration,
  }
}

export function sessionFor(account) {
  if (!sessions[account.id]) {
    sessions[account.id] = Session.restore(account.session)
  }
  return sessions[account.id]
}
