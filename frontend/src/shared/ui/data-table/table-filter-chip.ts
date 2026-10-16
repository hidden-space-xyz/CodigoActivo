/** A page-owned filter shown as a removable chip. */
export interface TableFilterChip {
  /** Stable key for rendering. */
  readonly key: string
  /** Full chip text. */
  readonly label: string
  /** Clears it; also run by «Limpiar filtros». */
  readonly remove: () => void
}
