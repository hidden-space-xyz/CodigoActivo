import {
  computed,
  inject,
  onBeforeUnmount,
  provide,
  shallowRef,
  type ComputedRef,
  type InjectionKey,
} from 'vue'

interface TableFilterDescription {
  readonly label: string
  readonly value: string
}

interface ActiveTableFilter extends TableFilterDescription {
  readonly id: symbol
  readonly clear: () => void
}

interface TableFilterEntry {
  readonly id: symbol
  readonly description: ComputedRef<TableFilterDescription | null>
  readonly clear: () => void
}

interface TableFilterRegistry {
  readonly register: (entry: TableFilterEntry) => void
  readonly unregister: (id: symbol) => void
}

const TABLE_FILTERS: InjectionKey<TableFilterRegistry> = Symbol('table-filters')

/**
 * Registers every column filter rendered inside the calling component (DataTable) and lists the
 * active ones, in mount order, with a readable value and the filter's own clear action.
 */
export function provideTableFilters(): {
  readonly active: ComputedRef<readonly ActiveTableFilter[]>
} {
  const entries = shallowRef<readonly TableFilterEntry[]>([])

  provide(TABLE_FILTERS, {
    register: (entry) => {
      entries.value = [...entries.value, entry]
    },
    unregister: (id) => {
      entries.value = entries.value.filter((entry) => entry.id !== id)
    },
  })

  const active = computed<readonly ActiveTableFilter[]>(() =>
    entries.value.flatMap((entry) => {
      const description = entry.description.value
      return description
        ? [{ id: entry.id, label: description.label, value: description.value, clear: entry.clear }]
        : []
    }),
  )

  return { active }
}

/**
 * Registers the calling filter component with the nearest `provideTableFilters`; `describe`
 * returns null while the filter is inactive. No-op outside a provider. The table only updates when
 * the described text changes, so props rebuilt on every render (such as an inline options array)
 * cannot make the table and its toolbar filters re-render each other endlessly.
 */
export function useTableFilterRegistration(
  describe: () => TableFilterDescription | null,
  clear: () => void,
): void {
  const registry = inject(TABLE_FILTERS, null)
  if (!registry) return

  const description = computed<TableFilterDescription | null>((previous) => {
    const next = describe()
    const unchanged = previous && next?.label === previous.label && next.value === previous.value
    return unchanged ? previous : next
  })
  const id = Symbol('table-filter')
  registry.register({ id, description, clear })
  onBeforeUnmount(() => registry.unregister(id))
}
