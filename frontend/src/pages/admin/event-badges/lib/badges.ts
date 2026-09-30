import { hexLuminance, normalizeHexColor } from '@/shared/lib/color'

import type { Badge } from '../model/types'

/** Badges printed on one A4 sheet. */
const BADGES_PER_SHEET = 12

/** Activities listed on a badge before the rest are summed up as "and N more". */
const MAX_ACTIVITY_CHIPS = 6

/** Accent of badges whose user type has no color, or one too light to read on white. */
const FALLBACK_ACCENT = '#475569'

const MIN_READABLE_LUMINANCE = 0.82

/** Splits the badges into printed sheets of `perSheet` badges, keeping their order. */
export function toSheets(badges: readonly Badge[], perSheet = BADGES_PER_SHEET): Badge[][] {
  const sheets: Badge[][] = []
  for (let index = 0; index < badges.length; index += perSheet) {
    sheets.push(badges.slice(index, index + perSheet))
  }
  return sheets
}

/** Accent color of a badge: its user type color, or a slate when missing or too light. */
export function accentColor(badge: Badge): string {
  const hex = normalizeHexColor(badge.userTypeColor)
  if (!hex) return FALLBACK_ACCENT
  return hexLuminance(hex) > MIN_READABLE_LUMINANCE ? FALLBACK_ACCENT : hex
}

/** Activities listed on the badge. */
export function visibleActivities(badge: Badge): Badge['activities'] {
  return badge.activities.slice(0, MAX_ACTIVITY_CHIPS)
}

/** Activities left out of the badge, summed up in its last chip. */
export function hiddenActivityCount(badge: Badge): number {
  return Math.max(0, badge.activities.length - MAX_ACTIVITY_CHIPS)
}
