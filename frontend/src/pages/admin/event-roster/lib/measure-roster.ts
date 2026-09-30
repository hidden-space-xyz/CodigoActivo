import type { ActivityMeasure, RosterLayout } from './paginate-roster'

const SHEET_WIDTH_MM = 210
const USABLE_HEIGHT_MM = 275
const CHUNK_GAP_MM = 5

function heightOf(element: Element | null): number {
  return element?.getBoundingClientRect().height ?? 0
}

function measureActivity(container: HTMLElement, index: number): ActivityMeasure | null {
  const section = container.querySelector<HTMLElement>(`[data-activity-index="${index}"]`)
  if (!section) return null
  return {
    headHeight: heightOf(section.querySelector('[data-part="head"]')),
    theadHeight: heightOf(section.querySelector('thead')),
    rowHeights: [...section.querySelectorAll('tbody tr')].map(heightOf),
  }
}

/**
 * Measures an off-screen A4 sheet holding every activity's whole table, to lay them out with
 * `paginateRoster`. Millimetres are converted with the sheet's rendered width.
 */
export function measureRoster(container: HTMLElement, activityCount: number): RosterLayout {
  const pxPerMm = container.getBoundingClientRect().width / SHEET_WIDTH_MM
  const sheetHead = heightOf(container.querySelector('[data-part="sheet-head"]'))
  return {
    capacity: USABLE_HEIGHT_MM * pxPerMm - sheetHead,
    gap: CHUNK_GAP_MM * pxPerMm,
    activities: Array.from({ length: activityCount }, (_, index) =>
      measureActivity(container, index),
    ),
  }
}
