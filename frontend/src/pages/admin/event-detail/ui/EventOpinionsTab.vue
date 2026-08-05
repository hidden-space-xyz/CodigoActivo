<script setup lang="ts">
import { useEventRatingsTable } from '@/features/manage-events'
import type { EventRatingListItemResponse } from '@/shared/api/generated/models'
import { DataState } from '@/shared/ui'
import { formatDateTime } from '@/shared/lib'

const props = defineProps<{
  eventId: string
  active: boolean
}>()

const ratings = useEventRatingsTable(
  () => props.eventId,
  () => props.active,
)

function answers(rating: EventRatingListItemResponse): { key: string; value: string }[] {
  return [
    { key: 'mostLiked', value: rating.mostLiked ?? '' },
    { key: 'leastLiked', value: rating.leastLiked ?? '' },
    { key: 'suggestions', value: rating.suggestions ?? '' },
  ].filter((answer) => answer.value.trim() !== '')
}
</script>

<template>
  <div>
    <DataState
      :loading="ratings.table.loading.value && ratings.table.items.value.length === 0"
      :error="ratings.table.isError.value"
      :empty="ratings.table.total.value === 0 && !ratings.table.loading.value"
      :empty-text="$t('pages.admin.eventDetail.opinions.empty')"
    >
      <p class="count">
        {{ $t('pages.admin.eventDetail.opinions.count', ratings.table.total.value) }}
      </p>

      <ul class="opinions">
        <li v-for="rating in ratings.table.items.value" :key="rating.id" class="opinion">
          <div class="opinion__head">
            <span class="opinion__author">{{
              $t('pages.admin.eventDetail.opinions.anonymous')
            }}</span>
            <el-rate :model-value="rating.score ?? 0" disabled :max="5" class="opinion__stars" />
            <span class="opinion__score">{{ rating.score ?? 0 }}/5</span>
            <span class="opinion__date">{{
              formatDateTime(rating.updatedAt ?? rating.createdAt)
            }}</span>
          </div>

          <dl v-if="answers(rating).length > 0" class="opinion__answers">
            <template v-for="answer in answers(rating)" :key="answer.key">
              <dt>{{ $t(`entities.event.ratingQuestions.${answer.key}`) }}</dt>
              <dd>{{ answer.value }}</dd>
            </template>
          </dl>
          <p v-else class="opinion__no-answers">
            {{ $t('pages.admin.eventDetail.opinions.noAnswers') }}
          </p>
        </li>
      </ul>

      <el-pagination
        v-if="ratings.table.total.value > 25 || ratings.table.first.value > 0"
        v-bind="ratings.table.paginationProps.value"
        class="paginator"
        @update:current-page="ratings.table.onCurrentPageChange"
        @update:page-size="ratings.table.onPageSizeChange"
      />
    </DataState>
  </div>
</template>

<style scoped>
.count {
  color: var(--ca-text-muted);
  font-size: 13px;
  margin-bottom: 12px;
}

.opinions {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.opinion {
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 12px;
  padding: 16px 18px;
}

.opinion__head {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.opinion__author {
  font-weight: 600;
  font-size: 13px;
  color: var(--ca-text-muted);
}

.opinion__score {
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-muted);
}

.opinion__date {
  margin-left: auto;
  font-size: 12.5px;
  color: var(--ca-text-muted);
}

.opinion__answers {
  margin: 14px 0 0;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.opinion__answers dt {
  font-size: 12.5px;
  font-weight: 600;
  color: var(--ca-text-muted);
}

.opinion__answers dd {
  margin: 3px 0 0;
  color: var(--ca-text);
  line-height: 1.55;
  white-space: pre-wrap;
}

.opinion__no-answers {
  margin: 12px 0 0;
  font-size: 13px;
  color: var(--ca-text-dim);
}

.paginator {
  margin-top: 14px;
  justify-content: flex-end;
}
</style>
