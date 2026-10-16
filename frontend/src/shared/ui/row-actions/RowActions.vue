<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

import { ActionButton } from '@/shared/ui/action-button'
import { AppIcon } from '@/shared/ui/app-icon'

import type { RowAction } from './row-action'

const props = defineProps<{
  /** Commands of the row in display order; inline ones become icon buttons, the rest fill the menu. */
  actions: readonly RowAction[]
  /** Accessible name of the "more actions" trigger; defaults to the generic localized text. */
  menuLabel?: string
}>()

const { t } = useI18n()

const inlineActions = computed(() => props.actions.filter((action) => action.inline))
const menuActions = computed(() => props.actions.filter((action) => !action.inline))
const triggerLabel = computed(() => props.menuLabel ?? t('table.moreActions'))
</script>

<template>
  <div class="row-actions">
    <ActionButton
      v-for="action in inlineActions"
      :key="action.key"
      :icon="action.icon"
      text
      circle
      size="small"
      class="row-actions__button"
      :class="{ 'row-actions__button--danger': action.danger }"
      :aria-label="action.label"
      :disabled="action.disabled"
      @click="action.run()"
    />
    <el-dropdown
      v-if="menuActions.length > 0"
      trigger="click"
      placement="bottom-end"
      :persistent="false"
      popper-class="row-actions__menu"
    >
      <el-button class="row-actions__more" text circle size="small" :aria-label="triggerLabel">
        <AppIcon name="more" />
      </el-button>
      <template #dropdown>
        <el-dropdown-menu>
          <el-dropdown-item
            v-for="action in menuActions"
            :key="action.key"
            :disabled="action.disabled"
            :divided="action.divided"
            :class="{ 'row-actions__item--danger': action.danger }"
            @click="action.run()"
          >
            <AppIcon :name="action.icon" class="row-actions__item-icon" />
            <span class="row-actions__item-text">
              <span class="row-actions__item-label">{{ action.label }}</span>
              <span v-if="action.disabled && action.reason" class="row-actions__item-reason">{{
                action.reason
              }}</span>
            </span>
          </el-dropdown-item>
        </el-dropdown-menu>
      </template>
    </el-dropdown>
  </div>
</template>

<style scoped>
.row-actions {
  display: inline-flex;
  align-items: center;
  justify-content: flex-end;
  gap: 4px;
  vertical-align: middle;
}

.row-actions :deep(.el-button) {
  margin-left: 0;
}

.row-actions :deep(.el-button.is-text) {
  color: var(--ca-text-muted);
  transition:
    color 0.15s ease,
    background-color 0.15s ease;
}

.row-actions :deep(.el-button.is-text:not(.is-disabled):hover),
.row-actions :deep(.el-button.is-text:not(.is-disabled):focus-visible),
.row-actions :deep(.row-actions__more[aria-expanded='true']) {
  color: var(--ca-text-bright);
  background-color: var(--ca-surface-2);
}

.row-actions :deep(.row-actions__button--danger:not(.is-disabled):hover),
.row-actions :deep(.row-actions__button--danger:not(.is-disabled):focus-visible) {
  color: var(--ca-danger-ink);
  background-color: var(--ca-danger-soft);
}

.row-actions :deep(.el-button.is-disabled) {
  color: var(--ca-text-ghost);
  background-color: transparent;
}

.row-actions :deep(.el-button:focus-visible) {
  outline: 2px solid var(--ca-orange-ink);
  outline-offset: 1px;
}
</style>

<style>
.row-actions__menu .el-dropdown-menu__item {
  gap: 10px;
  align-items: flex-start;
  min-width: 200px;
  max-width: min(320px, calc(100vw - 32px));
  padding: 8px 14px;
  font-size: 14px;
  line-height: 20px;
}

.row-actions__menu .row-actions__item-icon {
  flex: none;
  margin: 2px 0 0;
  font-size: 16px;
  color: var(--ca-text-muted);
}

.row-actions__menu .row-actions__item-text {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.row-actions__menu .row-actions__item-reason {
  font-size: 12px;
  line-height: 16px;
  color: var(--ca-text-dim);
  white-space: normal;
}

.row-actions__menu .row-actions__item--danger:not(.is-disabled),
.row-actions__menu .row-actions__item--danger:not(.is-disabled) .row-actions__item-icon {
  color: var(--ca-danger-ink);
}

.row-actions__menu .row-actions__item--danger:not(.is-disabled):hover,
.row-actions__menu .row-actions__item--danger:not(.is-disabled):focus {
  color: var(--ca-danger-ink);
  background-color: var(--ca-danger-soft);
}
</style>
