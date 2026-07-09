import { computed, toValue } from 'vue'
import type { ComputedRef, MaybeRefOrGetter } from 'vue'
import { groupByParent } from './group-by-parent'

export interface HierarchyFilterOptions<T> {
  items: MaybeRefOrGetter<readonly T[]>
  getId: (item: T) => string | null | undefined
  getParentId: (item: T) => string | null | undefined
  getName: (item: T) => string
  matches: (item: T) => boolean
  filterActive: MaybeRefOrGetter<boolean>
  sortActive: MaybeRefOrGetter<boolean>
}

export interface HierarchyFilter<T> {
  rows: ComputedRef<T[]>
  treeMode: ComputedRef<boolean>
  depthOf: (item: T) => number
  isChild: (item: T) => boolean
  childCountOf: (item: T) => number
  parentName: (parentId: string | null | undefined) => string
}

export function useHierarchyFilter<T>(options: HierarchyFilterOptions<T>): HierarchyFilter<T> {
  const grouped = computed(() =>
    groupByParent(toValue(options.items), options.getId, options.getParentId),
  )

  const nameById = computed(() => {
    const map = new Map<string, string>()
    for (const item of toValue(options.items)) {
      const id = options.getId(item)
      if (id) map.set(id, options.getName(item))
    }
    return map
  })

  const filterActive = computed(() => toValue(options.filterActive))
  const sortActive = computed(() => toValue(options.sortActive))

  const rows = computed<T[]>(() =>
    filterActive.value
      ? toValue(options.items).filter((item) => options.matches(item))
      : grouped.value.rows,
  )

  const treeMode = computed(() => !filterActive.value && !sortActive.value)

  function depthOf(item: T): number {
    const id = options.getId(item)
    if (!id) return 0
    return grouped.value.depthById.get(id) ?? 0
  }

  function isChild(item: T): boolean {
    return depthOf(item) > 0
  }

  function childCountOf(item: T): number {
    const id = options.getId(item)
    if (!id) return 0
    return grouped.value.childrenByParent.get(id)?.length ?? 0
  }

  function parentName(parentId: string | null | undefined): string {
    if (!parentId) return '—'
    return nameById.value.get(parentId) ?? '—'
  }

  return { rows, treeMode, depthOf, isChild, childCountOf, parentName }
}
