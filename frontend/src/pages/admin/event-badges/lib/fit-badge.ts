const MIN_FIT = 0.6
const MAX_FIT = 1.5
const NAME_MIN_FIT = 0.45
const NAME_MAX_FIT = 1.6

function fitName(badge: HTMLElement, body: HTMLElement, name: HTMLElement | null): number {
  if (!name || name.scrollWidth === 0) return 1
  const setNameFit = (value: number): void => {
    badge.style.setProperty('--name-fit', value.toFixed(3))
  }
  setNameFit(1)
  const style = getComputedStyle(body)
  const available =
    body.clientWidth - Number.parseFloat(style.paddingLeft) - Number.parseFloat(style.paddingRight)
  let nameFit = Math.min(NAME_MAX_FIT, (available / name.scrollWidth) * 0.97)
  setNameFit(nameFit)
  for (let attempt = 0; attempt < 5 && name.scrollWidth > available; attempt += 1) {
    nameFit *= Math.min((available / name.scrollWidth) * 0.97, 0.97)
    setNameFit(nameFit)
  }
  return nameFit
}

/**
 * Scales a rendered badge so its content fills it without overflowing: the name grows or shrinks
 * to the badge width (`--name-fit`), then the rest of the body scales (`--fit`) by bisection,
 * shrinking the name further only when even the smallest scale overflows.
 */
function fitBadge(badge: HTMLElement): void {
  const body = badge.querySelector<HTMLElement>('.badge__body')
  if (!body) return

  const fits = (value: number): boolean => {
    badge.style.setProperty('--fit', value.toFixed(3))
    return body.scrollHeight <= body.clientHeight + 1
  }
  const setNameFit = (value: number): void => {
    badge.style.setProperty('--name-fit', value.toFixed(3))
  }

  const name = badge.querySelector<HTMLElement>('.badge__name')
  let nameFit = fitName(badge, body, name)

  if (fits(MAX_FIT)) return
  while (nameFit > 1 && !fits(1)) {
    nameFit = Math.max(1, nameFit * 0.9)
    setNameFit(nameFit)
  }
  let low = MIN_FIT
  let high = MAX_FIT
  for (let step = 0; step < 7; step += 1) {
    const middle = (low + high) / 2
    if (fits(middle)) low = middle
    else high = middle
  }
  while (!fits(low) && name && nameFit > NAME_MIN_FIT) {
    nameFit = Math.max(NAME_MIN_FIT, nameFit * 0.9)
    setNameFit(nameFit)
  }
}

/** Fits every badge rendered inside `root`. */
export function fitAllBadges(root: HTMLElement | null): void {
  for (const badge of root?.querySelectorAll<HTMLElement>('.badge') ?? []) fitBadge(badge)
}
