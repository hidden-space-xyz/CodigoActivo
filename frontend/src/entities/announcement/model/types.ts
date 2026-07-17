export interface AnnouncementSummary {
  readonly id: string
  readonly title: string
  readonly subtitle: string
  readonly date: string
  readonly thumbnailId: string
  readonly featured: boolean
}

export interface Announcement extends AnnouncementSummary {
  readonly description: string
  readonly publishedAt: string | null
  readonly updatedAt: string | null
}

export interface HomeAnnouncements {
  readonly featured: AnnouncementSummary | null
  readonly items: readonly AnnouncementSummary[]
}
