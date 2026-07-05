/** Shape shared by list reads — the slim list endpoint does not carry the description. */
export interface AnnouncementSummary {
  readonly id: string
  readonly title: string
  readonly subtitle: string
  readonly date: string
  readonly thumbnailId: string
  readonly featured: boolean
}

/** Full shape returned by the detail read. */
export interface Announcement extends AnnouncementSummary {
  readonly description: string
}

export interface HomeAnnouncements {
  readonly featured: AnnouncementSummary | null
  readonly items: readonly AnnouncementSummary[]
}
