/** One command of a table row. */
export interface RowAction {
  /** Stable key for rendering. */
  readonly key: string
  /** Menu text; accessible name and tooltip of an inline button. */
  readonly label: string
  /** AppIcon name. */
  readonly icon: string
  /** Runs when chosen; never for a disabled action. */
  readonly run: () => void
  /** Rendered as an icon button in the row instead of inside the "more" menu. */
  readonly inline?: boolean
  /** Shows the action but refuses to run it. */
  readonly disabled?: boolean
  /** Why it is unavailable, shown under a disabled menu item. */
  readonly reason?: string
  /** Destructive: red only on hover (inline) or as a red menu item. */
  readonly danger?: boolean
  /** Separator above the menu item. */
  readonly divided?: boolean
}
