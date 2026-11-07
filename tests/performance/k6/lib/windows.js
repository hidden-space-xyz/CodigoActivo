import exec from 'k6/execution'
import { Counter } from 'k6/metrics'
import { PROFILE } from './profiles.js'
import { RUN_PHASE } from './thresholds.js'

const arrivals = new Counter('arrivals')

/** Tells a scenario iteration from setup, whose requests stay out of the results. */
export function inRun() {
  return exec.vu.metrics.tags.phase === RUN_PHASE
}

/** Names the window of the profile the clock is in, if any. */
export function currentWindow() {
  if (!PROFILE.windows.length || !inRun()) {
    return null
  }
  const elapsed = (Date.now() - exec.scenario.startTime) / 1000
  return PROFILE.windows.find(({ from, to }) => elapsed >= from && elapsed < to)?.name ?? null
}

/** Counts a started iteration, so a window can be compared with the load it promises. */
export function countArrival() {
  const window = currentWindow()
  if (window) {
    arrivals.add(1, { window })
  }
}
