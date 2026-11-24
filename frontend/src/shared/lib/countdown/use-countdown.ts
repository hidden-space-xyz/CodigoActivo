import { ref } from 'vue'
import { useIntervalFn } from '@vueuse/core'

/**
 * Seconds left of a countdown that ticks once per second until it reaches zero, e.g. before a
 * code can be resent. `start` restarts it, `stop` clears it at once, and it stops by itself when
 * the scope that created it ends.
 */
export function useCountdown(seconds: number) {
  const remaining = ref(0)
  let deadline = 0
  const { pause, resume } = useIntervalFn(tick, 1000, {
    immediate: false,
    immediateCallback: true,
  })

  function tick(): void {
    remaining.value = Math.max(0, Math.ceil((deadline - Date.now()) / 1000))
    if (remaining.value <= 0) pause()
  }

  function start(): void {
    deadline = Date.now() + seconds * 1000
    resume()
  }

  function stop(): void {
    pause()
    remaining.value = 0
  }

  return { remaining, start, stop }
}
