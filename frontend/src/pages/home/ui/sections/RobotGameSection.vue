<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

import { useRobotGame } from '../../model/useRobotGame'
import type { Direction } from '../../model/useRobotGame'
import { BaseButton } from '@/shared/ui'

const { t } = useI18n()

const {
  level,
  levelNumber,
  totalLevels,
  boardCells,
  commands,
  robot,
  phase,
  stepIndex,
  lostReason,
  canAdd,
  canRun,
  isRunning,
  isWall,
  isGoal,
  isStart,
  addCommand,
  undo,
  clearCommands,
  run,
  reset,
  nextLevel,
} = useRobotGame()

const ARROWS: Record<Direction, string> = {
  up: '↑',
  down: '↓',
  left: '←',
  right: '→',
}

const statusText = computed(() => {
  if (phase.value === 'won') return t('pages.home.game.status.won')
  if (phase.value === 'lost') {
    if (lostReason.value === 'wall') return t('pages.home.game.status.lostWall')
    if (lostReason.value === 'offGrid') return t('pages.home.game.status.lostOffGrid')
    return t('pages.home.game.status.lostIncomplete')
  }
  if (phase.value === 'running') return t('pages.home.game.status.running')
  return t('pages.home.game.status.building')
})

function onKey(event: KeyboardEvent): void {
  const map: Record<string, Direction> = {
    ArrowUp: 'up',
    ArrowDown: 'down',
    ArrowLeft: 'left',
    ArrowRight: 'right',
  }
  const dir = map[event.key]
  if (!dir) return
  event.preventDefault()
  addCommand(dir)
}

function chipClass(index: number): Record<string, boolean> {
  return {
    'robot-game__chip--active': isRunning.value && index === stepIndex.value,
    'robot-game__chip--error': phase.value === 'lost' && index === stepIndex.value,
    'robot-game__chip--done': isRunning.value && index < stepIndex.value,
  }
}
</script>

<template>
  <div class="robot-game" @keydown="onKey">
    <div class="robot-game__header">
      <div class="robot-game__title">
        <span class="robot-game__title-emoji" aria-hidden="true">🤖</span>
        {{ $t('pages.home.game.title') }}
      </div>
      <div class="robot-game__level">
        {{ $t('pages.home.game.level', { current: levelNumber, total: totalLevels }) }}
      </div>
    </div>

    <div class="robot-game__body">
      <p class="robot-game__instructions">{{ $t('pages.home.game.instructions') }}</p>

      <div
        class="robot-game__board"
        role="img"
        :aria-label="$t('pages.home.game.boardLabel')"
        :style="`--cols:${level.cols}; --rows:${level.rows}`"
      >
        <div
          v-for="cell in boardCells"
          :key="`${cell.row}-${cell.col}`"
          class="robot-game__cell"
          :class="{
            'robot-game__cell--wall': isWall(cell),
            'robot-game__cell--goal': isGoal(cell),
            'robot-game__cell--start': isStart(cell),
          }"
        >
          <span v-if="isGoal(cell)" class="robot-game__star" aria-hidden="true">⭐</span>
        </div>

        <div
          class="robot-game__robot"
          :style="`--rc:${robot.col}; --rr:${robot.row}`"
          aria-hidden="true"
        >
          <span class="robot-game__robot-face">🤖</span>
        </div>

        <div v-if="phase === 'won' || phase === 'lost'" class="robot-game__overlay">
          <div class="robot-game__overlay-card" :class="`robot-game__overlay-card--${phase}`">
            <div class="robot-game__overlay-emoji" aria-hidden="true">
              {{ phase === 'won' ? '🎉' : '💪' }}
            </div>
            <p class="robot-game__overlay-text" aria-hidden="true">
              {{ phase === 'won' ? $t('pages.home.game.status.wonMessage') : statusText }}
            </p>
            <BaseButton v-if="phase === 'won'" variant="primary" @click="nextLevel">
              {{ $t('pages.home.game.controls.next') }}
            </BaseButton>
            <BaseButton v-else variant="primary" @click="reset">
              {{ $t('pages.home.game.controls.retry') }}
            </BaseButton>
          </div>
        </div>
      </div>

      <p class="robot-game__status" role="status" aria-live="polite">{{ statusText }}</p>

      <div class="robot-game__program">
        <div class="robot-game__program-head">
          <span class="robot-game__program-title">{{ $t('pages.home.game.program.title') }}</span>
          <span class="robot-game__counter">
            {{
              $t('pages.home.game.program.counter', {
                count: commands.length,
                max: level.maxCommands,
              })
            }}
          </span>
        </div>
        <div class="robot-game__chips">
          <template v-if="commands.length">
            <span
              v-for="(dir, index) in commands"
              :key="index"
              class="robot-game__chip"
              :class="chipClass(index)"
            >
              <span class="robot-game__chip-index">{{ index + 1 }}</span>
              <span class="robot-game__chip-arrow">{{ ARROWS[dir] }}</span>
            </span>
          </template>
          <span v-else class="robot-game__chips-empty">
            {{ $t('pages.home.game.program.empty') }}
          </span>
        </div>
      </div>

      <div class="robot-game__controls">
        <div class="robot-game__dpad">
          <button
            type="button"
            class="robot-game__key robot-game__key--up"
            :disabled="!canAdd"
            :aria-label="$t('pages.home.game.controls.up')"
            @click="addCommand('up')"
          >
            ↑
          </button>
          <button
            type="button"
            class="robot-game__key robot-game__key--left"
            :disabled="!canAdd"
            :aria-label="$t('pages.home.game.controls.left')"
            @click="addCommand('left')"
          >
            ←
          </button>
          <button
            type="button"
            class="robot-game__key robot-game__key--down"
            :disabled="!canAdd"
            :aria-label="$t('pages.home.game.controls.down')"
            @click="addCommand('down')"
          >
            ↓
          </button>
          <button
            type="button"
            class="robot-game__key robot-game__key--right"
            :disabled="!canAdd"
            :aria-label="$t('pages.home.game.controls.right')"
            @click="addCommand('right')"
          >
            →
          </button>
        </div>

        <div class="robot-game__actions">
          <div class="robot-game__edit">
            <button
              type="button"
              class="robot-game__mini"
              :disabled="phase !== 'building' || !commands.length"
              :aria-label="$t('pages.home.game.controls.undoAria')"
              @click="undo"
            >
              ↶ {{ $t('pages.home.game.controls.undo') }}
            </button>
            <button
              type="button"
              class="robot-game__mini"
              :disabled="phase !== 'building' || !commands.length"
              :aria-label="$t('pages.home.game.controls.clearAria')"
              @click="clearCommands"
            >
              🗑 {{ $t('pages.home.game.controls.clear') }}
            </button>
          </div>
          <BaseButton
            variant="primary"
            block
            :disabled="!canRun"
            :aria-label="$t('pages.home.game.controls.runAria')"
            @click="run"
          >
            ▶ {{ $t('pages.home.game.controls.run') }}
          </BaseButton>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.robot-game {
  background: var(--ca-surface);
  border: 1px solid var(--ca-border);
  border-radius: 20px;
  box-shadow: var(--ca-shadow-lg);
  overflow: hidden;
}

.robot-game__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 16px 20px;
  background: linear-gradient(120deg, var(--ca-orange-soft), var(--ca-lime-soft));
}

.robot-game__title {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 18px;
  color: var(--ca-text-bright);
}

.robot-game__title-emoji {
  font-size: 22px;
}

.robot-game__level {
  font-family: var(--ca-font-mono);
  font-size: 12px;
  font-weight: 500;
  letter-spacing: 0.04em;
  color: var(--ca-azure-ink);
  background: var(--ca-azure-soft);
  padding: 5px 11px;
  border-radius: 999px;
  white-space: nowrap;
}

.robot-game__body {
  padding: 20px;
}

.robot-game__instructions {
  margin: 0 0 16px;
  font-size: 14px;
  line-height: 1.5;
  color: var(--ca-text-muted);
}

.robot-game__board {
  position: relative;
  display: grid;
  grid-template-columns: repeat(var(--cols), 1fr);
  gap: 0;
  width: 100%;
  max-width: 320px;
  margin: 0 auto;
}

.robot-game__cell {
  aspect-ratio: 1 / 1;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--ca-surface-2);
  box-shadow: inset 0 0 0 2px var(--ca-surface);
}

.robot-game__cell--wall {
  background: var(--ca-text-dim);
  box-shadow: inset 0 0 0 3px var(--ca-surface);
}

.robot-game__cell--goal {
  background: var(--ca-warning-soft);
}

.robot-game__cell--start {
  outline: 2px dashed var(--ca-lime);
  outline-offset: -5px;
}

.robot-game__star {
  font-size: clamp(15px, 5vw, 22px);
  animation: robot-game-twinkle 1.6s ease-in-out infinite;
}

.robot-game__robot {
  position: absolute;
  top: 0;
  left: 0;
  width: calc(100% / var(--cols));
  height: calc(100% / var(--rows));
  display: flex;
  align-items: center;
  justify-content: center;
  transform: translate(calc(var(--rc) * 100%), calc(var(--rr) * 100%));
  transition: transform 0.35s ease;
  pointer-events: none;
  z-index: 2;
}

.robot-game__robot-face {
  font-size: clamp(18px, 6vw, 27px);
  line-height: 1;
  animation: ca-float 3s ease-in-out infinite;
}

.robot-game__overlay {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 12px;
  background: var(--ca-glass-bg);
  backdrop-filter: blur(2px);
  z-index: 3;
}

.robot-game__overlay-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 10px;
  max-width: 90%;
  padding: 18px 22px;
  text-align: center;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border);
  border-radius: 16px;
  box-shadow: var(--ca-shadow-md);
  animation: robot-game-pop 0.3s ease;
}

.robot-game__overlay-emoji {
  font-size: 34px;
  line-height: 1;
}

.robot-game__overlay-text {
  margin: 0;
  font-size: 15px;
  line-height: 1.45;
  color: var(--ca-text);
}

.robot-game__status {
  margin: 14px 0 0;
  font-size: 13px;
  font-weight: 500;
  text-align: center;
  color: var(--ca-text-muted);
}

.robot-game__program {
  margin-top: 16px;
}

.robot-game__program-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 10px;
  margin-bottom: 8px;
}

.robot-game__program-title {
  font-family: var(--ca-font-display);
  font-weight: 600;
  font-size: 14px;
  color: var(--ca-text);
}

.robot-game__counter {
  font-family: var(--ca-font-mono);
  font-size: 12px;
  color: var(--ca-text-faint);
}

.robot-game__chips {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  min-height: 40px;
  padding: 10px;
  background: var(--ca-bg-deep);
  border: 1px solid var(--ca-border-soft);
  border-radius: 12px;
}

.robot-game__chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 5px 10px;
  font-family: var(--ca-font-mono);
  font-size: 13px;
  background: var(--ca-surface-2);
  color: var(--ca-text);
  border-radius: 8px;
  transition:
    background 0.15s ease,
    color 0.15s ease;
}

.robot-game__chip-index {
  font-size: 11px;
  color: var(--ca-text-faint);
}

.robot-game__chip--active {
  background: var(--ca-orange);
  color: var(--ca-on-primary);
}
.robot-game__chip--active .robot-game__chip-index {
  color: var(--ca-on-primary);
}

.robot-game__chip--error {
  background: var(--ca-danger-soft);
  color: var(--ca-danger-ink);
}
.robot-game__chip--error .robot-game__chip-index {
  color: var(--ca-danger-ink);
}

.robot-game__chip--done {
  color: var(--ca-text-muted);
  opacity: 0.7;
}

.robot-game__chips-empty {
  align-self: center;
  font-size: 13px;
  color: var(--ca-text-faint);
}

.robot-game__controls {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-start;
  gap: 18px;
  margin-top: 18px;
}

.robot-game__dpad {
  display: grid;
  grid-template-columns: repeat(3, 46px);
  grid-template-areas:
    '. up .'
    'left down right';
  gap: 6px;
  justify-content: center;
}

.robot-game__key {
  min-width: 46px;
  min-height: 46px;
  font-size: 20px;
  color: var(--ca-text);
  background: var(--ca-surface-2);
  border: 1px solid var(--ca-border);
  border-radius: 12px;
  cursor: pointer;
  transition:
    background 0.15s ease,
    transform 0.1s ease;
}
.robot-game__key--up {
  grid-area: up;
}
.robot-game__key--left {
  grid-area: left;
}
.robot-game__key--down {
  grid-area: down;
}
.robot-game__key--right {
  grid-area: right;
}
.robot-game__key:hover:not(:disabled) {
  background: var(--ca-orange-soft);
}
.robot-game__key:active:not(:disabled) {
  transform: translateY(1px);
}
.robot-game__key:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.robot-game__actions {
  flex: 1;
  min-width: 160px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.robot-game__edit {
  display: flex;
  gap: 8px;
}

.robot-game__mini {
  flex: 1;
  padding: 9px 10px;
  font-family: var(--ca-font-body);
  font-size: 13px;
  color: var(--ca-text-muted);
  background: transparent;
  border: 1px solid var(--ca-border-strong);
  border-radius: 10px;
  cursor: pointer;
  transition:
    border-color 0.15s ease,
    color 0.15s ease;
}
.robot-game__mini:hover:not(:disabled) {
  color: var(--ca-text-bright);
  border-color: var(--ca-border-strong-2);
}
.robot-game__mini:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}

.robot-game :where(button):focus-visible {
  outline: 2px solid var(--ca-azure);
  outline-offset: 2px;
}

@keyframes robot-game-twinkle {
  0%,
  100% {
    transform: scale(1);
  }
  50% {
    transform: scale(1.18);
  }
}

@keyframes robot-game-pop {
  from {
    transform: scale(0.7);
    opacity: 0;
  }
  to {
    transform: scale(1);
    opacity: 1;
  }
}
</style>
