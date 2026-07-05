/** Shape shared by list reads — the slim list endpoint does not carry the description. */
export interface LearningResourceSummary {
  readonly id: string
  readonly title: string
  readonly type: string
  readonly thumbnailId: string
}

/** Full shape returned by the detail read. */
export interface LearningResource extends LearningResourceSummary {
  readonly description: string
}
