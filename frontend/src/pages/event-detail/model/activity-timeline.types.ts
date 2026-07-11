export interface TimelineRole {
  id: string
  name: string
}

export interface TimelineMemberAssignment {
  userId: string
  name: string
  roleName: string
  status: string
}

export interface TimelineActivity {
  id: string
  title: string
  description: string
  location: string
  modality: string
  start: Date | null
  end: Date | null
  highDemandRoleIds: string[]
  assignment: { status: string; roleName: string } | null
  household: TimelineMemberAssignment[]
}
