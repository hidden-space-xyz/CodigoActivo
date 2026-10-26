import { useMutation, useQuery } from '@tanstack/vue-query'
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'

import { useSession } from '@/entities/session'
import { hasErrorCode } from '@/shared/api'
import { BLOCKED_DATA_MIN_SEARCH_LENGTH } from '@/shared/config'
import { toDateOnly } from '@/shared/lib/date'
import { downloadBlob } from '@/shared/lib/download'
import { getErrorMessage, useCrudFeedback } from '@/shared/lib/feedback'
import { useServerTable } from '@/shared/lib/paging'

import { blockedDataMutations, blockedDataQueries, consultationList } from '../api/queries'
import type { Candidate, Consultation, ConsultationRequest } from './types'

/** Errors after which the open consultation can no longer be used. */
const CLOSING_ERRORS = [
  'BlockedDataConsultationExpired',
  'BlockedDataConsultationNotFound',
] as const

/**
 * Blocked data page: every administrator sees the list of consultations, newest first, without
 * any of their data; the account designated for data protection opens one for a request, looks
 * accounts up in it and downloads an extract about the account it picks, narrowed to a range of
 * days and to kinds of data. Opening takes two steps: the request and the password, which email a
 * code unless the account reads it from an authenticator application, and then that code; closing
 * the dialog forgets the request and its password. A consultation that ended is closed with its
 * error. Call it in `setup`.
 */
export function useBlockedData() {
  const { t } = useI18n()
  const feedback = useCrudFeedback()
  const session = useSession()
  const access = useQuery(blockedDataQueries.access())
  const table = useServerTable({
    ...consultationList,
    defaultSort: { field: 'openedAt', order: -1 },
  })

  const dialogVisible = ref(false)
  const openError = ref('')
  const pendingRequest = ref<ConsultationRequest | null>(null)
  const consultation = ref<Consultation | null>(null)
  const query = ref('')
  const candidates = ref<Candidate[]>([])
  const searched = ref(false)
  const accountId = ref<string | null>(null)
  const range = ref<(Date | null)[] | null>(null)
  const entities = ref<string[]>([])

  const isOfficer = computed(() => access.data.value?.isPrivacyOfficer === true)
  const usesAuthenticator = computed(() => session.user?.twoFactorMethod === 'Authenticator')
  const email = computed(() => session.user?.email ?? '')
  const availableEntities = computed(() => access.data.value?.entities ?? [])

  function closeOn(error: unknown): void {
    if (CLOSING_ERRORS.some((code) => hasErrorCode(error, code))) closeConsultation()
    feedback.error(error)
  }

  function showOpenError(error: unknown): void {
    openError.value = getErrorMessage(error)
  }

  const requestingCode = useMutation({
    ...blockedDataMutations.requestCode(),
    onError: showOpenError,
  })

  const opening = useMutation({
    ...blockedDataMutations.open(),
    onSuccess: (opened) => {
      consultation.value = opened
      query.value = ''
      candidates.value = []
      searched.value = false
      accountId.value = null
      range.value = null
      entities.value = []
      dialogVisible.value = false
    },
    onError: showOpenError,
  })

  const searching = useMutation({
    ...blockedDataMutations.search(),
    onSuccess: (found) => {
      candidates.value = found
      searched.value = true
      accountId.value = found.length === 1 ? (found[0]?.accountId ?? null) : null
    },
    onError: closeOn,
  })

  const extracting = useMutation({
    ...blockedDataMutations.extract(t),
    onSuccess: (file) => {
      downloadBlob(
        t('pages.admin.blockedData.extract.fileName', { date: toDateOnly(new Date()) }),
        file,
      )
    },
    onError: closeOn,
  })

  watch(dialogVisible, (visible) => {
    if (!visible) pendingRequest.value = null
  })

  /** Shows the dialog that opens a consultation at its first step, without any earlier error. */
  function startConsultation(): void {
    openError.value = ''
    dialogVisible.value = true
  }

  /** Moves on to the code of the request the dialog read, emailing it unless an app shows it. */
  function continueConsultation(request: ConsultationRequest): void {
    openError.value = ''
    if (usesAuthenticator.value) {
      pendingRequest.value = request
      return
    }
    requestingCode.mutate(request.currentPassword, {
      onSuccess: () => {
        pendingRequest.value = request
      },
    })
  }

  /** Emails another code for the request that awaits it. */
  function resendCode(): void {
    if (!pendingRequest.value) return
    openError.value = ''
    requestingCode.mutate(pendingRequest.value.currentPassword)
  }

  /** Opens the consultation that awaits its code. */
  function openConsultation(code: string): void {
    if (!pendingRequest.value) return
    openError.value = ''
    opening.mutate({ ...pendingRequest.value, code })
  }

  /** Looks the typed text up in the open consultation. */
  function search(): void {
    const text = query.value.trim()
    if (!consultation.value || text.length < BLOCKED_DATA_MIN_SEARCH_LENGTH) return
    searching.mutate({ consultationId: consultation.value.id, query: text })
  }

  /** Downloads an extract about the picked account, narrowed as chosen. */
  function downloadExtract(): void {
    if (!consultation.value || !accountId.value) return
    const [from, to] = range.value ?? []
    extracting.mutate({
      consultationId: consultation.value.id,
      input: {
        accountId: accountId.value,
        from: from ? toDateOnly(from) : null,
        to: to ? toDateOnly(to) : null,
        entities: entities.value,
      },
    })
  }

  /** Forgets the open consultation and what it found. */
  function closeConsultation(): void {
    consultation.value = null
    candidates.value = []
    searched.value = false
    accountId.value = null
  }

  return {
    table,
    isOfficer,
    usesAuthenticator,
    email,
    availableEntities,
    dialogVisible,
    codeStep: computed(() => pendingRequest.value !== null),
    openError,
    sendingCode: computed(() => requestingCode.isPending.value),
    opening: computed(() => opening.isPending.value),
    consultation,
    query,
    candidates,
    searched,
    searching: computed(() => searching.isPending.value),
    accountId,
    range,
    entities,
    extracting: computed(() => extracting.isPending.value),
    startConsultation,
    continueConsultation,
    resendCode,
    openConsultation,
    search,
    downloadExtract,
    closeConsultation,
  }
}
