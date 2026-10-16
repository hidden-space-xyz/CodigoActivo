<script setup lang="ts">
defineProps<{
  /** Main text on one line with an ellipsis; the full text is its tooltip. */
  title: string
  /** Muted second line with an ellipsis and its full text as tooltip; hidden when empty. */
  subtitle?: string | null
}>()
</script>

<template>
  <div class="table-primary-cell">
    <div v-if="$slots.leading" class="table-primary-cell__leading">
      <slot name="leading" />
    </div>
    <div class="table-primary-cell__body">
      <div class="table-primary-cell__head">
        <span class="table-primary-cell__title" :title="title">{{ title }}</span>
        <span v-if="$slots.badges" class="table-primary-cell__badges">
          <slot name="badges" />
        </span>
      </div>
      <span v-if="subtitle" class="table-primary-cell__subtitle" :title="subtitle">{{
        subtitle
      }}</span>
      <div v-if="$slots.meta" class="table-primary-cell__meta">
        <slot name="meta" />
      </div>
    </div>
  </div>
</template>

<style scoped>
.table-primary-cell {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
}

.table-primary-cell__leading {
  flex: none;
  display: flex;
}

.table-primary-cell__body {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.table-primary-cell__head {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.table-primary-cell__title {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-weight: 600;
  color: var(--ca-text-bright);
}

.table-primary-cell__badges {
  display: inline-flex;
  flex: none;
  align-items: center;
  gap: 6px;
}

.table-primary-cell__subtitle,
.table-primary-cell__meta {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 12.5px;
  line-height: 18px;
  color: var(--ca-text-muted);
}

@media (max-width: 640px) {
  .table-primary-cell__leading {
    display: none;
  }
}
</style>
