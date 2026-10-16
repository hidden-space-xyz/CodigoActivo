<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

import { displayUrl } from '@/shared/lib/navigation'
import { AppIcon } from '@/shared/ui/app-icon'

const props = defineProps<{
  /** Absolute http(s) address already sanitised by the entity mapper; opened in a new tab. */
  href: string
}>()

const { t } = useI18n()

const text = computed(() => displayUrl(props.href))
const hint = computed(() => ` ${t('common.opensInNewTab')}`)
</script>

<template>
  <a :href="href" target="_blank" rel="noopener" :title="href" class="external-link">
    <span class="external-link__text">{{ text }}</span>
    <AppIcon name="arrow-up-right" class="external-link__icon" />
    <span class="external-link__hint">{{ hint }}</span>
  </a>
</template>

<style scoped>
.external-link {
  position: relative;
  display: inline-flex;
  align-items: center;
  gap: 4px;
  max-width: 100%;
  border-radius: 4px;
  color: var(--ca-orange-ink);
  text-decoration: none;
  vertical-align: bottom;
}

.external-link__text {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.external-link:hover .external-link__text {
  text-decoration: underline;
  text-underline-offset: 3px;
}

.external-link:focus-visible {
  outline: 2px solid transparent;
  border-radius: 3px;
  background-color: var(--ca-orange-soft);
  text-decoration: underline;
  text-decoration-thickness: 2px;
  text-underline-offset: 2px;
}

.external-link__icon {
  flex: none;
  font-size: 12px;
}

.external-link__hint {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip-path: inset(50%);
  white-space: nowrap;
}
</style>
