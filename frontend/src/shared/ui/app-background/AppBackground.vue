<script setup lang="ts">
interface FloatingGlyph {
  readonly char: string
  readonly top: number
  readonly left: number
  readonly color: string
  readonly size: number
  readonly duration: number
  readonly delay: number
}

const glyphs: readonly FloatingGlyph[] = [
  { char: '{ }', top: 14, left: 7, color: 'var(--ca-orange)', size: 26, duration: 22, delay: 0 },
  { char: '</>', top: 68, left: 13, color: 'var(--ca-azure)', size: 23, duration: 26, delay: 3 },
  {
    char: 'print()',
    top: 31,
    left: 79,
    color: 'var(--ca-lime)',
    size: 18,
    duration: 24,
    delay: 1,
  },
  { char: 'def', top: 81, left: 58, color: 'var(--ca-orange)', size: 21, duration: 20, delay: 5 },
  { char: ';', top: 19, left: 45, color: 'var(--ca-azure)', size: 30, duration: 18, delay: 2 },
  { char: 'for', top: 54, left: 89, color: 'var(--ca-lime)', size: 19, duration: 23, delay: 4 },
  { char: '( )', top: 87, left: 28, color: 'var(--ca-orange)', size: 25, duration: 25, delay: 6 },
  { char: '#', top: 9, left: 67, color: 'var(--ca-lime)', size: 22, duration: 19, delay: 1.5 },
  { char: '=>', top: 43, left: 4, color: 'var(--ca-azure)', size: 21, duration: 21, delay: 3.5 },
  { char: '[ ]', top: 61, left: 47, color: 'var(--ca-orange)', size: 22, duration: 27, delay: 2.5 },
  { char: '0 1', top: 37, left: 31, color: 'var(--ca-lime)', size: 16, duration: 24, delay: 5.5 },
  { char: 'if', top: 90, left: 83, color: 'var(--ca-azure)', size: 19, duration: 20, delay: 0.5 },
  { char: '< >', top: 23, left: 91, color: 'var(--ca-orange)', size: 22, duration: 22, delay: 4.5 },
  { char: '★', top: 49, left: 21, color: 'var(--ca-lime)', size: 22, duration: 18, delay: 6.5 },
  { char: '&&', top: 75, left: 4, color: 'var(--ca-azure)', size: 18, duration: 26, delay: 1.2 },
  { char: '//', top: 6, left: 25, color: 'var(--ca-orange)', size: 20, duration: 23, delay: 3.2 },
]
</script>

<template>
  <div class="bg" aria-hidden="true">
    <div class="bg__grid" />
    <span
      v-for="(glyph, index) in glyphs"
      :key="index"
      class="bg__glyph"
      :style="{
        top: `${glyph.top}%`,
        left: `${glyph.left}%`,
        color: glyph.color,
        fontSize: `${glyph.size}px`,
        animationDuration: `${glyph.duration}s`,
        animationDelay: `${glyph.delay}s`,
      }"
      >{{ glyph.char }}</span
    >
    <div class="bg__vignette" />
  </div>
</template>

<style scoped>
.bg {
  position: fixed;
  inset: 0;
  z-index: -1;
  overflow: hidden;
  pointer-events: none;
  background: var(--ca-bg-deep);
}

.bg__grid {
  position: absolute;
  inset: -2px;
  background-image:
    linear-gradient(to right, var(--ca-grid-line) 1px, transparent 1px),
    linear-gradient(to bottom, var(--ca-grid-line) 1px, transparent 1px);
  background-size: 54px 54px;
  animation: ca-bg-grid 14s linear infinite;
}

.bg__glyph {
  position: absolute;
  font-family: var(--ca-font-mono);
  font-weight: 600;
  letter-spacing: 0.02em;
  white-space: nowrap;
  opacity: var(--ca-glyph-op);
  will-change: transform;
  animation-name: ca-bg-float;
  animation-timing-function: ease-in-out;
  animation-iteration-count: infinite;
}

.bg__vignette {
  position: absolute;
  inset: 0;
  background: radial-gradient(
    ellipse 100% 90% at 50% 40%,
    transparent 45%,
    var(--ca-vignette) 100%
  );
}

@keyframes ca-bg-grid {
  from {
    background-position:
      0 0,
      0 0;
  }
  to {
    background-position:
      54px 54px,
      54px 54px;
  }
}

@keyframes ca-bg-float {
  0%,
  100% {
    transform: translateY(0) rotate(-3deg);
  }
  50% {
    transform: translateY(-24px) rotate(3deg);
  }
}
</style>
