/** A news item as lists and home cards show it; `createdAt` is when it was published. */
export interface NewsSummary {
  readonly id: string
  readonly title: string
  readonly subtitle: string
  readonly createdAt: string
  readonly thumbnailId: string
  readonly featured: boolean
}

/** A whole news item: its summary plus the rich-text description and its last change. */
export interface NewsItem extends NewsSummary {
  readonly description: string
  readonly updatedAt: string | null
}

/** Home page split: the highlighted news item plus the remaining latest ones. */
export interface HomeNews {
  readonly featured: NewsSummary | null
  readonly items: readonly NewsSummary[]
}

/** Values a news item is created or replaced with; `description` is a rich-text document. */
export interface NewsItemInput {
  readonly title: string
  readonly subtitle: string
  readonly description: string
  readonly thumbnailId: string
}

/** Filters, sort and page of the admin news list. */
export interface NewsListParams {
  readonly title?: string
  readonly subtitle?: string
  readonly createdFrom?: string
  readonly createdTo?: string
  readonly page?: number
  readonly pageSize?: number
  readonly sort?: string
}
