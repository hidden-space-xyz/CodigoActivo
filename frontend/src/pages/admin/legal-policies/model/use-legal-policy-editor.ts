import { computed, ref, watch } from 'vue'
import { useMutation, useQuery } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import {
  legalPolicyMutations,
  legalPolicyQueries,
  type LegalPolicy,
  type LegalPolicyKind,
} from '@/entities/legal-policy'
import { useActionConfirm, useCrudFeedback } from '@/shared/lib/feedback'
import { useForm } from '@/shared/lib/form'

import {
  isSameLegalPolicyContent,
  readLegalPolicyDraft,
  toLegalPolicyDraft,
} from './legal-policy-form'

/**
 * Editor of one legal policy: the draft starts from the version in force and follows it while it is
 * unchanged, publishing asks for confirmation and starts a new version, and any earlier version can
 * be read and loaded back into the draft. Call it in `setup`.
 */
export function useLegalPolicyEditor(kind: LegalPolicyKind) {
  const { t } = useI18n()
  const feedback = useCrudFeedback()
  const { confirmAction } = useActionConfirm()

  const current = useQuery(legalPolicyQueries.current(kind))
  const versions = useQuery(legalPolicyQueries.versions(kind))
  const publication = useMutation(legalPolicyMutations.publish())
  const form = useForm({ initial: () => toLegalPolicyDraft(null), read: readLegalPolicyDraft })

  const viewing = ref<number | null>(null)
  const viewed = useQuery(() => ({
    ...legalPolicyQueries.version(kind, viewing.value ?? 0),
    enabled: viewing.value !== null,
  }))

  const dirty = computed(
    () => !isSameLegalPolicyContent(form.draft.content, current.data.value?.content ?? ''),
  )

  watch(
    () => current.data.value,
    (policy, previous: LegalPolicy | null | undefined) => {
      if (policy === undefined) return
      const untouched =
        previous === undefined ||
        isSameLegalPolicyContent(form.draft.content, previous?.content ?? '')
      if (untouched) form.reset(toLegalPolicyDraft(policy))
    },
    { immediate: true },
  )

  function discard(): void {
    form.reset(toLegalPolicyDraft(current.data.value))
  }

  function publish(): void {
    const value = form.submit()
    if (!value) return
    const policy = current.data.value
    confirmAction({
      header: t('pages.admin.legalPolicies.publish.header'),
      message: policy
        ? t('pages.admin.legalPolicies.publish.message', { version: policy.version + 1 })
        : t('pages.admin.legalPolicies.publish.firstMessage'),
      acceptLabel: t('pages.admin.legalPolicies.publish.accept'),
      accept: () => {
        publication.mutate(
          { kind, content: value.content },
          feedback.outcome(t('pages.admin.legalPolicies.toasts.published'), discard),
        )
      },
    })
  }

  function view(version: number): void {
    viewing.value = version
  }

  function closeVersion(): void {
    viewing.value = null
  }

  function restore(policy: LegalPolicy): void {
    form.draft.content = policy.content
    closeVersion()
  }

  return {
    policy: current.data,
    loading: current.isLoading,
    loadFailed: current.isError,
    versions: computed(() => versions.data.value ?? []),
    versionsLoading: versions.isLoading,
    versionsFailed: versions.isError,
    form,
    dirty,
    publishing: publication.isPending,
    publish,
    discard,
    viewing,
    viewedPolicy: viewed.data,
    viewedLoading: viewed.isLoading,
    viewedFailed: viewed.isError,
    view,
    closeVersion,
    restore,
  }
}
