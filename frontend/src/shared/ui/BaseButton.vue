<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import type { RouteLocationRaw } from 'vue-router'

import AppIcon from './AppIcon.vue'

type Variant = 'primary' | 'ghost' | 'light' | 'link' | 'back'

const props = withDefaults(
  defineProps<{
    /** Visual style; `back` includes the standard leading arrow for return actions. */
    variant?: Variant
    /** Renders a `RouterLink` to this route; takes precedence over `href`. */
    to?: RouteLocationRaw | undefined
    /** Renders a plain `<a>` with this URL when `to` is not set. */
    href?: string | undefined
    /** Native button type; ignored when rendered as a link. */
    type?: 'button' | 'submit'
    /** Stretches the button to the full width of its container. */
    block?: boolean
    /** Disables the native button; links stay clickable. */
    disabled?: boolean
    /** Shows a spinner before the slot, sets `aria-busy` and disables a native button. */
    loading?: boolean
  }>(),
  {
    variant: 'primary',
    to: undefined,
    href: undefined,
    type: 'button',
    block: false,
    disabled: false,
    loading: false,
  },
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
    <AppIcon v-if="loading" name="spinner" spin class="base-button__spinner" />
    <AppIcon v-else-if="variant === 'back'" name="arrow-left" class="base-button__back-icon" />
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
    border-color 0.15s ease,
    box-shadow 0.15s ease,
    color 0.15s ease;
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
  min-height: var(--ca-tap);
  padding: 12px 6px;
  margin: -12px -6px;
  border: none;
  background: transparent;
  color: var(--ca-text-muted);
  font-family: var(--ca-font-body);
  font-size: 15px;
}
.base-button--link:hover {
  color: var(--ca-text-bright);
}

.base-button--back {
  min-height: var(--ca-tap);
  padding: 0 15px 0 12px;
  border-color: var(--ca-border);
  background: var(--ca-surface);
  color: var(--ca-text-muted);
  font-size: 14px;
  box-shadow: var(--ca-shadow-sm);
}
.base-button--back:hover {
  border-color: var(--ca-orange);
  background: var(--ca-orange-soft);
  color: var(--ca-orange-ink);
  transform: translateY(-1px);
}
.base-button--back:active {
  transform: translateY(0);
}

.base-button__back-icon {
  flex: none;
  font-size: 17px;
  transition: transform 0.15s ease;
}
.base-button--back:hover .base-button__back-icon {
  transform: translateX(-2px);
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
