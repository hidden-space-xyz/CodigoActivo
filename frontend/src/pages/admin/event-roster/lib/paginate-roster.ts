import type { RosterActivity } from '../model/types'
import { type RoleRow, type RosterRow, rosterRows } from './roster-rows'

/** Rendered heights of one activity's table, in pixels. */
export interface ActivityMeasure {
  readonly headHeight: number
  readonly theadHeight: number
  /** Height of each row, in the order `rosterRows` gives them. */
  readonly rowHeights: readonly number[]
}

/** Room on a sheet and the measured heights of every activity, in pixels. */
export interface RosterLayout {
  /** Height available for activities on one sheet. */
  readonly capacity: number
  /** Space between two activities on the same sheet. */
  readonly gap: number
  /** Measures by activity index; `null` for an activity that was not rendered. */
  readonly activities: readonly (ActivityMeasure | null)[]
}

/** Part of an activity's table printed on one sheet; `continued` repeats its header. */
export interface SheetChunk {
  readonly activity: RosterActivity
  readonly rows: RosterRow[]
  readonly continued: boolean
}

/**
 * Lays the activities out on sheets from their measured heights. An activity starts on the current
 * sheet when its header and first row fit, otherwise on a new one; a table that overflows goes on
 * the next sheet, repeating its header and, above participants, the heading of their role. A role
 * heading never ends a sheet without its first participant. Activities without participants are
 * left out.
 */
export function paginateRoster(
  activities: readonly RosterActivity[],
  layout: RosterLayout,
): SheetChunk[][] {
  const sheets: SheetChunk[][] = []
  let sheet: SheetChunk[] = []
  let used = 0

  const closeSheet = (): void => {
    if (sheet.length) sheets.push(sheet)
    sheet = []
    used = 0
  }

  for (const [index, activity] of activities.entries()) {
    const measure = layout.activities[index]
    if (!measure) continue
    const headerHeight = measure.headHeight + measure.theadHeight
    let chunk: SheetChunk | null = null
    let lastRole: RoleRow | null = null
    let lastRoleHeight = 0

    for (const [rowIndex, row] of rosterRows(activity).entries()) {
      const rowHeight = measure.rowHeights[rowIndex] ?? 0
      const needed =
        row.kind === 'role' ? rowHeight + (measure.rowHeights[rowIndex + 1] ?? 0) : rowHeight
      if (!chunk) {
        const total = (sheet.length ? layout.gap : 0) + headerHeight + needed
        if (sheet.length && used + total > layout.capacity) closeSheet()
        if (sheet.length) used += layout.gap
        chunk = { activity, rows: [], continued: false }
        sheet.push(chunk)
        used += headerHeight
      } else if (used + needed > layout.capacity && chunk.rows.length) {
        closeSheet()
        chunk = { activity, rows: [], continued: true }
        sheet.push(chunk)
        used += headerHeight
        if (row.kind === 'participant' && lastRole) {
          chunk.rows.push({ ...lastRole, key: `${lastRole.key}:cont` })
          used += lastRoleHeight
        }
      }
      chunk.rows.push(row)
      used += rowHeight
      if (row.kind === 'role') {
        lastRole = row
        lastRoleHeight = rowHeight
      }
    }
  }
  closeSheet()

  return sheets
}
