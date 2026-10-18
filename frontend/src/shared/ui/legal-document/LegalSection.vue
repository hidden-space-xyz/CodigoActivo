<script setup lang="ts">
defineProps<{
  /** Id of the `<h2>`, which also labels the section and can be linked to as a fragment. */
  id: string
  /** Section heading. */
  title: string
}>()
</script>

<template>
  <section class="legal-section" :aria-labelledby="id">
    <h2 :id="id" class="legal-section__heading">{{ title }}</h2>
    <slot />
  </section>
</template>

<style scoped>
.legal-section__heading {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: clamp(22px, 3.4vw, 26px);
  line-height: 1.2;
  letter-spacing: -0.02em;
  color: var(--ca-text-bright);
  scroll-margin-top: 96px;
}

.legal-section > :deep(p) {
  margin-top: 12px;
  font-size: 16px;
  line-height: 1.7;
  color: var(--ca-text-muted);
}

.legal-section > :deep(ul:not(.legal-entry-list)) {
  margin: 10px 0 0;
  padding-left: 22px;
  list-style: disc;
  font-size: 16px;
  line-height: 1.7;
  color: var(--ca-text-muted);
}

.legal-section > :deep(ul:not(.legal-entry-list)) > li + li {
  margin-top: 6px;
}

.legal-section :deep(a) {
  color: var(--ca-orange-ink);
  font-weight: 600;
  text-decoration: none;
  overflow-wrap: anywhere;
}

.legal-section :deep(a:hover) {
  text-decoration: underline;
}

.legal-section :deep(strong) {
  font-weight: 600;
  color: var(--ca-text);
}
</style>
