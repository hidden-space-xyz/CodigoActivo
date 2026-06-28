export interface ParentGrouping<T> {
  /** Items reordered so each parent is immediately followed by its descendants. */
  rows: T[]
  /** Depth of each item by id (0 for roots, 1 for their children, …). */
  depthById: Map<string, number>
  /** Direct children of each parent id, in their original order. */
  childrenByParent: Map<string, T[]>
}

/**
 * Groups a flat list into parent → children order using each item's id and parentId.
 * Handles arbitrary nesting depth, guards against parentId cycles, and never drops a
 * row: items whose parent is missing from the list are treated as roots, and any item
 * trapped in a cycle is still appended at the end.
 */
export function groupByParent<T>(
  items: readonly T[],
  getId: (item: T) => string | null | undefined,
  getParentId: (item: T) => string | null | undefined,
): ParentGrouping<T> {
  const ids = new Set(items.map(getId).filter((id): id is string => Boolean(id)))
  const childrenByParent = new Map<string, T[]>()
  const roots: T[] = []
  for (const item of items) {
    const parentId = getParentId(item)
    if (parentId && ids.has(parentId)) {
      const siblings = childrenByParent.get(parentId) ?? []
      siblings.push(item)
      childrenByParent.set(parentId, siblings)
    } else {
      roots.push(item)
    }
  }

  const rows: T[] = []
  const depthById = new Map<string, number>()
  const visited = new Set<string>()
  const visit = (item: T, depth: number): void => {
    const id = getId(item)
    if (id) {
      if (visited.has(id)) return // guard against parentId cycles
      visited.add(id)
      depthById.set(id, depth)
    }
    rows.push(item)
    for (const child of childrenByParent.get(id ?? '') ?? []) {
      visit(child, depth + 1)
    }
  }
  for (const root of roots) visit(root, 0)

  // Safety net: surface any item trapped in a cycle so no row is ever hidden.
  for (const item of items) {
    const id = getId(item)
    if (id && !visited.has(id)) rows.push(item)
  }

  return { rows, depthById, childrenByParent }
}
