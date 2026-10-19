<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'

import type { TermsDecisionInput } from '@/entities/activity'
import type { EventTermsDocumentState } from '@/entities/event'
import { ActionButton } from '@/shared/ui/action-button'
import { RichTextContent } from '@/shared/ui/rich-text-content'

import {
  asksForSignature,
  draftKey,
  readTermsDecisions,
  type TermsDecider,
  type TermsDialogItem,
  type TermsDialogSection,
} from '../model/terms-decisions'
import SignaturePad from './SignaturePad.vue'

const props = defineProps<{
  /** Opens the dialog; every time it opens, decisions reset to unchecked for `sections`. */
  visible: boolean
  /** People being signed up, each with the documents still to decide for them. */
  sections: readonly TermsDialogSection[]
}>()

const emit = defineEmits<{
  /** Fired with `false` when the dialog is closed or dismissed without confirming. */
  'update:visible': [value: boolean]
  /**
   * Fired with the decisions of each person, by person id, once every required document is checked
   * and every checked one that asks for a signature is signed; confirming earlier only reveals what
   * is missing.
   */
  confirm: [decisions: Readonly<Record<string, TermsDecisionInput[]>>]
}>()

const { t } = useI18n()

const checked = reactive<Record<string, boolean>>({})
const signatures = reactive<Record<string, string>>({})
const attempted = ref(false)

const keys = computed(() =>
  props.sections.flatMap((section) =>
    section.items.map((item) => draftKey(section.personId, item.document.id)),
  ),
)

watch(
  () => props.visible,
  (open) => {
    if (!open) return
    attempted.value = false
    for (const key of Object.keys(checked)) delete checked[key]
    for (const key of Object.keys(signatures)) delete signatures[key]
    for (const key of keys.value) {
      checked[key] = false
      signatures[key] = ''
    }
  },
)

watch(checked, () => {
  for (const key of keys.value) {
    if (!checked[key]) signatures[key] = ''
  }
})

const reading = computed(() => readTermsDecisions(props.sections, { checked, signatures }))

/** The documents of a section, split by who decides them, the signed-in user's first. */
function groupsOf(
  section: TermsDialogSection,
): { decider: TermsDecider; items: TermsDialogItem[] }[] {
  return (['user', 'person'] as const)
    .map((decider) => ({
      decider,
      items: section.items.filter((item) => item.decider === decider),
    }))
    .filter((group) => group.items.length > 0)
}

function showsSignature(personId: string, document: EventTermsDocumentState): boolean {
  return asksForSignature(personId, document, { checked, signatures })
}

const preview = reactive<{
  visible: boolean
  document: EventTermsDocumentState | null
  key: string
}>({ visible: false, document: null, key: '' })

/** Opens the content popup of `document` for the decision stored under `key`. */
function openPreview(document: EventTermsDocumentState, key: string): void {
  preview.document = document
  preview.key = key
  preview.visible = true
}

/** Marks the previewed decision as accepted and closes the popup. */
function acceptFromPreview(): void {
  if (preview.key) checked[preview.key] = true
  preview.visible = false
}

/** Marks the previewed decision as rejected and closes the popup. */
function rejectFromPreview(): void {
  if (preview.key) checked[preview.key] = false
  preview.visible = false
}

function close(): void {
  emit('update:visible', false)
}

function confirm(): void {
  attempted.value = true
  const { decisions } = reading.value
  if (decisions) emit('confirm', decisions)
}
</script>

<template>
  <el-dialog
    :model-value="visible"
    :title="t('pages.eventDetail.terms.header')"
    width="min(680px, 94vw)"
    append-to-body
    @update:model-value="close"
  >
    <p class="terms-dialog__lead">{{ t('pages.eventDetail.terms.lead') }}</p>
    <section v-for="section in sections" :key="section.personId" class="terms-dialog__person">
      <h3 class="terms-dialog__person-name">
        {{ section.self ? t('pages.eventDetail.terms.selfHeader') : section.name }}
      </h3>
      <div v-for="group in groupsOf(section)" :key="group.decider" class="terms-dialog__group">
        <p v-if="!section.self" class="terms-dialog__decider">
          {{
            group.decider === 'person'
              ? t('pages.eventDetail.terms.decidedByPerson', { name: section.name })
              : t('pages.eventDetail.terms.decidedByUser', { name: section.name })
          }}
        </p>
        <p v-if="group.decider === 'person'" class="terms-dialog__hint">
          {{ t('pages.eventDetail.terms.handOver', { name: section.name }) }}
        </p>
        <ul class="terms-dialog__list">
          <li v-for="{ document } in group.items" :key="document.id" class="terms-dialog__row">
            <div class="terms-dialog__decision">
              <el-checkbox
                :id="`terms-${draftKey(section.personId, document.id)}`"
                v-model="checked[draftKey(section.personId, document.id)]"
                :aria-label="
                  section.self || group.decider === 'person'
                    ? t('pages.eventDetail.terms.checkboxLabel', { name: document.name })
                    : t('pages.eventDetail.terms.checkboxLabelFor', {
                        name: document.name,
                        person: section.name,
                      })
                "
              />
              <div class="terms-dialog__info">
                <button
                  type="button"
                  class="terms-dialog__link"
                  :title="t('pages.eventDetail.terms.openDocument', { name: document.name })"
                  @click="openPreview(document, draftKey(section.personId, document.id))"
                >
                  {{ document.name }}
                </button>
                <span
                  class="terms-dialog__tag"
                  :class="{
                    'terms-dialog__tag--required': document.required,
                    'terms-dialog__tag--optional': !document.required,
                  }"
                >
                  {{
                    document.required
                      ? t('pages.eventDetail.terms.required')
                      : t('pages.eventDetail.terms.optional')
                  }}
                </span>
                <span v-if="document.requiresSignature" class="terms-dialog__tag">
                  {{ t('pages.eventDetail.terms.signature.tag') }}
                </span>
              </div>
            </div>
            <SignaturePad
              v-if="showsSignature(section.personId, document)"
              :model-value="signatures[draftKey(section.personId, document.id)] ?? ''"
              :label="t('pages.eventDetail.terms.signature.label', { name: document.name })"
              :invalid="attempted && !signatures[draftKey(section.personId, document.id)]"
              @update:model-value="
                (value: string) => (signatures[draftKey(section.personId, document.id)] = value)
              "
            />
          </li>
        </ul>
      </div>
    </section>
    <p v-if="attempted && reading.missingRequired" class="terms-dialog__warning" role="alert">
      {{ t('pages.eventDetail.terms.missingRequired') }}
    </p>
    <p v-if="attempted && reading.missingSignature" class="terms-dialog__warning" role="alert">
      {{ t('pages.eventDetail.terms.signature.missing') }}
    </p>
    <template #footer>
      <ActionButton :label="t('common.cancel')" text @click="close" />
      <ActionButton :label="t('pages.eventDetail.terms.confirm')" type="primary" @click="confirm" />
    </template>
  </el-dialog>

  <el-dialog
    v-model="preview.visible"
    :title="preview.document?.name"
    :aria-label="`${t('pages.eventDetail.terms.documentHeader')}: ${preview.document?.name ?? ''}`"
    width="min(680px, 94vw)"
    append-to-body
  >
    <div class="terms-dialog__content">
      <RichTextContent :content="preview.document?.description ?? ''" />
    </div>
    <template #footer>
      <ActionButton
        :label="t('pages.eventDetail.terms.close')"
        text
        @click="preview.visible = false"
      />
      <ActionButton
        :label="t('pages.eventDetail.terms.reject')"
        type="danger"
        @click="rejectFromPreview"
      />
      <ActionButton
        :label="t('pages.eventDetail.terms.accept')"
        type="primary"
        @click="acceptFromPreview"
      />
    </template>
  </el-dialog>
</template>

<style scoped>
.terms-dialog__lead {
  color: var(--ca-text);
  line-height: 1.55;
  margin-bottom: 14px;
}

.terms-dialog__person + .terms-dialog__person {
  margin-top: 18px;
}

.terms-dialog__person-name {
  font-size: 15px;
  font-weight: 700;
  color: var(--ca-text);
  margin: 0 0 8px;
}

.terms-dialog__group + .terms-dialog__group {
  margin-top: 12px;
}

.terms-dialog__decider {
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-muted);
  margin: 0 0 6px;
}

.terms-dialog__hint {
  font-size: 13px;
  color: var(--ca-text-muted);
  line-height: 1.5;
  margin: 0 0 8px;
}

.terms-dialog__list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.terms-dialog__row {
  display: flex;
  flex-direction: column;
  gap: 12px;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 12px;
  padding: 12px 14px;
}

.terms-dialog__decision {
  display: flex;
  align-items: center;
  gap: 10px;
}

.terms-dialog__info {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px 10px;
  min-width: 0;
}

.terms-dialog__link {
  background: none;
  border: none;
  padding: 0;
  font: inherit;
  font-weight: 600;
  color: var(--ca-orange);
  text-decoration: underline;
  cursor: pointer;
}

.terms-dialog__tag {
  font-size: 12px;
  font-weight: 600;
  padding: 2px 8px;
  border-radius: 999px;
  white-space: nowrap;
}

.terms-dialog__tag--required {
  color: var(--ca-danger-ink);
  background: var(--ca-danger-soft);
}

.terms-dialog__tag--optional {
  color: var(--ca-text-muted);
  background: var(--ca-surface-2);
}

.terms-dialog__warning {
  margin-top: 14px;
  font-size: 13px;
  color: var(--ca-danger-ink);
}

.terms-dialog__content {
  max-height: 48vh;
  overflow-y: auto;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 12px;
  padding: 14px 16px;
}
</style>
