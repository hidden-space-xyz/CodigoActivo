/** Label/value pair for select inputs. */
export interface SelectOption {
  readonly label: string
  readonly value: string
}

/** Maps catalog `{ id, name }` items to select options; none while the catalog is not loaded. */
export function toSelectOptions(
  items?: readonly { readonly id: string; readonly name: string }[],
): SelectOption[] {
  return (items ?? []).map((item) => ({ label: item.name, value: item.id }))
}
