import { onScopeDispose, watch } from 'vue'
import { useQueryClient } from '@tanstack/vue-query'

import { activityKeys } from '@/entities/activity'
import { eventKeys, nextSignupChange, type EventDetail } from '@/entities/event'

const LONGEST_TIMER_MS = 2 ** 31 - 1
const REFRESH_DELAY_MS = 2_000

/**
 * Refetches an event and its activities two seconds after its signup next opens or closes, so a
 * page left open shows the new stage without a reload. A change further away than a timer can
 * wait is reached in several waits, and the timer stops with the scope that created it.
 *
 * @param event - Getter of the loaded event; nothing is scheduled while it is missing.
 */
export function useSignupWindowRefresh(event: () => EventDetail | null | undefined): void {
  const queryClient = useQueryClient()
  let timer: ReturnType<typeof setTimeout> | undefined

  function clear(): void {
    clearTimeout(timer)
    timer = undefined
  }

  function refresh(eventId: string): void {
    void queryClient.invalidateQueries({ queryKey: eventKeys.detail(eventId) })
    void queryClient.invalidateQueries({ queryKey: activityKeys.ofEvent(eventId) })
    schedule()
  }

  function schedule(): void {
    clear()
    const current = event()
    if (!current) return
    const change = nextSignupChange(current, Date.now())
    if (change === null) return
    const delay = change + REFRESH_DELAY_MS - Date.now()
    timer =
      delay > LONGEST_TIMER_MS
        ? setTimeout(schedule, LONGEST_TIMER_MS)
        : setTimeout(() => {
            refresh(current.id)
          }, delay)
  }

  watch(event, schedule, { immediate: true })
  onScopeDispose(clear)
}
