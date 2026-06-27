export const ADULT_AGE = 18

export function computeAge(birthDate: string, reference: Date = new Date()): number | null {
  if (birthDate === '') return null
  const birth = new Date(birthDate)
  if (Number.isNaN(birth.getTime())) return null

  let age = reference.getFullYear() - birth.getFullYear()
  const monthDelta = reference.getMonth() - birth.getMonth()
  if (monthDelta < 0 || (monthDelta === 0 && reference.getDate() < birth.getDate())) {
    age -= 1
  }
  return age
}

export function isMinor(birthDate: string, reference?: Date): boolean {
  const age = computeAge(birthDate, reference)
  return age !== null && age < ADULT_AGE
}
