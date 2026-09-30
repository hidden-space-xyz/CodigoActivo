import { onScopeDispose, ref } from 'vue'

/**
 * Seconds left of a countdown that ticks once per second until it reaches zero, e.g. before a
 * code can be resent. `start` restarts it, `stop` clears it at once, and it stops by itself when
 * the scope that created it ends.
 */
export function useCountdown(seconds: number) {
  const remaining = ref(0)
  let timer: ReturnType<typeof setInterval> | null = null

  function clear(): void {
    if (timer === null) return
    clearInterval(timer)
    timer = null
  }

  function start(): void {
    clear()
    const deadline = Date.now() + seconds * 1000
    const tick = (): void => {
      remaining.value = Math.max(0, Math.ceil((deadline - Date.now()) / 1000))
      if (remaining.value <= 0) clear()
    }
    tick()
    timer = setInterval(tick, 1000)
  }

  function stop(): void {
    clear()
    remaining.value = 0
  }

  onScopeDispose(clear)

  return { remaining, start, stop }
}
