/** Partner as the admin list shows and edits it; `fromDate` is a `YYYY-MM-DD` day. */
export interface Partner {
  readonly id: string
  readonly name: string
  readonly fromDate: string
  readonly tier: number
  readonly website: string | null
  readonly thumbnailId: string
}

/** Partner shown in the home page sponsors section; `website` and `thumbnailId` may be empty. */
export interface Sponsor {
  readonly id: string
  readonly name: string
  readonly website: string
  readonly thumbnailId: string
}

/** Values a partner is created or replaced with; `fromDate` is a `YYYY-MM-DD` day. */
export interface PartnerInput {
  readonly name: string
  readonly fromDate: string
  readonly tier: number
  readonly website: string | null
  readonly thumbnailId: string
}

/** Filters, sort and page of the admin partner list. */
export interface PartnerListParams {
  readonly name?: string
  readonly website?: string
  readonly tier?: number
  readonly fromDateFrom?: string
  readonly fromDateTo?: string
  readonly page?: number
  readonly pageSize?: number
  readonly sort?: string
}
