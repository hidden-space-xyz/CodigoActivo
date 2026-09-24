/** News item as shown in list and home cards, mapped from `NewsListItemResponse`. */
export interface NewsSummary {
  readonly id: string
  readonly title: string
  readonly subtitle: string
  /** Creation date already formatted for display; `''` when unknown. */
  readonly date: string
  readonly thumbnailId: string
  readonly featured: boolean
}

/** Full news item for the public detail page, mapped from `NewsItemResponse`. */
export interface NewsItem extends NewsSummary {
  readonly description: string
  /** Raw ISO creation timestamp, unlike the formatted `date`. */
  readonly publishedAt: string | null
  readonly updatedAt: string | null
}

/** Home page split: the highlighted news item plus the remaining latest ones. */
export interface HomeNews {
  readonly featured: NewsSummary | null
  readonly items: readonly NewsSummary[]
}
