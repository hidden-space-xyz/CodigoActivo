import { useMutation } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'

import { accountMutations } from '@/entities/account'
import { toDateOnly } from '@/shared/lib/date'
import { downloadBlob } from '@/shared/lib/download'
import { useCrudFeedback } from '@/shared/lib/feedback'
import { absoluteUrl } from '@/shared/lib/seo'

/**
 * Downloads the copy of the signed-in user's personal data as a JSON file named after today's
 * date, linking the privacy policy page. A failed request is reported and saves nothing.
 */
export function usePersonalDataDownload() {
  const { t } = useI18n()
  const router = useRouter()
  const feedback = useCrudFeedback()

  const mutation = useMutation({
    ...accountMutations.personalData(
      absoluteUrl(router.resolve({ name: 'privacy-policy' }).href),
      t,
    ),
    onSuccess: (file) => {
      downloadBlob(t('pages.account.personalData.fileName', { date: toDateOnly(new Date()) }), file)
    },
    onError: (error) => {
      feedback.error(error)
    },
  })

  function download(): void {
    if (!mutation.isPending.value) mutation.mutate()
  }

  return { download, isDownloading: mutation.isPending }
}
