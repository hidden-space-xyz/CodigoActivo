import { computed } from 'vue'

import {
  ACTIVITY_CONFIG,
  VALUE_CONFIG,
} from '@/modules/about/presentation/config/about-content.config'

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

export function useOrganizationContent() {
  const values = computed<OrganizationValue[]>(() =>
    VALUE_CONFIG.map((config) => ({
      id: config.id,
      icon: config.icon,
      soft: config.soft,
      title: config.title,
      description: config.description,
    })),
  )

  const activities = computed<OrganizationActivity[]>(() =>
    ACTIVITY_CONFIG.map((config) => ({
      id: config.id,
      number: config.number,
      color: config.color,
      soft: config.soft,
      title: config.title,
      description: config.description,
    })),
  )

  return { values, activities }
}
