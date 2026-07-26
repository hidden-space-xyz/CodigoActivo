<script setup lang="ts">
import SectionEyebrow from './SectionEyebrow.vue'

defineProps<{ eyebrow: string; title: string }>()
</script>

<template>
  <section class="page-head">
    <div class="page-head__glow" aria-hidden="true" />
    <div class="ca-container--narrow page-head__inner">
      <SectionEyebrow :text="eyebrow" color="var(--ca-orange-ink)" />
      <h1 class="page-head__title">{{ title }}</h1>
      <slot />
    </div>
  </section>
</template>

<style scoped>
.page-head {
  position: relative;
  overflow: hidden;
  padding: 64px 24px 16px;
}

.page-head__glow {
  position: absolute;
  inset: 0;
  background: radial-gradient(700px 400px at 80% -20%, var(--ca-orange-soft), transparent 60%);
}

.page-head__inner {
  position: relative;
}

.page-head__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 46px;
  letter-spacing: -0.03em;
  color: var(--ca-text-bright);
}
</style>
