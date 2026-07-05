export interface LearningResourceSummary {
  readonly id: string
  readonly title: string
  readonly type: string
  readonly thumbnailId: string
}

export interface LearningResource extends LearningResourceSummary {
  readonly description: string
}
