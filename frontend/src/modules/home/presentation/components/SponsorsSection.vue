<script setup lang="ts">
import { useSponsorCarousel } from '@/modules/home/presentation/composables/useSponsorCarousel'
import { useSponsors } from '@/modules/home/presentation/composables/useSponsors'
import { fileContentUrl } from '@/shared/utils/media'

const { sponsors } = useSponsors()
const { cards, next, prev, pause, resume } = useSponsorCarousel(sponsors)

const LOGO_PALETTE = [
  'var(--ca-cyan)',
  'var(--ca-purple)',
  'var(--ca-green)',
  'var(--ca-coral)',
  'var(--ca-amber)',
  'var(--ca-blue)',
] as const

function colorFor(id: string): string {
  let hash = 0
  for (let i = 0; i < id.length; i += 1) hash = (hash + id.charCodeAt(i)) % LOGO_PALETTE.length
  return LOGO_PALETTE[hash] ?? LOGO_PALETTE[0]
}

function initials(name: string): string {
  const words = name.trim().split(/\s+/)
  if (words.length >= 2 && words[0] && words[1]) {
    return (words[0][0] ?? '') + (words[1][0] ?? '')
  }
  return (words[0] ?? '').slice(0, 2)
}

function logoUrl(thumbnailId: string): string {
  return fileContentUrl(thumbnailId)
}

function distanceOf(offset: number): number {
  return Math.min(Math.abs(offset), 3)
}
</script>

<template>
  <section class="sponsors">
    <div class="ca-container">
      <div class="sponsors__heading">Con el apoyo de</div>

      <div class="sponsors__carousel" @mouseenter="pause" @mouseleave="resume">
        <button
          type="button"
          class="sponsors__arrow"
          aria-label="Patrocinador anterior"
          @click="prev"
        >
          ‹
        </button>

        <div class="sponsors__viewport">
          <div class="sponsors__track">
            <component
              :is="card.sponsor.website ? 'a' : 'div'"
              v-for="card in cards"
              :key="card.sponsor.id"
              :href="card.sponsor.website || null"
              :target="card.sponsor.website ? '_blank' : null"
              :rel="card.sponsor.website ? 'noopener' : null"
              class="sponsor"
              :class="`sponsor--d${distanceOf(card.offset)}`"
              :style="{ '--offset': String(card.offset) }"
              :aria-hidden="distanceOf(card.offset) === 3 ? 'true' : undefined"
            >
              <div
                class="sponsor__logo"
                :class="{ 'sponsor__logo--color': !logoUrl(card.sponsor.thumbnailId) }"
                :style="{ '--logo-color': colorFor(card.sponsor.id) }"
              >
                <img
                  v-if="logoUrl(card.sponsor.thumbnailId)"
                  :src="logoUrl(card.sponsor.thumbnailId)"
                  :alt="card.sponsor.name"
                  class="sponsor__logo-img"
                  loading="lazy"
                />
                <span v-else>{{ initials(card.sponsor.name).toUpperCase() }}</span>
              </div>
              <div class="sponsor__name">{{ card.sponsor.name }}</div>
            </component>
          </div>
        </div>

        <button
          type="button"
          class="sponsors__arrow"
          aria-label="Patrocinador siguiente"
          @click="next"
        >
          ›
        </button>
      </div>
    </div>
  </section>
</template>

<style scoped>
.sponsors {
  padding: 24px 24px 72px;
}

.sponsors__heading {
  text-align: center;
  font-family: var(--ca-font-mono);
  font-size: 12px;
  color: var(--ca-text-faint);
  letter-spacing: 0.12em;
  text-transform: uppercase;
  margin-bottom: 30px;
}

.sponsors__carousel {
  display: flex;
  align-items: center;
  gap: 12px;
}

.sponsors__arrow {
  flex: 0 0 auto;
  width: 42px;
  height: 42px;
  border-radius: 50%;
  border: 1px solid var(--ca-border-strong);
  background: var(--ca-surface);
  color: var(--ca-text);
  font-size: 22px;
  line-height: 1;
  cursor: pointer;
  z-index: 4;
  transition:
    border-color 0.15s ease,
    color 0.15s ease,
    transform 0.15s ease;
}

.sponsors__arrow:hover {
  border-color: var(--ca-cyan);
  color: var(--ca-cyan);
  transform: translateY(-1px);
}

.sponsors__viewport {
  flex: 1 1 auto;
  min-width: 0;
  overflow: hidden;
}

.sponsors__track {
  position: relative;
  height: 160px;
}

.sponsor {
  --step: clamp(94px, 22vw, 150px);
  --scale: 1;
  position: absolute;
  left: 50%;
  top: 50%;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
  text-decoration: none;
  color: inherit;
  transform: translate(-50%, -50%) translateX(calc(var(--offset) * var(--step))) scale(var(--scale));
  transition:
    transform 0.6s cubic-bezier(0.16, 1, 0.3, 1),
    filter 0.6s ease,
    opacity 0.6s ease;
}

.sponsor__logo {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 82px;
  height: 82px;
  border-radius: 18px;
  overflow: hidden;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 28px;
  color: var(--ca-bg);
  box-shadow: 0 12px 30px rgba(0, 0, 0, 0.35);
}

.sponsor__logo--color {
  border: none;
  background: linear-gradient(
    140deg,
    var(--logo-color),
    color-mix(in srgb, var(--logo-color) 55%, #ffffff)
  );
}

.sponsor__logo-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}

.sponsor__name {
  font-family: var(--ca-font-display);
  font-weight: 600;
  font-size: 15px;
  color: var(--ca-text);
  white-space: nowrap;
}

.sponsor--d0 {
  --scale: 1.1;
  z-index: 3;
}

.sponsor--d1 {
  --scale: 0.82;
  filter: grayscale(1);
  opacity: 0.6;
  z-index: 2;
}

.sponsor--d2 {
  --scale: 0.64;
  filter: grayscale(1);
  opacity: 0.32;
  z-index: 1;
}

.sponsor--d3 {
  --scale: 0.5;
  filter: grayscale(1);
  opacity: 0;
  z-index: 0;
}

@media (max-width: 640px) {
  .sponsor__logo {
    width: 64px;
    height: 64px;
    font-size: 22px;
  }
  .sponsor__name {
    font-size: 13px;
  }
}
</style>
