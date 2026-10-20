/**
 * Element Plus tag type for a signup status, read from its Spanish name since statuses are a
 * catalog: confirmed signups show as success, rejected ones as danger and any other as info.
 */
export function signupStatusTagType(name: string): 'success' | 'danger' | 'info' {
  const value = name.toLowerCase()
  if (value.includes('confirm')) return 'success'
  if (value.includes('rechaz')) return 'danger'
  return 'info'
}
