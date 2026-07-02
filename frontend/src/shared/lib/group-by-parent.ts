interface ParentGrouping<T> {
  rows: T[]
  depthById: Map<string, number>
  childrenByParent: Map<string, T[]>
}

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
      if (visited.has(id)) return
      visited.add(id)
      depthById.set(id, depth)
    }
    rows.push(item)
    for (const child of childrenByParent.get(id ?? '') ?? []) {
      visit(child, depth + 1)
    }
  }
  for (const root of roots) visit(root, 0)

  for (const item of items) {
    const id = getId(item)
    if (id && !visited.has(id)) rows.push(item)
  }

  return { rows, depthById, childrenByParent }
}
