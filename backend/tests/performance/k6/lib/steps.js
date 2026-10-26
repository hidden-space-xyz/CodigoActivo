import exec from 'k6/execution'
import { PROFILE, STEP } from './config.js'

export function currentUsers() {
  if (!PROFILE.steps) {
    return null
  }
  let startTime
  try {
    startTime = exec.scenario.startTime
  } catch {
    return null
  }
  const elapsed = (Date.now() - startTime) / 1000
  const period = STEP.ramp + STEP.hold
  const index = Math.floor(elapsed / period)
  const holding = elapsed - index * period >= STEP.ramp
  return holding && index < PROFILE.steps.length ? String(PROFILE.steps[index]) : null
}
