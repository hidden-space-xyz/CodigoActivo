<script setup lang="ts">
import { useId } from 'vue'

import type { LegalPolicyKind } from '@/entities/legal-policy'
import { formatDateTime } from '@/shared/lib/date'
import { ActionButton } from '@/shared/ui/action-button'
import { DataState } from '@/shared/ui/data-state'
import { RichTextEditor } from '@/shared/ui/rich-text-editor'

import { useLegalPolicyEditor } from '../model/use-legal-policy-editor'
import LegalPolicyVersionDialog from './LegalPolicyVersionDialog.vue'

const props = defineProps<{
  /** Policy edited. */
  kind: LegalPolicyKind
  /** Accessible name of the editor, the name of the policy. */
  label: string
  /** Note about what the public page shows besides the text, if anything. */
  hint?: string
}>()

const {
  policy,
  loading,
  loadFailed,
  versions,
  versionsLoading,
  versionsFailed,
  form,
  dirty,
  publishing,
  publish,
  discard,
  viewing,
  viewedPolicy,
  viewedLoading,
  viewedFailed,
  view,
  closeVersion,
  restore,
} = useLegalPolicyEditor(props.kind)
const { draft, errors, invalid } = form

const versionsTitleId = useId()
</script>

<template>
  <div class="legal-policy-editor">
    <section class="legal-policy-editor__main">
      <DataState
        :loading="loading"
        :error="loadFailed"
        :empty="false"
        :error-text="$t('pages.admin.legalPolicies.loadError')"
      >
        <p class="legal-policy-editor__status">
          {{
            policy
              ? $t('pages.admin.legalPolicies.status.published', {
                  version: policy.version,
                  date: formatDateTime(policy.publishedAt),
                })
              : $t('pages.admin.legalPolicies.status.unpublished')
          }}
        </p>
        <p v-if="hint" class="ca-field__hint">{{ hint }}</p>
        <form class="legal-policy-editor__form" @submit.prevent="publish">
          <RichTextEditor v-model="draft.content" :label="label" :invalid="invalid('content')" />
          <small v-if="errors.content" class="ca-field__error">{{ errors.content }}</small>
          <div class="legal-policy-editor__actions">
            <span v-if="dirty" class="legal-policy-editor__dirty">
              {{ $t('pages.admin.legalPolicies.unpublishedChanges') }}
            </span>
            <ActionButton
              :label="$t('pages.admin.legalPolicies.discard')"
              text
              :disabled="!dirty || publishing"
              @click="discard"
            />
            <ActionButton
              :label="$t('pages.admin.legalPolicies.publish.button')"
              type="primary"
              :disabled="!dirty"
              :loading="publishing"
              @click="publish"
            />
          </div>
        </form>
      </DataState>
    </section>

    <section class="legal-policy-editor__versions" :aria-labelledby="versionsTitleId">
      <h2 :id="versionsTitleId" class="legal-policy-editor__versions-title">
        {{ $t('pages.admin.legalPolicies.versions.title') }}
      </h2>
      <DataState
        :loading="versionsLoading"
        :error="versionsFailed"
        :empty="versions.length === 0"
        :empty-text="$t('pages.admin.legalPolicies.versions.empty')"
      >
        <ol class="legal-policy-editor__version-list">
          <li
            v-for="(entry, index) in versions"
            :key="entry.version"
            class="legal-policy-editor__version"
          >
            <div class="legal-policy-editor__version-text">
              <span class="legal-policy-editor__version-number">
                {{ $t('pages.admin.legalPolicies.versions.number', { version: entry.version }) }}
              </span>
              <el-tag v-if="index === 0" size="small" type="success" disable-transitions>
                {{ $t('pages.admin.legalPolicies.versions.inForce') }}
              </el-tag>
              <span class="legal-policy-editor__version-date">
                {{ formatDateTime(entry.publishedAt) }}
              </span>
            </div>
            <ActionButton
              :label="$t('pages.admin.legalPolicies.versions.view')"
              size="small"
              text
              @click="view(entry.version)"
            />
          </li>
        </ol>
      </DataState>
    </section>

    <LegalPolicyVersionDialog
      :version="viewing"
      :policy="viewedPolicy"
      :loading="viewedLoading"
      :error="viewedFailed"
      @close="closeVersion"
      @restore="restore"
    />
  </div>
</template>

<style scoped>
.legal-policy-editor {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 280px;
  align-items: start;
  gap: 18px;
}

.legal-policy-editor__main,
.legal-policy-editor__versions {
  padding: 16px;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: var(--ca-radius-lg);
  box-shadow: var(--ca-shadow-sm);
}

.legal-policy-editor__status {
  margin-bottom: 8px;
  font-family: var(--ca-font-mono);
  font-size: 13px;
  color: var(--ca-text-dim);
}

.legal-policy-editor__form {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-top: 12px;
}

.legal-policy-editor__actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: flex-end;
  gap: 8px;
  margin-top: 8px;
}

.legal-policy-editor__dirty {
  margin-right: auto;
  font-size: 13px;
  color: var(--ca-warning-ink);
}

.legal-policy-editor__versions-title {
  margin-bottom: 10px;
  font-family: var(--ca-font-display);
  font-size: 16px;
  font-weight: 600;
  color: var(--ca-text-bright);
}

.legal-policy-editor__version-list {
  display: flex;
  flex-direction: column;
  margin: 0;
  padding: 0;
  list-style: none;
}

.legal-policy-editor__version {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 8px 0;
}

.legal-policy-editor__version + .legal-policy-editor__version {
  border-top: 1px solid var(--ca-border-soft);
}

.legal-policy-editor__version-text {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 4px 8px;
  min-width: 0;
}

.legal-policy-editor__version-number {
  font-weight: 600;
  color: var(--ca-text);
}

.legal-policy-editor__version-date {
  flex-basis: 100%;
  font-size: 13px;
  color: var(--ca-text-dim);
}

@media (max-width: 960px) {
  .legal-policy-editor {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
