<script setup lang="ts">
defineProps<{ title: string; description: string }>()
</script>

<template>
  <div class="page-heading">
    <h1 class="page-heading__title">{{ title }}</h1>
    <p class="page-heading__comment">
      <span class="page-heading__comment-mark" aria-hidden="true">//</span>
      <span>{{ description }}</span>
    </p>
  </div>
</template>

<style scoped>
.page-heading__title {
  font-family: var(--ca-font-display);
  font-size: clamp(32px, 6vw, 46px);
  font-weight: 700;
  letter-spacing: -0.03em;
  color: var(--ca-text-bright);
}

.page-heading__comment {
  display: flex;
  gap: 0.65em;
  align-items: baseline;
  max-width: 680px;
  margin-top: 14px;
  font-family: var(--ca-font-mono);
  font-size: clamp(14px, 1.8vw, 16px);
  line-height: 1.65;
  color: var(--ca-text-muted);
}

.page-heading__comment-mark {
  flex: none;
  font-weight: 700;
  color: var(--ca-orange);
}
</style>
