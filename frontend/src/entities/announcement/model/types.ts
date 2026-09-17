/** Announcement as shown in list and home cards, mapped from `AnnouncementListItemResponse`. */
export interface AnnouncementSummary {
  readonly id: string
  readonly title: string
  readonly subtitle: string
  /** Creation date already formatted for display; `''` when unknown. */
  readonly date: string
  readonly thumbnailId: string
  readonly featured: boolean
}

/** Full announcement for the public detail page, mapped from `AnnouncementResponse`. */
export interface Announcement extends AnnouncementSummary {
  readonly description: string
  /** Raw ISO creation timestamp, unlike the formatted `date`. */
  readonly publishedAt: string | null
  readonly updatedAt: string | null
}

/** Home page split: the highlighted announcement plus the remaining latest ones. */
export interface HomeAnnouncements {
  readonly featured: AnnouncementSummary | null
  readonly items: readonly AnnouncementSummary[]
}
