export const REGISTRATION_ROLES = ['member', 'occasionalVolunteer', 'participant'] as const

export type RegistrationRole = (typeof REGISTRATION_ROLES)[number]

export function isRegistrationRole(value: string): value is RegistrationRole {
  return (REGISTRATION_ROLES as readonly string[]).includes(value)
}

export function roleRemindsAboutEvents(role: RegistrationRole): boolean {
  return role === 'occasionalVolunteer' || role === 'participant'
}

export const ROLE_LABELS: Record<RegistrationRole, string> = {
  member: 'Socio',
  occasionalVolunteer: 'Voluntario puntual',
  participant: 'Participante',
}

export const ROLE_DESCRIPTIONS: Record<RegistrationRole, string> = {
  member: 'Apoya a la asociación y forma parte de la comunidad de manera continua.',
  occasionalVolunteer: 'Echa una mano en eventos y talleres concretos cuando puedas.',
  participant: 'Únete a nuestras actividades y aprende a crear con código.',
}
