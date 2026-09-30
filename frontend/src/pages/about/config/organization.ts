import type { TranslationKey } from '@/shared/i18n'

/** Core value card of the page; `soft` is the translucent background of its emoji icon. */
export interface OrganizationValue {
  readonly id: string
  readonly icon: string
  readonly soft: string
  readonly titleKey: TranslationKey
  readonly descriptionKey: TranslationKey
}

/**
 * Recurring activity shown as a numbered step; `number` is the zero-padded badge text and
 * `color`/`soft` are its text and translucent background colors.
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
    soft: 'rgba(255,107,94,0.13)',
    titleKey: 'pages.about.organization.values.free.title',
    descriptionKey: 'pages.about.organization.values.free.description',
  },
  {
    id: 'inclusive',
    icon: '🌍',
    soft: 'rgba(45,212,217,0.13)',
    titleKey: 'pages.about.organization.values.inclusive.title',
    descriptionKey: 'pages.about.organization.values.inclusive.description',
  },
  {
    id: 'community',
    icon: '🤝',
    soft: 'rgba(167,139,250,0.13)',
    titleKey: 'pages.about.organization.values.community.title',
    descriptionKey: 'pages.about.organization.values.community.description',
  },
  {
    id: 'fun',
    icon: '🚀',
    soft: 'rgba(91,229,132,0.13)',
    titleKey: 'pages.about.organization.values.fun.title',
    descriptionKey: 'pages.about.organization.values.fun.description',
  },
]

/** The kinds of activity the association runs every year, numbered in order. */
export const ORGANIZATION_ACTIVITIES: readonly OrganizationActivity[] = [
  {
    id: 'workshops',
    number: '01',
    color: '#5BE584',
    soft: 'rgba(91,229,132,0.13)',
    titleKey: 'pages.about.organization.activities.workshops.title',
    descriptionKey: 'pages.about.organization.activities.workshops.description',
  },
  {
    id: 'annualDay',
    number: '02',
    color: '#2DD4D9',
    soft: 'rgba(45,212,217,0.13)',
    titleKey: 'pages.about.organization.activities.annualDay.title',
    descriptionKey: 'pages.about.organization.activities.annualDay.description',
  },
  {
    id: 'meetAndCode',
    number: '03',
    color: '#A78BFA',
    soft: 'rgba(167,139,250,0.13)',
    titleKey: 'pages.about.organization.activities.meetAndCode.title',
    descriptionKey: 'pages.about.organization.activities.meetAndCode.description',
  },
  {
    id: 'competitions',
    number: '04',
    color: '#FF6B5E',
    soft: 'rgba(255,107,94,0.13)',
    titleKey: 'pages.about.organization.activities.competitions.title',
    descriptionKey: 'pages.about.organization.activities.competitions.description',
  },
]
