<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { DataState } from '@/shared/ui'

import { useEventBadges } from '@/features/manage-events'
import type { EventBadgeResponse } from '@/shared/api/generated/models'

const BADGES_PER_SHEET = 16
const MAX_ACTIVITY_CHIPS = 6

// Vue cannot scope an @page at-rule, so declaring it in <style> would leak it into every
// other page's print layout for the rest of the session; attach it only while mounted.
const pageStyle = document.createElement('style')
pageStyle.textContent = '@page { size: A4 portrait; margin: 0; }'
onMounted(() => document.head.appendChild(pageStyle))
onBeforeUnmount(() => pageStyle.remove())

const route = useRoute()
const eventId = computed(() => String(route.params.eventId))

const report = useEventBadges(eventId)
const eventTitle = computed(() => report.data.value?.title ?? '')
const badges = computed(() => report.data.value?.badges ?? [])

const sheets = computed(() => {
  const pages: EventBadgeResponse[][] = []
  for (let i = 0; i < badges.value.length; i += BADGES_PER_SHEET) {
    pages.push(badges.value.slice(i, i + BADGES_PER_SHEET))
  }
  return pages
})

// Text scales per badge instead of using fixed sizes. Two independent passes:
// 1) width pass — the full name gets its own factor (--name-fit) so it always
//    occupies a SINGLE line, growing on short names and shrinking on long ones,
//    which frees vertical room for the activity list;
// 2) height pass — a binary search picks the largest body factor (--fit) whose
//    activity list still fits the room the band and footer leave in the 37mm label.
const MIN_FIT = 0.6
const MAX_FIT = 1.5
const NAME_MIN_FIT = 0.45
const NAME_MAX_FIT = 1.6

const rootEl = ref<HTMLElement | null>(null)

function fitBadge(el: HTMLElement): void {
  const body = el.querySelector<HTMLElement>('.badge__body')
  if (!body) return

  const fits = (value: number): boolean => {
    el.style.setProperty('--fit', value.toFixed(3))
    return body.scrollHeight <= body.clientHeight + 1
  }
  const setNameFit = (value: number): void => {
    el.style.setProperty('--name-fit', value.toFixed(3))
  }

  const name = el.querySelector<HTMLElement>('.badge__name')
  let nameFit = 1
  if (name && name.scrollWidth > 0) {
    setNameFit(1)
    const style = getComputedStyle(body)
    const available =
      body.clientWidth -
      Number.parseFloat(style.paddingLeft) -
      Number.parseFloat(style.paddingRight)
    nameFit = Math.min(NAME_MAX_FIT, (available / name.scrollWidth) * 0.97)
    setNameFit(nameFit)
    // Glyph widths don't scale perfectly linearly with font-size, so re-measure
    // and correct until the name truly fits: it must never overflow the label.
    for (let i = 0; i < 5 && name.scrollWidth > available; i += 1) {
      nameFit *= Math.min((available / name.scrollWidth) * 0.97, 0.97)
      setNameFit(nameFit)
    }
  }

  if (fits(MAX_FIT)) return
  let low = MIN_FIT
  let high = MAX_FIT
  for (let i = 0; i < 7; i += 1) {
    const mid = (low + high) / 2
    if (fits(mid)) low = mid
    else high = mid
  }
  // Dense badges: if even the smallest body scale overflows, trade name size for room.
  while (!fits(low) && name && nameFit > NAME_MIN_FIT) {
    nameFit = Math.max(NAME_MIN_FIT, nameFit * 0.9)
    setNameFit(nameFit)
  }
}

function fitAllBadges(): void {
  for (const el of rootEl.value?.querySelectorAll<HTMLElement>('.badge') ?? []) {
    fitBadge(el)
  }
}

watch(
  sheets,
  async () => {
    await nextTick()
    // Font metrics change line wrapping, so wait for the webfonts before measuring.
    await document.fonts.ready
    fitAllBadges()
  },
  { immediate: true },
)

// Colors come from the user-type catalog and can be arbitrary (Participante is
// seeded as #FFFFFF): too-light accents would leave the band unreadable on paper.
const FALLBACK_ACCENT = '#475569'

function accentColor(badge: EventBadgeResponse): string {
  const raw = (badge.userTypeColor ?? '').trim()
  if (!/^#([0-9a-f]{3}|[0-9a-f]{6})$/i.test(raw)) return FALLBACK_ACCENT
  const hex = raw.length === 4 ? `#${raw[1]}${raw[1]}${raw[2]}${raw[2]}${raw[3]}${raw[3]}` : raw
  const red = Number.parseInt(hex.slice(1, 3), 16)
  const green = Number.parseInt(hex.slice(3, 5), 16)
  const blue = Number.parseInt(hex.slice(5, 7), 16)
  const luminance = (0.299 * red + 0.587 * green + 0.114 * blue) / 255
  return luminance > 0.82 ? FALLBACK_ACCENT : hex
}

function fullName(badge: EventBadgeResponse): string {
  return [badge.firstName, badge.lastName].filter(Boolean).join(' ') || '—'
}

function guardianName(badge: EventBadgeResponse): string {
  return [badge.guardian?.firstName, badge.guardian?.lastName].filter(Boolean).join(' ')
}

function visibleActivities(badge: EventBadgeResponse): string[] {
  return (badge.activities ?? []).slice(0, MAX_ACTIVITY_CHIPS)
}

function hiddenActivityCount(badge: EventBadgeResponse): number {
  return Math.max(0, (badge.activities?.length ?? 0) - MAX_ACTIVITY_CHIPS)
}
</script>

<template>
  <div ref="rootEl" class="badges">
    <div class="back-row no-print">
      <RouterLink :to="{ name: 'admin-event-detail', params: { eventId } }" class="back">
        ← Volver al evento
      </RouterLink>
    </div>

    <DataState
      class="no-print"
      :loading="report.isLoading.value"
      :error="report.isError.value"
      :empty="badges.length === 0"
      empty-text="Este evento no tiene asignaciones confirmadas, así que no hay etiquetas que imprimir."
    >
      <span />
    </DataState>

    <div v-for="(sheet, sheetIndex) in sheets" :key="sheetIndex" class="sheet">
      <article
        v-for="badge in sheet"
        :key="badge.userId"
        class="badge"
        :style="{ '--accent': accentColor(badge) }"
      >
        <header class="badge__band">
          <span class="badge__brand">
            <span class="badge__brand-mark">&lt;/&gt;</span>
            Código Activo
          </span>
          <span class="badge__event">{{ eventTitle }}</span>
        </header>

        <div class="badge__body">
          <h2 class="badge__name">{{ fullName(badge) }}</h2>

          <ul v-if="badge.activities?.length" class="badge__activities">
            <li
              v-for="(activity, index) in visibleActivities(badge)"
              :key="index"
              class="badge__activity"
            >
              {{ activity }}
            </li>
            <li v-if="hiddenActivityCount(badge)" class="badge__activity badge__activity--more">
              +{{ hiddenActivityCount(badge) }} más
            </li>
          </ul>
        </div>

        <footer class="badge__footer">
          <span class="badge__type">{{ badge.userTypeName || '—' }}</span>
          <span v-if="badge.guardian" class="badge__guardian">
            <i class="pi pi-user badge__guardian-icon" aria-label="Responsable" />
            <span class="badge__guardian-name">{{ guardianName(badge) || '—' }}</span>
            <span v-if="badge.guardian.phone" class="badge__guardian-phone">
              <i class="pi pi-phone badge__phone-icon" /> {{ badge.guardian.phone }}
            </span>
          </span>
        </footer>
      </article>
    </div>
  </div>
</template>

<style scoped>
.badges {
  min-height: 100vh;
  padding: 24px 16px 48px;
}

.back-row {
  max-width: 210mm;
  margin: 0 auto;
}

.back {
  display: inline-block;
  margin-bottom: 14px;
  color: var(--ca-text-muted);
  text-decoration: none;
  font-size: 14px;
}

.back:hover {
  color: var(--ca-text);
}

.sheet {
  width: 210mm;
  min-height: 297mm;
  margin: 0 auto 24px;
  background: #fff;
  box-shadow: 0 4px 24px rgb(0 0 0 / 0.5);
  display: grid;
  grid-template-columns: repeat(2, 105mm);
  grid-auto-rows: 37mm;
  align-content: start;
}

.badge {
  --fit: 1;
  --name-fit: 1;
  --accent-ink: color-mix(in srgb, var(--accent) 72%, #111827);

  position: relative;
  box-sizing: border-box;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  background:
    radial-gradient(
      140% 120% at 100% 0%,
      color-mix(in srgb, var(--accent) 22%, #fff) 0%,
      transparent 55%
    ),
    linear-gradient(
      165deg,
      #fff 0%,
      color-mix(in srgb, var(--accent) 10%, #fff) 55%,
      color-mix(in srgb, var(--accent) 18%, #fff) 100%
    );
  break-inside: avoid;
  print-color-adjust: exact;
  -webkit-print-color-adjust: exact;
}

/* Python-code watermark filling the whole label body behind the content. */
.badge::after {
  content: 'def confirmar_asistencia(usuario, actividad):\A     asistente = {"nombre": usuario.nombre, "confirmado": True}\A     actividad.inscritos.append(asistente)\A     return f"¡Nos vemos alli, {usuario.nombre}!"\A \A actividades = ["robotica", "scratch", "huerto_urbano", "gymkhana"]\A evento = Evento("Feria de Voluntariado", anio=2026)\A \A for titulo in actividades:\A     taller = evento.crear_actividad(titulo, plazas=20)\A     for peque in taller.lista_espera:\A         confirmar_asistencia(peque, taller)\A \A print(f"{evento.nombre}: {len(evento.inscritos)} inscritos")';
  position: absolute;
  z-index: 0;
  inset: 0;
  padding: 9mm 2.5mm 0;
  font-family: 'Cascadia Code', Consolas, ui-monospace, monospace;
  font-size: 2.4mm;
  line-height: 1.4;
  white-space: pre;
  text-align: right;
  overflow: hidden;
  color: var(--accent);
  opacity: 0.24;
}

.badge__band,
.badge__body,
.badge__footer {
  position: relative;
  z-index: 1;
}

.badge__band {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 3mm;
  min-height: 8mm;
  padding: 1.2mm 3.5mm;
  border-bottom: 0.5mm solid color-mix(in srgb, var(--accent) 45%, #fff);
  background:
    repeating-linear-gradient(115deg, rgb(255 255 255 / 0.07) 0 1.2mm, transparent 1.2mm 3.6mm),
    linear-gradient(120deg, var(--accent), var(--accent-ink));
  color: #fff;
}

.badge__brand {
  display: inline-flex;
  align-items: center;
  gap: 1.6mm;
  font-family: var(--ca-font-display, inherit);
  font-size: 8pt;
  font-weight: 800;
  line-height: 1.1;
  text-transform: uppercase;
  letter-spacing: 0.14em;
  white-space: nowrap;
}

.badge__brand-mark {
  display: inline-flex;
  align-items: center;
  padding: 0.4mm 1mm;
  border-radius: 1mm;
  background: rgb(255 255 255 / 0.18);
  font-size: 6.5pt;
  letter-spacing: 0;
}

.badge__event {
  flex: 1;
  min-width: 0;
  text-align: right;
  font-size: 7.5pt;
  line-height: 1.15;
  opacity: 0.92;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.badge__body {
  flex: 1;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  gap: calc(1.2mm * var(--fit));
  padding: calc(1.5mm * var(--fit)) 3.5mm;
}

.badge__name {
  align-self: flex-start;
  max-width: 100%;
  font-family: var(--ca-font-display, inherit);
  font-size: calc(16pt * var(--name-fit));
  font-weight: 800;
  line-height: 1.05;
  letter-spacing: -0.01em;
  white-space: nowrap;
  color: #0f172a;
  margin: 0;
}

.badge__footer {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  align-items: center;
  gap: 0.4mm 3mm;
  min-height: 5.5mm;
  padding: 0.9mm 3.5mm;
  background: linear-gradient(
    120deg,
    color-mix(in srgb, var(--accent) 32%, #fff),
    color-mix(in srgb, var(--accent) 16%, #fff)
  );
}

.badge__type {
  font-family: var(--ca-font-display, inherit);
  padding: 0.3mm 1.8mm;
  border-radius: 3mm;
  background: rgb(255 255 255 / 0.55);
  font-size: 8pt;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: var(--accent-ink);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.badge__guardian {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  align-items: baseline;
  column-gap: 1.6mm;
  row-gap: 0.2mm;
  min-width: 0;
  text-align: right;
  font-size: 8pt;
}

.badge__guardian-icon {
  font-size: 7pt;
  align-self: center;
  color: var(--accent-ink);
}

.badge__guardian-name {
  font-weight: 700;
  color: #1f2937;
}

.badge__guardian-phone {
  color: #374151;
}

.badge__phone-icon {
  font-size: 6.5pt;
  color: var(--accent-ink);
}

.badge__activities {
  list-style: none;
  display: flex;
  flex-wrap: wrap;
  gap: calc(0.8mm * var(--fit));
  margin: 0;
  padding: 0;
}

.badge__activity {
  display: inline-flex;
  align-items: center;
  gap: calc(1.2mm * var(--fit));
  font-size: calc(7pt * var(--fit));
  line-height: 1;
  padding: calc(1mm * var(--fit)) calc(2mm * var(--fit));
  border-radius: 4mm;
  background: rgb(255 255 255 / 0.65);
  color: #374151;
}

.badge__activity::before {
  content: '';
  flex-shrink: 0;
  width: calc(1.1mm * var(--fit));
  height: calc(1.1mm * var(--fit));
  border-radius: 50%;
  background: var(--accent);
}

.badge__activity--more {
  font-weight: 700;
  color: var(--accent-ink);
}

.badge__activity--more::before {
  display: none;
}

@media print {
  .no-print {
    display: none !important;
  }

  .badges {
    min-height: 0;
    padding: 0;
  }

  .sheet {
    min-height: 0;
    margin: 0;
    box-shadow: none;
    break-after: page;
    page-break-after: always;
  }

  .sheet:last-child {
    break-after: auto;
    page-break-after: auto;
  }
}
</style>
