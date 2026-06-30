import { computed } from 'vue'

interface ValueConfig {
  readonly id: string
  readonly icon: string
  readonly soft: string
  readonly title: string
  readonly description: string
}

interface ActivityConfig {
  readonly id: string
  readonly number: string
  readonly color: string
  readonly soft: string
  readonly title: string
  readonly description: string
}

const VALUE_CONFIG: readonly ValueConfig[] = [
  {
    id: 'free',
    icon: '🎟️',
    soft: 'rgba(255,107,94,0.13)',
    title: 'Gratuito',
    description: 'Todas nuestras actividades son siempre sin coste para las familias.',
  },
  {
    id: 'inclusive',
    icon: '🌍',
    soft: 'rgba(45,212,217,0.13)',
    title: 'Inclusivo',
    description: 'Llevamos el código al mundo rural y a quien tiene menos acceso.',
  },
  {
    id: 'community',
    icon: '🤝',
    soft: 'rgba(167,139,250,0.13)',
    title: 'En comunidad',
    description: 'Voluntariado, familias y empresas que reman en la misma dirección.',
  },
  {
    id: 'fun',
    icon: '🚀',
    soft: 'rgba(91,229,132,0.13)',
    title: 'Divertido',
    description: 'Se aprende creando, jugando y compartiendo proyectos propios.',
  },
]

const ACTIVITY_CONFIG: readonly ActivityConfig[] = [
  {
    id: 'workshops',
    number: '01',
    color: '#5BE584',
    soft: 'rgba(91,229,132,0.13)',
    title: 'Talleres de programación',
    description: 'De Scratch y robótica a Python e inteligencia artificial, por edades.',
  },
  {
    id: 'annualDay',
    number: '02',
    color: '#2DD4D9',
    soft: 'rgba(45,212,217,0.13)',
    title: 'El Día Código Activo',
    description: 'Nuestro gran evento anual con un lema y un reto temático distinto cada año.',
  },
  {
    id: 'meetAndCode',
    number: '03',
    color: '#A78BFA',
    soft: 'rgba(167,139,250,0.13)',
    title: 'Meet and Code',
    description:
      'Participamos en la Semana Europea de la Programación con actividades para 8-18 años.',
  },
  {
    id: 'competitions',
    number: '04',
    color: '#FF6B5E',
    soft: 'rgba(255,107,94,0.13)',
    title: 'Competiciones',
    description: 'Preparamos a jóvenes para la Olimpiada Informática y otros campeonatos.',
  },
]

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
