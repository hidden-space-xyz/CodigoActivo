import { computed, ref } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { accountQueryKeys, getAccountCertificatesRequest } from '@/entities/account'
import type { AccountCertificate } from '@/entities/account'
import { useSession } from '@/entities/session'

import { downloadCertificatePdf, downloadCertificatePng } from './certificate-sheet'

export type CertificateFormat = 'png' | 'pdf'

export function useAccountCertificates() {
  const session = useSession()
  const userId = computed(() => session.user?.id ?? null)

  const certificates = useQuery({
    queryKey: accountQueryKeys.certificates(),
    queryFn: () => getAccountCertificatesRequest(),
    enabled: computed(() => userId.value !== null),
  })

  const entries = computed<readonly AccountCertificate[]>(() => certificates.data.value ?? [])

  const preview = ref<AccountCertificate | null>(null)
  const busy = ref(new Set<string>())

  function open(certificate: AccountCertificate): void {
    preview.value = certificate
  }

  function close(): void {
    preview.value = null
  }

  function isBusy(certificate: AccountCertificate | null, format: CertificateFormat): boolean {
    return certificate !== null && busy.value.has(`${certificateKey(certificate)}:${format}`)
  }

  async function download(
    certificate: AccountCertificate,
    format: CertificateFormat,
  ): Promise<void> {
    const key = `${certificateKey(certificate)}:${format}`
    if (busy.value.has(key)) return

    busy.value.add(key)
    try {
      if (format === 'pdf') await downloadCertificatePdf(certificate)
      else await downloadCertificatePng(certificate)
    } finally {
      busy.value.delete(key)
    }
  }

  return { certificates, entries, preview, isBusy, open, close, download }
}

export function certificateKey(certificate: AccountCertificate): string {
  return `${certificate.eventId}-${certificate.participantId}`
}
