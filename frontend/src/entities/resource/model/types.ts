/** Kind of a learning resource; external ones link to another site instead of being read here. */
export interface ResourceType {
  readonly id: string
  readonly name: string
  readonly color: string
  readonly isExternal: boolean
}

/**
 * A learning resource as lists show it; `url` is set for external links and `null` for resources
 * read on the site.
 */
export interface LearningResourceSummary {
  readonly id: string
  readonly title: string
  readonly subtitle: string
  readonly type: ResourceType
  readonly url: string | null
  readonly createdAt: string
  readonly thumbnailId: string
}

/** A whole learning resource: its summary plus the rich-text description. */
export interface LearningResource extends LearningResourceSummary {
  readonly description: string
}

/**
 * Values a resource is created or replaced with: external types send only `url`, the others only
 * the rich-text `description`.
 */
export interface ResourceInput {
  readonly title: string
  readonly subtitle: string
  readonly description: string | null
  readonly url: string | null
  readonly resourceTypeId: string
  readonly thumbnailId: string
}

/** Filters, sort and page of the admin resource list. */
export interface ResourceListParams {
  readonly title?: string
  readonly subtitle?: string
  readonly resourceTypeId?: string
  readonly url?: string
  readonly createdFrom?: string
  readonly createdTo?: string
  readonly page?: number
  readonly pageSize?: number
  readonly sort?: string
}
