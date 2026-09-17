import { defineComponent, h, nextTick, ref, type Ref } from 'vue'
import { mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { Sponsor } from '@/entities/partner'
import { useSponsorCarousel } from '@/pages/home/model/useSponsorCarousel'

function sponsors(count: number): Sponsor[] {
  return Array.from({ length: count }, (_, index) => ({
    id: `s${index}`,
    name: `Sponsor ${index}`,
    website: '',
    thumbnailId: '',
  }))
}

function mountCarousel(source: Ref<readonly Sponsor[] | undefined>) {
  let carousel: ReturnType<typeof useSponsorCarousel> | undefined
  const wrapper = mount(
    defineComponent({
      setup() {
        carousel = useSponsorCarousel(source)
        return () => h('div')
      },
    }),
  )
  if (!carousel) throw new Error('carousel not created')
  return { carousel, wrapper }
}

function offsets(carousel: ReturnType<typeof useSponsorCarousel>): number[] {
  return carousel.cards.value.map((card) => card.offset)
}

describe('useSponsorCarousel', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  it('has no cards while the sponsors are not loaded and ignores navigation', () => {
    const { carousel } = mountCarousel(ref(undefined))

    carousel.next()
    carousel.prev()

    expect(carousel.cards.value).toEqual([])
    expect(vi.getTimerCount()).toBe(0)
  })

  it('assigns wrapped signed offsets around the first sponsor', () => {
    const { carousel } = mountCarousel(ref(sponsors(5)))

    expect(offsets(carousel)).toEqual([0, 1, 2, -2, -1])
    expect(carousel.cards.value[0]?.sponsor.id).toBe('s0')
  })

  it('auto-advances every 3 seconds, wrapping around the ring', () => {
    const { carousel } = mountCarousel(ref(sponsors(3)))

    vi.advanceTimersByTime(3000)
    expect(offsets(carousel)).toEqual([-1, 0, 1])

    vi.advanceTimersByTime(6000)
    expect(offsets(carousel)).toEqual([0, 1, -1])
  })

  it('does not auto-advance a single sponsor', () => {
    const { carousel } = mountCarousel(ref(sponsors(1)))

    vi.advanceTimersByTime(9000)

    expect(offsets(carousel)).toEqual([0])
    expect(vi.getTimerCount()).toBe(0)
  })

  it('moves with next and prev and restarts the timer', () => {
    const { carousel } = mountCarousel(ref(sponsors(4)))

    vi.advanceTimersByTime(2000)
    carousel.prev()
    expect(offsets(carousel)).toEqual([1, -2, -1, 0])

    vi.advanceTimersByTime(2000)
    expect(offsets(carousel)).toEqual([1, -2, -1, 0])

    carousel.next()
    carousel.next()
    expect(offsets(carousel)).toEqual([-1, 0, 1, 2])
    expect(vi.getTimerCount()).toBe(1)
  })

  it('stops while paused and continues after resume', () => {
    const { carousel } = mountCarousel(ref(sponsors(3)))

    carousel.pause()
    vi.advanceTimersByTime(9000)
    expect(offsets(carousel)).toEqual([0, 1, -1])

    carousel.resume()
    vi.advanceTimersByTime(3000)
    expect(offsets(carousel)).toEqual([-1, 0, 1])
  })

  it('starts rotating once the sponsors arrive', async () => {
    const source = ref<readonly Sponsor[] | undefined>(undefined)
    const { carousel } = mountCarousel(source)
    expect(vi.getTimerCount()).toBe(0)

    source.value = sponsors(2)
    await nextTick()
    vi.advanceTimersByTime(3000)

    expect(offsets(carousel)).toEqual([-1, 0])
  })

  it('stops the timer on unmount', () => {
    const { wrapper } = mountCarousel(ref(sponsors(3)))
    expect(vi.getTimerCount()).toBe(1)

    wrapper.unmount()

    expect(vi.getTimerCount()).toBe(0)
  })
})
