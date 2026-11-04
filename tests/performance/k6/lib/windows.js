import exec from 'k6/execution'
import { PROFILE } from './profiles.js'
import { RUN_PHASE } from './thresholds.js'

export function inRun() {
  return exec.vu.metrics.tags.phase === RUN_PHASE
}

export function currentWindow() {
  if (!PROFILE.windows.length || !inRun()) {
    return null
  }
  const elapsed = (Date.now() - exec.scenario.startTime) / 1000
  return PROFILE.windows.find(({ from, to }) => elapsed >= from && elapsed < to)?.name ?? null
}
