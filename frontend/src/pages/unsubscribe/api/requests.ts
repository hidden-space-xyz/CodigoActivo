import { postApiEmailsUnsubscribe } from '@/shared/api/generated/endpoints/emails/emails'

/**
 * Withdraws the promotional consent of the account an emailed unsubscribe link names
 * (`POST /api/emails/unsubscribe`). It needs no session.
 */
export async function unsubscribeRequest(token: string): Promise<void> {
  await postApiEmailsUnsubscribe({ token })
}
