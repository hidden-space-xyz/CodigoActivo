export interface TimelineRole {
  id: string
  name: string
}

export interface TimelineActivity {
  id: string
  title: string
  start: Date | null
  end: Date | null
  roles: TimelineRole[]
  assignment: { status: string; roleName: string } | null
}
