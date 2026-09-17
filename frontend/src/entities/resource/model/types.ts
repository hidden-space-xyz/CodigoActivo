/** Card model for a learning resource; `url` is set for external links, `null` otherwise. */
export interface LearningResourceSummary {
  readonly id: string
  readonly title: string
  readonly subtitle: string
  readonly date: string
  readonly url: string | null
  readonly thumbnailId: string
}

/** Detail page model for a resource, adding the long description. */
export interface LearningResource extends LearningResourceSummary {
  readonly description: string
}
