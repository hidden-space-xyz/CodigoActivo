export interface Announcement {
  readonly id: string
  readonly title: string
  readonly subtitle: string
  readonly description: string
  readonly date: string
  readonly thumbnailId: string
  readonly featured: boolean
}

/** Home announcements block: one featured item plus up to three other recent ones. */
export interface HomeAnnouncements {
  readonly featured: Announcement | null
  readonly items: readonly Announcement[]
}
