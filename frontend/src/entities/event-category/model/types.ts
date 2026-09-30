/** A category events are classified with; `color` is a `#rrggbb` hex color. */
export interface EventCategory {
  readonly id: string
  readonly name: string
  readonly color: string
}

/** Values a category is created or updated with. */
export interface EventCategoryInput {
  readonly name: string
  readonly color: string
}

/** Filters, sort and page of the admin category list. */
export interface EventCategoryListParams {
  readonly name?: string
  readonly color?: string
  readonly page?: number
  readonly pageSize?: number
  readonly sort?: string
}
