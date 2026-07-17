import { i18n } from '@/shared/i18n'

export interface OrganizationValue {
  readonly id: string
  readonly title: string
  readonly description: string
  readonly icon: string
  readonly soft: string
}

export interface OrganizationActivity {
  readonly id: string
  readonly title: string
  readonly description: string
  readonly number: string
  readonly color: string
  readonly soft: string
}

const VALUES: readonly OrganizationValue[] = [
  {
    id: 'free',
    icon: '🎟️',
    soft: 'rgba(255,107,94,0.13)',
    title: i18n.global.t('entities.organization.values.free.title'),
    description: i18n.global.t('entities.organization.values.free.description'),
  },
  {
    id: 'inclusive',
    icon: '🌍',
    soft: 'rgba(45,212,217,0.13)',
    title: i18n.global.t('entities.organization.values.inclusive.title'),
    description: i18n.global.t('entities.organization.values.inclusive.description'),
  },
  {
    id: 'community',
    icon: '🤝',
    soft: 'rgba(167,139,250,0.13)',
    title: i18n.global.t('entities.organization.values.community.title'),
    description: i18n.global.t('entities.organization.values.community.description'),
  },
  {
    id: 'fun',
    icon: '🚀',
    soft: 'rgba(91,229,132,0.13)',
    title: i18n.global.t('entities.organization.values.fun.title'),
    description: i18n.global.t('entities.organization.values.fun.description'),
  },
]

const ACTIVITIES: readonly OrganizationActivity[] = [
  {
    id: 'workshops',
    number: '01',
    color: '#5BE584',
    soft: 'rgba(91,229,132,0.13)',
    title: i18n.global.t('entities.organization.activities.workshops.title'),
    description: i18n.global.t('entities.organization.activities.workshops.description'),
  },
  {
    id: 'annualDay',
    number: '02',
    color: '#2DD4D9',
    soft: 'rgba(45,212,217,0.13)',
    title: i18n.global.t('entities.organization.activities.annualDay.title'),
    description: i18n.global.t('entities.organization.activities.annualDay.description'),
  },
  {
    id: 'meetAndCode',
    number: '03',
    color: '#A78BFA',
    soft: 'rgba(167,139,250,0.13)',
    title: i18n.global.t('entities.organization.activities.meetAndCode.title'),
    description: i18n.global.t('entities.organization.activities.meetAndCode.description'),
  },
  {
    id: 'competitions',
    number: '04',
    color: '#FF6B5E',
    soft: 'rgba(255,107,94,0.13)',
    title: i18n.global.t('entities.organization.activities.competitions.title'),
    description: i18n.global.t('entities.organization.activities.competitions.description'),
  },
]

export function useOrganizationContent() {
  return { values: VALUES, activities: ACTIVITIES }
}
