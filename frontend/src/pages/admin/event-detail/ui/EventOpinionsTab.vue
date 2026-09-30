<script setup lang="ts">
import { DataState } from '@/shared/ui/data-state'

import { ratingAnswers } from '../lib/rating-answers'
import { useEventRatings } from '../model/use-event-ratings'

const props = defineProps<{
  /** Event whose anonymous ratings and written feedback are listed. */
  eventId: string
  /** Whether this tab is selected; ratings are only fetched while it is. */
  active: boolean
}>()

const table = useEventRatings(
  () => props.eventId,
  () => props.active,
)
</script>

<template>
  <div>
    <DataState
      :loading="table.loading.value && table.items.value.length === 0"
      :error="table.isError.value"
      :empty="table.total.value === 0 && !table.loading.value"
      :empty-text="$t('pages.admin.eventDetail.opinions.empty')"
    >
      <p class="count">
        {{ $t('pages.admin.eventDetail.opinions.count', table.total.value) }}
      </p>

      <ul class="opinions">
        <li v-for="rating in table.items.value" :key="rating.id" class="opinion">
          <div class="opinion__head">
            <span class="opinion__author">{{
              $t('pages.admin.eventDetail.opinions.anonymous')
            }}</span>
            <el-rate :model-value="rating.score" disabled :max="5" class="opinion__stars" />
            <span class="opinion__score">{{ rating.score }}/5</span>
          </div>

          <dl v-if="ratingAnswers(rating).length > 0" class="opinion__answers">
            <template v-for="answer in ratingAnswers(rating)" :key="answer.labelKey">
              <dt>{{ $t(answer.labelKey) }}</dt>
              <dd>{{ answer.value }}</dd>
            </template>
          </dl>
          <p v-else class="opinion__no-answers">
            {{ $t('pages.admin.eventDetail.opinions.noAnswers') }}
          </p>
        </li>
      </ul>

      <el-pagination
        v-if="table.total.value > 25 || table.first.value > 0"
        v-bind="table.paginationProps.value"
        class="paginator"
        @update:current-page="table.onCurrentPageChange"
        @update:page-size="table.onPageSizeChange"
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
