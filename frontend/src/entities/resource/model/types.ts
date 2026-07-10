export interface LearningResourceSummary {
  readonly id: string
  readonly title: string
  readonly subtitle: string
  readonly typeName: string
  readonly typeColor: string
  readonly url: string | null
  readonly thumbnailId: string
}

export interface LearningResource extends LearningResourceSummary {
  readonly description: string
}
