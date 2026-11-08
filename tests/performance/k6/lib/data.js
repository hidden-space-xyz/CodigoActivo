import { EMAIL_DOMAIN, PASSWORD } from './config.js'

/** Identifiers of the catalog rows the API seeds with fixed values. */
export const LOOKUPS = {
  userTypes: { member: 'b0df7ac6-1312-412f-9c2a-88e6cdfb6e1c' },
  roles: {
    leader: '5bd627de-831a-4169-874e-26a90550db9f',
    volunteer: '3b31564d-9879-434c-9152-5907db0c46fb',
    participant: '03a5613e-a2d2-42da-94d2-b6d63c0f01b5',
  },
  statuses: {
    requested: '3d717eeb-de06-44b8-b7df-2cc3e2ce5cb0',
    confirmed: '3c172c13-d238-4f0b-a61b-0a5ffc6a53ba',
  },
  modalities: {
    onSite: '3a7956c5-2346-4fc3-b3e8-10ecc07f1e1f',
    online: '44b62b00-17f5-46c3-ac65-245fbd8e7db2',
  },
}

/** A one-pixel image, sent as a signature and as an attachment. */
export const SIGNATURE_PNG =
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg=='

const firstNames = ['Lucía', 'Hugo', 'Martina', 'Mateo', 'Sofía', 'Leo', 'Julia', 'Pablo', 'Vega']
const lastNames = ['García Prieto', 'Fernández Rey', 'López Vidal', 'Martín Soto', 'Díaz Cano']
const words = ['robótica', 'scratch', 'python', 'arduino', 'videojuegos', 'web', 'datos', 'taller']
const nationalIds = ['12345678Z', '87654321X', '45678912S', '23456789D', '76543210S', '31415926L']

/** Title prefix of what the back office scenario creates and deletes, so nobody opens it. */
export const DRAFT = 'Borrador k6'

/** Leaves out the drafts of the back office scenario. */
export function published(items) {
  return items.filter((item) => !item.title?.startsWith(DRAFT))
}

/** Picks the item at `index`, wrapping around, or a random one without it. */
export function pick(items, index) {
  if (!items.length) {
    return undefined
  }
  return items[index === undefined ? randomInt(0, items.length - 1) : index % items.length]
}

/** Draws an unbiased integer between both limits. */
export function randomInt(min, max) {
  const range = max - min + 1
  const unbiased = 2 ** 32 - (2 ** 32 % range)
  const value = new DataView(crypto.getRandomValues(new Uint8Array(4)).buffer).getUint32(0)
  return value < unbiased ? min + (value % range) : randomInt(min, max)
}

/** Draws a word for a search or a text. */
export function word() {
  return pick(words)
}

/** Builds the rich text document the API stores. */
export function richText(text, image) {
  const content = [{ type: 'paragraph', content: [{ type: 'text', text }] }]
  if (image) {
    content.push({ type: 'image', attrs: { src: `/api/files/${image}/content`, alt: text } })
  }
  return JSON.stringify({ type: 'doc', content })
}

function phone() {
  return `6${randomInt(10000000, 99999999)}`
}

/** Builds an address of the domain the suite owns. */
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

/** Formats the date `offset` days from today. */
export function day(offset) {
  const date = new Date()
  date.setUTCHours(12, 0, 0, 0)
  date.setUTCDate(date.getUTCDate() + offset)
  return date.toISOString().slice(0, 10)
}

function minor() {
  return { ...person(), birthDate: day(-365 * randomInt(8, 15) - 30), declaresGuardianship: true }
}

/** Builds a registration with the given number of children. */
export function registration(address, minors = 0) {
  return {
    ...person(),
    email: address,
    phone: phone(),
    password: PASSWORD,
    nationalId: pick(nationalIds),
    declaresAdult: true,
    newsConsent: false,
    minors: Array.from({ length: minors }, () => minor()),
  }
}

/** Builds an event whose signup is open now. */
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

/** Builds an activity of an event; slots an hour apart overlap. */
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

/**
 * Accepts every terms document, signing those that ask for it and every consent, which a dependent
 * of 14 or more must sign.
 */
export function decisionsFor(documents) {
  return documents.map((document) => ({
    termsDocumentId: document.termsDocumentId,
    accepted: true,
    signature: document.requiresSignature || document.kind === 'Consent' ? SIGNATURE_PNG : null,
    version: document.version,
  }))
}
