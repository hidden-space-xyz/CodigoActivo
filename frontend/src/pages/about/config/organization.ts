import type { TranslationKey } from '@/shared/i18n'

/**
 * Core value card of the page; `soft` is the theme variable of the translucent background of its
 * emoji icon.
 */
export interface OrganizationValue {
  readonly id: string
  readonly icon: string
  readonly soft: string
  readonly titleKey: TranslationKey
  readonly descriptionKey: TranslationKey
}

/**
 * Recurring activity shown as a numbered step; `number` is the zero-padded badge text and
 * `color`/`soft` are the theme variables of its text and translucent background, so both follow
 * the light and dark themes.
 */
export interface OrganizationActivity {
  readonly id: string
  readonly number: string
  readonly color: string
  readonly soft: string
  readonly titleKey: TranslationKey
  readonly descriptionKey: TranslationKey
}

/** The four values the association stands for, in the order the page shows them. */
export const ORGANIZATION_VALUES: readonly OrganizationValue[] = [
  {
    id: 'free',
    icon: '🎟️',
    soft: 'var(--ca-coral-soft)',
    titleKey: 'pages.about.organization.values.free.title',
    descriptionKey: 'pages.about.organization.values.free.description',
  },
  {
    id: 'inclusive',
    icon: '🌍',
    soft: 'var(--ca-teal-soft)',
    titleKey: 'pages.about.organization.values.inclusive.title',
    descriptionKey: 'pages.about.organization.values.inclusive.description',
  },
  {
    id: 'community',
    icon: '🤝',
    soft: 'var(--ca-violet-soft)',
    titleKey: 'pages.about.organization.values.community.title',
    descriptionKey: 'pages.about.organization.values.community.description',
  },
  {
    id: 'fun',
    icon: '🚀',
    soft: 'var(--ca-mint-soft)',
    titleKey: 'pages.about.organization.values.fun.title',
    descriptionKey: 'pages.about.organization.values.fun.description',
  },
]

/** The kinds of activity the association runs every year, numbered in order. */
export const ORGANIZATION_ACTIVITIES: readonly OrganizationActivity[] = [
  {
    id: 'workshops',
    number: '01',
    color: 'var(--ca-mint-ink)',
    soft: 'var(--ca-mint-soft)',
    titleKey: 'pages.about.organization.activities.workshops.title',
    descriptionKey: 'pages.about.organization.activities.workshops.description',
  },
  {
    id: 'annualDay',
    number: '02',
    color: 'var(--ca-teal-ink)',
    soft: 'var(--ca-teal-soft)',
    titleKey: 'pages.about.organization.activities.annualDay.title',
    descriptionKey: 'pages.about.organization.activities.annualDay.description',
  },
  {
    id: 'meetAndCode',
    number: '03',
    color: 'var(--ca-violet-ink)',
    soft: 'var(--ca-violet-soft)',
    titleKey: 'pages.about.organization.activities.meetAndCode.title',
    descriptionKey: 'pages.about.organization.activities.meetAndCode.description',
  },
  {
    id: 'competitions',
    number: '04',
    color: 'var(--ca-coral-ink)',
    soft: 'var(--ca-coral-soft)',
    titleKey: 'pages.about.organization.activities.competitions.title',
    descriptionKey: 'pages.about.organization.activities.competitions.description',
  },
]
