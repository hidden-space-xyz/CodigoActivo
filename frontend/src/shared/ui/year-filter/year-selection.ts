/**
 * Year a year filter keeps selected once its list of years loads or changes: the current one while
 * it is still listed, otherwise the first (newest) year, or `''` when the list is empty.
 */
export function yearSelection(years: readonly string[], selected: string): string {
  return years.includes(selected) ? selected : (years[0] ?? '')
}
