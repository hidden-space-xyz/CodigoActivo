import { readonly, ref, type Ref } from 'vue'

const mediaQueries = new Map<string, Readonly<Ref<boolean>>>()

/** Readonly ref tracking a CSS media query; one shared listener per query string, never removed. */
export function useMediaQuery(query: string): Readonly<Ref<boolean>> {
  const cached = mediaQueries.get(query)
  if (cached) return cached

  const list = window.matchMedia(query)
  const matches = ref(list.matches)
  list.addEventListener('change', (event) => {
    matches.value = event.matches
  })

  const result = readonly(matches)
  mediaQueries.set(query, result)
  return result
}
