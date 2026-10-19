import { computed } from 'vue'
import { useMutation, useQuery } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import { accountKeys, accountQueries, type AccountConsent } from '@/entities/account'
import { eventMutations } from '@/entities/event'
import { useSession } from '@/entities/session'
import { alsoInvalidates } from '@/shared/api'
import { useActionConfirm, useCrudFeedback } from '@/shared/lib/feedback'

/**
 * Consents section of the account: the consents decided for the user and their minors about the
 * documents of the events, and the withdrawal of the ones given, once confirmed. A withdrawal ends
 * the consent from then on and is reported with a toast. Call it in `setup`.
 */
export function useAccountConsents() {
  const { t } = useI18n()
  const session = useSession()
  const feedback = useCrudFeedback()
  const { confirmAction } = useActionConfirm()

  const consents = useQuery(() => ({
    ...accountQueries.consents(),
    enabled: session.user !== null,
  }))
  const withdrawal = useMutation(() =>
    alsoInvalidates(eventMutations.withdrawConsent(), accountKeys.consents()),
  )

  const entries = computed<readonly AccountConsent[]>(() => consents.data.value ?? [])

  /** Whether `consent` is being withdrawn right now. */
  function isWithdrawing(consent: AccountConsent): boolean {
    const target = withdrawal.variables.value
    return (
      withdrawal.isPending.value &&
      target?.eventId === consent.eventId &&
      target.userId === consent.personId &&
      target.termsDocumentId === consent.termsDocumentId
    )
  }

  /** Asks for confirmation and withdraws `consent`; a minor's own consent is theirs to withdraw. */
  function withdraw(consent: AccountConsent): void {
    const names = {
      document: consent.documentName,
      person: consent.personName,
      event: consent.eventTitle,
    }
    confirmAction({
      header: t('pages.account.consents.withdraw.header'),
      message:
        consent.personId === session.user?.id
          ? t('pages.account.consents.withdraw.messageSelf', names)
          : t('pages.account.consents.withdraw.messageMinor', names),
      acceptLabel: t('pages.account.consents.withdraw.confirm'),
      accept: () => {
        withdrawal.mutate(
          {
            eventId: consent.eventId,
            userId: consent.personId,
            termsDocumentId: consent.termsDocumentId,
          },
          feedback.outcome(t('pages.account.consents.withdraw.done')),
        )
      },
    })
  }

  return { consents, entries, isWithdrawing, withdraw }
}

/** Stable identity of a consent (event, person and document), usable as a list key. */
export function consentKey(consent: AccountConsent): string {
  return `${consent.eventId}__${consent.personId}__${consent.termsDocumentId}`
}
