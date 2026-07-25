<script setup lang="ts">
import { reactive, watch } from 'vue'
import Dialog from 'primevue/dialog'
import Rating from 'primevue/rating'
import Textarea from 'primevue/textarea'

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
  <Dialog
    :visible="visible"
    modal
    :draggable="false"
    :header="$t('features.account.history.dialog.header')"
    :style="{ width: '90vw', maxWidth: '560px' }"
    @update:visible="(value) => !value && emit('close')"
  >
    <p class="acc-rating__event">{{ eventTitle }}</p>

    <form class="acc-form" @submit.prevent="onSubmit">
      <div class="acc-form__field">
        <label for="rating-score">{{ $t('features.account.history.dialog.score') }}</label>
        <div class="acc-rating__stars">
          <Rating v-model="form.score" input-id="rating-score" />
          <BaseButton
            v-if="form.score > 0"
            variant="link"
            type="button"
            @click="form.score = 0"
            >{{ $t('features.account.history.dialog.clearScore') }}</BaseButton
          >
        </div>
      </div>

      <div class="acc-form__field">
        <label for="rating-most">{{ $t('entities.event.ratingQuestions.mostLiked') }}</label>
        <Textarea
          id="rating-most"
          v-model="form.mostLiked"
          :maxlength="MAX_ANSWER_LENGTH"
          rows="3"
          auto-resize
          fluid
        />
      </div>

      <div class="acc-form__field">
        <label for="rating-least">{{ $t('entities.event.ratingQuestions.leastLiked') }}</label>
        <Textarea
          id="rating-least"
          v-model="form.leastLiked"
          :maxlength="MAX_ANSWER_LENGTH"
          rows="3"
          auto-resize
          fluid
        />
      </div>

      <div class="acc-form__field">
        <label for="rating-suggestions">{{
          $t('entities.event.ratingQuestions.suggestions')
        }}</label>
        <Textarea
          id="rating-suggestions"
          v-model="form.suggestions"
          :maxlength="MAX_ANSWER_LENGTH"
          rows="3"
          auto-resize
          fluid
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
  </Dialog>
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
