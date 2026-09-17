/** Smooth-scrolls the window to the top, jumping instantly on browsers without scroll options. */
export function scrollToTop(): void {
  try {
    window.scrollTo({ top: 0, behavior: 'smooth' })
  } catch {
    window.scrollTo(0, 0)
  }
}
