import { EMAIL_DOMAIN, PASSWORD } from './config.js'

export const CATALOG = {
  userTypes: {
    member: 'b0df7ac6-1312-412f-9c2a-88e6cdfb6e1c',
    sponsor: '8e0b7dc4-59d3-4c3b-9a71-4f25c6b0de88',
    participant: '1c038ae8-306f-4785-a5f5-b9c25e5cc4aa',
  },
  userStatuses: { active: '766f114c-6168-4be5-89f2-bae2a7a919e4' },
  roles: {
    leader: '5bd627de-831a-4169-874e-26a90550db9f',
    volunteer: '3b31564d-9879-434c-9152-5907db0c46fb',
    participant: '03a5613e-a2d2-42da-94d2-b6d63c0f01b5',
  },
  statuses: {
    requested: '3d717eeb-de06-44b8-b7df-2cc3e2ce5cb0',
    confirmed: '3c172c13-d238-4f0b-a61b-0a5ffc6a53ba',
    denied: '714c9041-5536-420a-8176-bf745957d80e',
  },
  modalities: {
    onSite: '3a7956c5-2346-4fc3-b3e8-10ecc07f1e1f',
    online: '44b62b00-17f5-46c3-ac65-245fbd8e7db2',
  },
  resourceTypes: {
    internal: '47833436-b131-4536-87a6-737a9680a423',
    external: 'd4b28595-eed5-4728-ad87-8eaf9d8ce754',
  },
}

export const SIGNATURE_PNG =
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg=='

const firstNames = ['Lucía', 'Hugo', 'Martina', 'Mateo', 'Sofía', 'Leo', 'Julia', 'Pablo', 'Vega']
const lastNames = ['García Prieto', 'Fernández Rey', 'López Vidal', 'Martín Soto', 'Díaz Cano']
const words = ['robótica', 'scratch', 'python', 'arduino', 'videojuegos', 'web', 'datos', 'taller']
const nationalIdLetters = 'TRWAGMYFPDXBNJZSQVHLCKE'

export const DRAFT = 'Borrador k6'

export function published(items) {
  return items.filter((item) => !(item.title ?? item.name).startsWith(DRAFT))
}

export function pick(items, index = Math.floor(Math.random() * items.length)) {
  return items[index % items.length]
}

export function randomInt(min, max) {
  return min + Math.floor(Math.random() * (max - min + 1))
}

export function word() {
  return pick(words)
}

export function richText(text, image) {
  const content = [{ type: 'paragraph', content: [{ type: 'text', text }] }]
  if (image) {
    content.push({ type: 'image', attrs: { src: `/api/files/${image}/content`, alt: text } })
  }
  return JSON.stringify({ type: 'doc', content })
}

function nationalId() {
  const number = randomInt(10000000, 99999999)
  return `${number}${nationalIdLetters[number % 23]}`
}

function phone() {
  return `6${randomInt(10000000, 99999999)}`
}

export function email(...parts) {
  return `perf-${parts.join('-')}@${EMAIL_DOMAIN}`.toLowerCase()
}

function person() {
  return {
    firstName: pick(firstNames),
    lastName: pick(lastNames),
    gender: pick(['Male', 'Female']),
  }
}

export function day(offset) {
  const date = new Date()
  date.setUTCHours(12, 0, 0, 0)
  date.setUTCDate(date.getUTCDate() + offset)
  return date.toISOString().slice(0, 10)
}

export function minor(age = randomInt(8, 15)) {
  return { ...person(), birthDate: day(-365 * age - 30), declaresGuardianship: true }
}

export function registration(address, minors = 0) {
  return {
    ...person(),
    email: address,
    phone: phone(),
    password: PASSWORD,
    nationalId: nationalId(),
    declaresAdult: true,
    newsConsent: true,
    minors: Array.from({ length: minors }, () => minor()),
  }
}

export function eventBody({ thumbnailId, categoryIds, terms = [], startsIn = 14, title }) {
  const now = Date.now()
  return {
    title: title ?? `${DRAFT} ${word()} ${now.toString(36)}`,
    subtitle: 'Evento creado por la suite de rendimiento',
    description: richText(`Jornada de ${word()} y ${word()}.`, thumbnailId),
    eventStartsAt: day(startsIn),
    eventEndsAt: day(startsIn + 1),
    earlySignupStartsAt: new Date(now - 3 * 86400000).toISOString(),
    signupStartsAt: new Date(now - 86400000).toISOString(),
    signupEndsAt: `${day(startsIn - 1)}T20:00:00Z`,
    thumbnailId,
    categoryTypeIds: categoryIds,
    termsDocuments: terms,
  }
}

export function activityBody({ thumbnailId, modalityId, roles, startsIn = 14, slot = 0 }) {
  const hour = 8 + (slot % 8)
  const pad = (value) => String(value).padStart(2, '0')
  return {
    title: `Taller de ${word()} ${slot}`,
    description: `Actividad de ${word()} para la suite de rendimiento.`,
    location: 'León',
    activityModalityTypeId: modalityId,
    activityStartsAt: `${day(startsIn)}T${pad(hour)}:00:00Z`,
    activityEndsAt: `${day(startsIn)}T${pad(hour + 1)}:30:00Z`,
    thumbnailId,
    roleCapacities: roles,
  }
}

export function decisionsFor(documents) {
  return documents.map((document) => ({
    termsDocumentId: document.termsDocumentId,
    accepted: true,
    signature: document.requiresSignature ? SIGNATURE_PNG : null,
    version: document.version,
  }))
}
