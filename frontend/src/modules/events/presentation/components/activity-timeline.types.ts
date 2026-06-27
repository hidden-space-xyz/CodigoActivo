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
  start: Date | null
  end: Date | null
  roles: TimelineRole[]
  assignment: { status: string; roleName: string } | null
  household: TimelineMemberAssignment[]
}
