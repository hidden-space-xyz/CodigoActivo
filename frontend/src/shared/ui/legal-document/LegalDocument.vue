<script setup lang="ts">
import { PageHead } from '@/shared/ui/page-head'

defineProps<{
  /** Small uppercase kicker shown above the title. */
  eyebrow: string
  /** Page `<h1>`. */
  title: string
  /** Summary paragraph below the title; omitted when unset. */
  lead?: string | undefined
  /** Line telling when the document last changed; omitted when unset. */
  updated?: string | undefined
}>()
</script>

<template>
  <div>
    <PageHead :eyebrow="eyebrow" :title="title">
      <p v-if="lead" class="legal-document__lead">{{ lead }}</p>
      <p v-if="updated" class="legal-document__updated">{{ updated }}</p>
    </PageHead>

    <div class="legal-document">
      <div class="ca-container--narrow">
        <div class="legal-document__body">
          <slot />
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.legal-document__lead {
  max-width: 680px;
  margin-top: 14px;
  font-size: 17px;
  line-height: 1.6;
  color: var(--ca-text-muted);
}

.legal-document__updated {
  margin-top: 12px;
  font-family: var(--ca-font-mono);
  font-size: 13px;
  color: var(--ca-text-dim);
}

.legal-document {
  padding: 24px var(--ca-gutter) 80px;
}

.legal-document__body {
  max-width: 780px;
  display: flex;
  flex-direction: column;
  gap: 44px;
}
</style>
