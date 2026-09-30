<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { fullName } from '@/entities/user'
import { AppIcon } from '@/shared/ui/app-icon'
import { DataState } from '@/shared/ui/data-state'
import { PrintToolbar } from '@/shared/ui/print-toolbar'

import { badgeQueries } from '../api/queries'
import { accentColor, hiddenActivityCount, toSheets, visibleActivities } from '../lib/badges'
import { fitAllBadges } from '../lib/fit-badge'

const props = defineProps<{
  /** Event id from the `/admin/events/:eventId/badges` route param. */
  eventId: string
}>()

const report = useQuery(() => badgeQueries.ofEvent(props.eventId))
const eventTitle = computed(() => report.data.value?.title ?? '')
const badges = computed(() => report.data.value?.badges ?? [])
const sheets = computed(() => toSheets(badges.value))

const rootEl = ref<HTMLElement | null>(null)

watch(
  sheets,
  async () => {
    await nextTick()
    await document.fonts.ready
    fitAllBadges(rootEl.value)
  },
  { immediate: true },
)
</script>

<template>
  <div ref="rootEl" class="badges">
    <PrintToolbar
      :back="{ name: 'admin-event-detail', params: { eventId } }"
      :back-label="$t('pages.admin.eventBadges.back')"
      :print-label="$t('pages.admin.eventBadges.print')"
    />
    <p class="print-hint no-print">{{ $t('pages.admin.eventBadges.printHint') }}</p>

    <DataState
      class="no-print"
      :loading="report.isLoading.value"
      :error="report.isError.value"
      :empty="badges.length === 0"
      :empty-text="$t('pages.admin.eventBadges.emptyText')"
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
            {{ $t('seo.siteName') }}
          </span>
          <span class="badge__event">{{ eventTitle }}</span>
        </header>

        <div class="badge__body">
          <h2 class="badge__name">{{ fullName(badge) }}</h2>

          <ul v-if="badge.activities.length" class="badge__activities">
            <li
              v-for="(activity, index) in visibleActivities(badge)"
              :key="index"
              class="badge__activity"
            >
              <span class="badge__activity-title">{{ activity.title }}</span>
              <span class="badge__activity-location">
                <span
                  class="badge__location-icon"
                  :aria-label="$t('pages.admin.eventBadges.locationAria')"
                >
                  <AppIcon name="map-marker" />
                </span>
                {{ activity.location }}
              </span>
            </li>
            <li v-if="hiddenActivityCount(badge)" class="badge__activity badge__activity--more">
              {{ $t('pages.admin.eventBadges.moreActivities', { n: hiddenActivityCount(badge) }) }}
            </li>
          </ul>
        </div>

        <footer class="badge__footer">
          <span class="badge__type">{{ badge.userTypeName }}</span>
          <span v-if="badge.guardian" class="badge__guardian">
            <span
              class="badge__guardian-icon"
              :aria-label="$t('pages.admin.eventBadges.guardianAria')"
            >
              <AppIcon name="user" />
            </span>
            <span class="badge__guardian-name">{{ badge.guardian.firstName }}</span>
            <span v-if="badge.guardian.phone" class="badge__guardian-phone">
              <span class="badge__phone-icon"><AppIcon name="phone" /></span>
              {{ badge.guardian.phone }}
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

.print-hint {
  max-width: 210mm;
  margin: 0 auto 14px;
  color: var(--ca-text-muted);
  font-size: 13px;
}

.sheet {
  box-sizing: border-box;
  width: 210mm;
  min-height: 297mm;
  margin: 0 auto 24px;
  padding: 21.3mm 8mm;
  background: #fff;
  box-shadow: 0 4px 24px rgb(0 0 0 / 0.5);
  display: grid;
  grid-template-columns: repeat(2, 97mm);
  grid-auto-rows: 42.4mm;
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

.badge::after {
  content: 'def confirmar_asistencia(usuario, actividad):\A     asistente = {"nombre": usuario.nombre, "confirmado": True}\A     actividad.inscritos.append(asistente)\A     return f"¡Nos vemos allí, {usuario.nombre}!"\A \A actividades = ["robotica", "scratch", "huerto_urbano", "gymkhana"]\A evento = Evento("Feria de Voluntariado", anio=2026)\A \A for titulo in actividades:\A     taller = evento.crear_actividad(titulo, plazas=20)\A     for peque in taller.lista_espera:\A         confirmar_asistencia(peque, taller)\A \A print(f"{evento.nombre}: {len(evento.inscritos)} inscritos")';
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
  display: inline-flex;
  align-items: center;
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
  display: inline-flex;
  align-items: center;
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
  position: relative;
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  column-gap: calc(1.4mm * var(--fit));
  max-width: 100%;
  font-size: calc(7pt * var(--fit));
  line-height: 1.2;
  padding: calc(0.8mm * var(--fit)) calc(2mm * var(--fit)) calc(0.8mm * var(--fit))
    calc(4.3mm * var(--fit));
  border-radius: calc(2.4mm * var(--fit));
  background: rgb(255 255 255 / 0.65);
  color: #374151;
}

.badge__activity::before {
  content: '';
  position: absolute;
  top: calc(0.8mm * var(--fit) + 0.6em - 0.55mm * var(--fit));
  left: calc(2mm * var(--fit));
  width: calc(1.1mm * var(--fit));
  height: calc(1.1mm * var(--fit));
  border-radius: 50%;
  background: var(--accent);
}

.badge__activity-title {
  font-weight: 600;
  color: #1f2937;
}

.badge__location-icon {
  display: inline-flex;
  align-items: center;
  font-size: calc(6.5pt * var(--fit));
  color: var(--accent-ink);
}

.badge__activity--more {
  padding-left: calc(2mm * var(--fit));
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
    padding-bottom: 0;
    box-shadow: none;
    break-after: page;
  }

  .sheet:last-child {
    break-after: auto;
  }
}

@media screen and (max-width: 1024px) {
  .badges {
    padding: 16px var(--ca-gutter) 40px;
    overflow-x: auto;
  }
}
</style>
