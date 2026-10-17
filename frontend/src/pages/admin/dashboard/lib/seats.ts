/** Share of the desired seats that are filled, as a whole percent; `null` when none is desired. */
export function seatPercent(filled: number, desired: number): number | null {
  return desired > 0 ? Math.round((filled / desired) * 100) : null
}
