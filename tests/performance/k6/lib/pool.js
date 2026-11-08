import exec from 'k6/execution'
import { Session } from './http.js'

const sessions = {}

/**
 * Deals the accounts of a pool in turn across all the VUs of a scenario. A pool must be large
 * enough that an account finishes its iteration before its next turn.
 */
export function nextAccount(pool) {
  const iteration = exec.scenario.iterationInTest
  return {
    account: pool[iteration % pool.length],
    round: Math.floor(iteration / pool.length),
    iteration,
  }
}

/** Reuses the session setup opened for an account, with cookies of its own per VU. */
export function sessionFor(account) {
  if (!sessions[account.id]) {
    sessions[account.id] = new Session(account.session)
  }
  return sessions[account.id]
}
