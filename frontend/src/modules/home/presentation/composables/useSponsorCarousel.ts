import { computed, onMounted, onUnmounted, ref, toValue, watch, type MaybeRefOrGetter } from 'vue'

import type { Sponsor } from '@/modules/home/domain/entities/sponsor.entity'

export interface SponsorCard {
  readonly sponsor: Sponsor
  readonly offset: number
}

const INTERVAL_MS = 3000

function circularOffset(index: number, active: number, total: number): number {
  let offset = index - active
  const half = total / 2
  if (offset > half) offset -= total
  else if (offset < -half) offset += total
  return offset
}

export function useSponsorCarousel(source: MaybeRefOrGetter<readonly Sponsor[] | undefined>) {
  const items = computed<readonly Sponsor[]>(() => toValue(source) ?? [])
  const count = computed(() => items.value.length)
  const activeIndex = ref(0)
  let timer: ReturnType<typeof setInterval> | null = null

  const cards = computed<SponsorCard[]>(() =>
    items.value.map((sponsor, index) => ({
      sponsor,
      offset: circularOffset(index, activeIndex.value, count.value),
    })),
  )

  function advance(step: number): void {
    const total = count.value
    if (total === 0) return
    activeIndex.value = (((activeIndex.value + step) % total) + total) % total
  }

  function stop(): void {
    if (timer !== null) {
      clearInterval(timer)
      timer = null
    }
  }

  function start(): void {
    stop()
    if (count.value > 1) timer = setInterval(() => advance(1), INTERVAL_MS)
  }

  function next(): void {
    advance(1)
    start()
  }
  function prev(): void {
    advance(-1)
    start()
  }
  function pause(): void {
    stop()
  }
  function resume(): void {
    start()
  }

  watch(count, start)

  onMounted(start)
  onUnmounted(stop)

  return { cards, next, prev, pause, resume, count }
}
