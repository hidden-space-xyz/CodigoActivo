<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

import { formatDate } from '@/shared/lib/date'
import { DataState } from '@/shared/ui/data-state'
import { LegalDocument } from '@/shared/ui/legal-document'
import { RichTextContent } from '@/shared/ui/rich-text-content'

import type { LegalPolicy } from '../model/types'

const props = defineProps<{
  /** Small uppercase kicker shown above the title. */
  eyebrow: string
  /** Page `<h1>`. */
  title: string
  /** Version in force; `null` while none was published, `undefined` until it loads. */
  policy: LegalPolicy | null | undefined
  /** Shows a spinner instead of the text. */
  loading: boolean
  /** Shows the load error instead of the text. */
  error: boolean
}>()

defineSlots<{
  /** Content shown before the text of the policy, such as the data controller. */
  default?: () => unknown
}>()

const { t } = useI18n()

const updated = computed(() =>
  props.policy
    ? t('entities.legalPolicy.updated', {
        date: formatDate(props.policy.publishedAt),
        version: props.policy.version,
      })
    : undefined,
)
</script>

<template>
  <LegalDocument :eyebrow="eyebrow" :title="title" :updated="updated">
    <slot />
    <DataState
      :loading="loading"
      :error="error"
      :empty="policy === null"
      :empty-text="$t('entities.legalPolicy.unpublished')"
    >
      <RichTextContent v-if="policy" class="legal-policy" :content="policy.content" />
    </DataState>
  </LegalDocument>
</template>

<style scoped>
.legal-policy {
  color: var(--ca-text-muted);
}

.legal-policy :deep(:where(h1, h2, h3)) {
  scroll-margin-top: 96px;
}

.legal-policy :deep(:where(h1, h2, h3):first-child) {
  margin-top: 0;
}

.legal-policy :deep(:where(a)) {
  color: var(--ca-orange-ink);
  font-weight: 600;
}

.legal-policy :deep(:where(strong)) {
  color: var(--ca-text);
}
</style>
