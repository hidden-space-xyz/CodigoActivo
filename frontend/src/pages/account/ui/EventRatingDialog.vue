<script setup lang="ts">
import { computed, reactive, watch } from 'vue'

import { isEventRatingEmpty } from '@/entities/account'
import type { EventRatingInput } from '@/entities/account'
import { RATING_ANSWER_MAX_LENGTH, RATING_MAX_SCORE } from '@/shared/config'
import { BrandButton } from '@/shared/ui/brand-button'

const props = defineProps<{
  /** Opens the dialog; each time it opens the form is reset to `rating`. */
  visible: boolean
  /** Title of the rated event, shown above the form. */
  eventTitle: string
  /**
   * The account's stored rating of the event, which the form starts from and which can be deleted;
   * `null` to start from an empty form without the delete button.
   */
  rating: EventRatingInput | null
  /** Shows a loading state on the save button while the rating is stored. */
  saving: boolean
  /** Shows a loading state on the delete button while the rating is deleted. */
  deleting: boolean
}>()

const emit = defineEmits<{
  /**
   * Fired on submit with the score (`null` without stars) and the free-text answers; never for a
   * rating with neither a score nor an answer.
   */
  submit: [EventRatingInput]
  /** Fired when the user asks to delete the stored rating. */
  delete: []
  /** Fired on cancel or when the dialog is dismissed. */
  close: []
}>()

const form = reactive({
  stars: 0,
  mostLiked: '',
  leastLiked: '',
  suggestions: '',
})

const answers = computed<EventRatingInput>(() => ({
  score: form.stars > 0 ? form.stars : null,
  mostLiked: form.mostLiked,
  leastLiked: form.leastLiked,
  suggestions: form.suggestions,
}))
const empty = computed(() => isEventRatingEmpty(answers.value))

watch(
  () => props.visible,
  (visible) => {
    if (!visible) return
    form.stars = props.rating?.score ?? 0
    form.mostLiked = props.rating?.mostLiked ?? ''
    form.leastLiked = props.rating?.leastLiked ?? ''
    form.suggestions = props.rating?.suggestions ?? ''
  },
  { immediate: true },
)

function onSubmit(): void {
  if (empty.value) return
  emit('submit', answers.value)
}
</script>

<template>
  <el-dialog
    :model-value="visible"
    :title="$t('pages.account.history.dialog.header')"
    width="min(90vw, 560px)"
    :close-on-click-modal="false"
    @update:model-value="(value: boolean) => !value && emit('close')"
  >
    <p class="acc-rating__event">{{ eventTitle }}</p>
    <p class="acc-rating__notice">{{ $t('pages.account.history.dialog.notice') }}</p>

    <form class="acc-form" @submit.prevent="onSubmit">
      <div class="ca-field acc-form__field">
        <label class="ca-field__label" for="rating-score">{{
          $t('pages.account.history.dialog.score')
        }}</label>
        <div class="acc-rating__stars">
          <el-rate id="rating-score" v-model="form.stars" :max="RATING_MAX_SCORE" />
          <BrandButton v-if="form.stars > 0" variant="link" type="button" @click="form.stars = 0">{{
            $t('pages.account.history.dialog.clearScore')
          }}</BrandButton>
        </div>
      </div>

      <div class="ca-field acc-form__field">
        <label class="ca-field__label" for="rating-most">{{
          $t('entities.event.ratingQuestions.mostLiked')
        }}</label>
        <el-input
          id="rating-most"
          v-model="form.mostLiked"
          type="textarea"
          :maxlength="RATING_ANSWER_MAX_LENGTH"
          :autosize="{ minRows: 3, maxRows: 6 }"
        />
      </div>

      <div class="ca-field acc-form__field">
        <label class="ca-field__label" for="rating-least">{{
          $t('entities.event.ratingQuestions.leastLiked')
        }}</label>
        <el-input
          id="rating-least"
          v-model="form.leastLiked"
          type="textarea"
          :maxlength="RATING_ANSWER_MAX_LENGTH"
          :autosize="{ minRows: 3, maxRows: 6 }"
        />
      </div>

      <div class="ca-field acc-form__field">
        <label class="ca-field__label" for="rating-suggestions">{{
          $t('entities.event.ratingQuestions.suggestions')
        }}</label>
        <el-input
          id="rating-suggestions"
          v-model="form.suggestions"
          type="textarea"
          :maxlength="RATING_ANSWER_MAX_LENGTH"
          :autosize="{ minRows: 3, maxRows: 6 }"
        />
      </div>

      <p v-if="empty" class="acc-rating__hint">
        {{ $t('pages.account.history.dialog.emptyHint') }}
      </p>

      <div class="acc-form__actions">
        <BrandButton
          v-if="rating"
          class="acc-rating__delete"
          variant="link"
          type="button"
          :loading="deleting"
          @click="emit('delete')"
        >
          {{ $t('pages.account.history.dialog.delete') }}
        </BrandButton>
        <BrandButton variant="link" type="button" @click="emit('close')">
          {{ $t('common.cancel') }}
        </BrandButton>
        <BrandButton variant="primary" type="submit" :disabled="empty" :loading="saving">
          {{
            rating
              ? $t('pages.account.history.dialog.save')
              : $t('pages.account.history.dialog.submit')
          }}
        </BrandButton>
      </div>
    </form>
  </el-dialog>
</template>

<style scoped>
.acc-rating__event {
  margin: 0 0 6px;
  color: var(--ca-text-muted);
  font-size: 14px;
}

.acc-rating__notice {
  margin: 0 0 18px;
  color: var(--ca-text-dim);
  font-size: 13px;
  line-height: 1.5;
}

.acc-rating__hint {
  margin: 0 0 8px;
  color: var(--ca-text-dim);
  font-size: 13px;
  text-align: right;
}

.acc-rating__stars {
  display: flex;
  align-items: center;
  gap: 16px;
  flex-wrap: wrap;
}

.acc-rating__stars :deep(.el-rate) {
  --el-rate-icon-size: 28px;
  --el-rate-icon-margin: 10px;
  height: var(--ca-tap);
}

.acc-rating__stars :deep(.base-button--link) {
  min-height: var(--ca-tap);
}

.acc-form__field {
  margin-bottom: 16px;
}

.acc-form__actions {
  display: flex;
  justify-content: flex-end;
  flex-wrap: wrap;
  gap: 10px;
  margin-top: 8px;
}

.acc-rating__delete {
  margin-right: auto;
}
</style>
