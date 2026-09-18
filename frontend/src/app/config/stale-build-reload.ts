const RELOAD_MARK = 'ca:stale-build-reload'
const RELOAD_COOLDOWN_MS = 60_000

function reloadedRecently(storage: Storage, now: number): boolean {
  return now - Number(storage.getItem(RELOAD_MARK)) < RELOAD_COOLDOWN_MS
}

/**
 * Recovers a tab left open across a deployment. Its `index.html` still names the previous build's
 * hashed chunks, which the new image no longer serves, so a lazy route fails with Vite's
 * `vite:preloadError`. The page is reloaded once to pick up the current `index.html`. Being offline, a
 * second failure within a minute or unavailable session storage leave the error to surface instead.
 */
export function registerStaleBuildReload(target: Window = window): void {
  target.addEventListener('vite:preloadError', (event) => {
    if (!target.navigator.onLine) return
    try {
      const now = Date.now()
      if (reloadedRecently(target.sessionStorage, now)) return
      target.sessionStorage.setItem(RELOAD_MARK, String(now))
    } catch {
      return
    }
    event.preventDefault()
    target.location.reload()
  })
}
