<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import type { RouteLocationRaw } from 'vue-router'

type Variant = 'primary' | 'ghost' | 'light' | 'purple' | 'link'

const props = withDefaults(
  defineProps<{
    variant?: Variant
    to?: RouteLocationRaw
    href?: string
    type?: 'button' | 'submit'
    block?: boolean
    disabled?: boolean
    loading?: boolean
  }>(),
  { variant: 'primary', type: 'button', block: false, disabled: false, loading: false },
)

const isButton = computed(() => !props.to && !props.href)
const isInactive = computed(() => props.disabled || props.loading)

const componentTag = computed(() => {
  if (props.to) return RouterLink
  if (props.href) return 'a'
  return 'button'
})

const bindings = computed(() => {
  if (props.to) return { to: props.to }
  if (props.href) return { href: props.href }
  return { type: props.type, disabled: isInactive.value }
})
</script>

<template>
  <component
    :is="componentTag"
    v-bind="bindings"
    class="base-button"
    :class="[
      `base-button--${variant}`,
      {
        'base-button--block': block,
        'base-button--loading': loading,
        'base-button--disabled': isInactive && isButton,
      },
    ]"
    :aria-busy="loading ? 'true' : undefined"
  >
    <i v-if="loading" class="pi pi-spin pi-spinner base-button__spinner" aria-hidden="true" />
    <slot />
  </component>
</template>

<style scoped>
.base-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  font-family: var(--ca-font-display);
  font-weight: 600;
  font-size: 16px;
  line-height: 1;
  border: 1px solid transparent;
  border-radius: 12px;
  padding: 14px 24px;
  cursor: pointer;
  text-decoration: none;
  transition:
    transform 0.15s ease,
    background 0.15s ease,
    border-color 0.15s ease;
}

.base-button--block {
  width: 100%;
}

.base-button--primary {
  color: var(--ca-on-primary);
  background: var(--ca-orange);
}
.base-button--primary:hover {
  background: var(--ca-orange-strong);
  transform: translateY(-2px);
}

.base-button--purple {
  color: var(--ca-on-primary);
  background: var(--ca-azure);
}
.base-button--purple:hover {
  background: color-mix(in srgb, var(--ca-azure) 82%, #ffffff);
}

.base-button--light {
  color: var(--ca-bg);
  background: var(--ca-text);
}
.base-button--light:hover {
  background: var(--ca-text-bright);
}

.base-button--ghost {
  color: var(--ca-text);
  background: transparent;
  border-color: var(--ca-border-strong);
}
.base-button--ghost:hover {
  border-color: var(--ca-border-strong-2);
}

.base-button--link {
  padding: 0;
  border: none;
  background: transparent;
  color: var(--ca-text-muted);
  font-family: var(--ca-font-body);
  font-size: 15px;
}
.base-button--link:hover {
  color: var(--ca-text-bright);
}

.base-button--disabled,
.base-button--loading {
  opacity: 0.6;
  cursor: not-allowed;
}
.base-button--disabled:hover,
.base-button--loading:hover {
  transform: none;
}

.base-button__spinner {
  font-size: 0.9em;
}
</style>
