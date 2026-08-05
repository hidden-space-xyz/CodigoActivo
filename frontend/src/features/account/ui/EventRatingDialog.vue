<script setup lang="ts">
import { reactive, watch } from 'vue'

import type { AccountEventRating, EventRatingInput } from '@/entities/account'
import { BaseButton } from '@/shared/ui'

const props = defineProps<{
  visible: boolean
  eventTitle: string
  rating: AccountEventRating | null
  saving: boolean
}>()

const emit = defineEmits<{
  submit: [EventRatingInput]
  close: []
}>()

const MAX_ANSWER_LENGTH = 2000

const form = reactive<EventRatingInput>({
  score: 0,
  mostLiked: '',
  leastLiked: '',
  suggestions: '',
})

watch(
  () => [props.visible, props.rating] as const,
  ([visible, rating]) => {
    if (!visible) return
    form.score = rating?.score ?? 0
    form.mostLiked = rating?.mostLiked ?? ''
    form.leastLiked = rating?.leastLiked ?? ''
    form.suggestions = rating?.suggestions ?? ''
  },
  { immediate: true },
)

function onSubmit(): void {
  emit('submit', {
    score: form.score,
    mostLiked: form.mostLiked,
    leastLiked: form.leastLiked,
    suggestions: form.suggestions,
  })
}
</script>

<template>
  <el-dialog
    :model-value="visible"
    :title="$t('features.account.history.dialog.header')"
    width="min(90vw, 560px)"
    :close-on-click-modal="false"
    @update:model-value="(value: boolean) => !value && emit('close')"
  >
    <p class="acc-rating__event">{{ eventTitle }}</p>

    <form class="acc-form" @submit.prevent="onSubmit">
      <div class="acc-form__field">
        <label for="rating-score">{{ $t('features.account.history.dialog.score') }}</label>
        <div class="acc-rating__stars">
          <el-rate id="rating-score" v-model="form.score" />
          <BaseButton v-if="form.score > 0" variant="link" type="button" @click="form.score = 0">{{
            $t('features.account.history.dialog.clearScore')
          }}</BaseButton>
        </div>
      </div>

      <div class="acc-form__field">
        <label for="rating-most">{{ $t('entities.event.ratingQuestions.mostLiked') }}</label>
        <el-input
          id="rating-most"
          v-model="form.mostLiked"
          type="textarea"
          :maxlength="MAX_ANSWER_LENGTH"
          :rows="3"
          :autosize="{ minRows: 3 }"
        />
      </div>

      <div class="acc-form__field">
        <label for="rating-least">{{ $t('entities.event.ratingQuestions.leastLiked') }}</label>
        <el-input
          id="rating-least"
          v-model="form.leastLiked"
          type="textarea"
          :maxlength="MAX_ANSWER_LENGTH"
          :rows="3"
          :autosize="{ minRows: 3 }"
        />
      </div>

      <div class="acc-form__field">
        <label for="rating-suggestions">{{
          $t('entities.event.ratingQuestions.suggestions')
        }}</label>
        <el-input
          id="rating-suggestions"
          v-model="form.suggestions"
          type="textarea"
          :maxlength="MAX_ANSWER_LENGTH"
          :rows="3"
          :autosize="{ minRows: 3 }"
        />
      </div>

      <div class="acc-form__actions">
        <BaseButton variant="link" type="button" @click="emit('close')">
          {{ $t('common.cancel') }}
        </BaseButton>
        <BaseButton variant="primary" type="submit" :loading="saving">
          {{ $t('common.save') }}
        </BaseButton>
      </div>
    </form>
  </el-dialog>
</template>

<style scoped>
.acc-rating__event {
  margin: 0 0 18px;
  color: var(--ca-text-muted);
  font-size: 14px;
}

.acc-rating__stars {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.acc-form__field {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-bottom: 16px;
}

.acc-form__field label {
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-muted);
}

.acc-form__actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 8px;
}
</style>
