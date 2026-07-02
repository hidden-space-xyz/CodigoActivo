import { ApiError, httpClient } from './http-client'

/**
 * Hand-written OData v4 query layer. Read endpoints live under `/api/odata/<EntitySet>` and are
 * queried directly (the generated client only covers the REST write/report/auth/files surface).
 * The frontend writes every `$filter`/`$orderby`/`$top`/`$skip`/`$count` per screen — there is no
 * per-list query code on the backend.
 */

const ODATA_BASE = '/api/odata'

/** Raw OData collection envelope. */
export interface ODataList<T> {
  readonly value: T[]
  readonly '@odata.count'?: number
}

/** Normalized page: the items plus the server total (for a paginator). */
export interface ODataPage<T> {
  readonly items: T[]
  readonly total: number
}

export interface ODataQuery {
  readonly filter?: string | undefined
  readonly orderBy?: string | undefined
  readonly top?: number | undefined
  readonly skip?: number | undefined
  readonly count?: boolean | undefined
  readonly select?: string | undefined
}

export type ODataFieldType = 'text' | 'numeric' | 'boolean' | 'date' | 'datetime' | 'guid'

export function buildODataQueryString(query: ODataQuery): string {
  const params: string[] = []
  if (query.filter) params.push(`$filter=${encodeURIComponent(query.filter)}`)
  if (query.orderBy) params.push(`$orderby=${encodeURIComponent(query.orderBy)}`)
  if (typeof query.top === 'number') params.push(`$top=${query.top}`)
  if (typeof query.skip === 'number') params.push(`$skip=${query.skip}`)
  if (query.count) params.push('$count=true')
  if (query.select) params.push(`$select=${encodeURIComponent(query.select)}`)
  return params.join('&')
}

/** Fetch an OData collection and normalize it to `{ items, total }`. */
export async function fetchODataList<T>(
  resource: string,
  query: ODataQuery = {},
): Promise<ODataPage<T>> {
  const qs = buildODataQueryString(query)
  const url = `${ODATA_BASE}/${resource}${qs ? `?${qs}` : ''}`
  const response = await httpClient<{ data: ODataList<T> }>(url)
  const body = response.data
  return {
    items: body?.value ?? [],
    total: body?.['@odata.count'] ?? body?.value?.length ?? 0,
  }
}

/** Fetch a single OData entity by key; returns null on 404. */
export async function fetchODataEntity<T>(resource: string, key: string): Promise<T | null> {
  const url = `${ODATA_BASE}/${resource}(${key})`
  try {
    const response = await httpClient<{ data: T }>(url)
    return response.data ?? null
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null
    throw error
  }
}

/** Serialize unbound-function parameters as `p1=v1,p2=v2`. Values are spliced raw (OData v4 leaves
 *  guids/numbers unquoted), so callers must pass pre-validated literals (e.g. via `odataGuid`). */
function buildFunctionParams(params: Record<string, string | number>): string {
  return Object.entries(params)
    .map(([key, value]) => `${key}=${value}`)
    .join(',')
}

/**
 * Invoke an unbound OData function returning a single value: `GET /api/odata/Name(p1=v1,...)`.
 * A single complex/entity result is serialized at the top level (next to `@odata.context`), so the
 * parsed body already IS the value.
 */
export async function fetchODataFunction<T>(
  name: string,
  params: Record<string, string | number> = {},
): Promise<T> {
  const url = `${ODATA_BASE}/${name}(${buildFunctionParams(params)})`
  const response = await httpClient<{ data: T }>(url)
  return response.data
}

/**
 * Invoke an unbound OData function returning a collection: `GET /api/odata/Name(p1=v1,...)`.
 * The result is the OData envelope `{ value: T[] }`; this unwraps it to the array (empty if absent).
 */
export async function fetchODataFunctionList<T>(
  name: string,
  params: Record<string, string | number> = {},
): Promise<T[]> {
  const url = `${ODATA_BASE}/${name}(${buildFunctionParams(params)})`
  const response = await httpClient<{ data: { value?: T[] } }>(url)
  return response.data?.value ?? []
}

/** Combine filter clauses with `and`, dropping empties. */
export function combineFilters(...clauses: Array<string | null | undefined>): string | undefined {
  const parts = clauses.filter((clause): clause is string => Boolean(clause))
  return parts.length ? parts.map((p) => `(${p})`).join(' and ') : undefined
}

const GUID_RE = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/

/**
 * Safe OData literal for a GUID spliced into a raw `$filter` clause (guids are unquoted in OData v4).
 * Throws on a non-GUID so a malformed/hostile id can never break out of the clause (filter injection).
 */
export function odataGuid(value: string): string {
  if (!GUID_RE.test(value)) throw new Error(`Invalid GUID for OData filter: ${value}`)
  return value
}

/** Safe OData integer literal for a raw `$filter` clause; throws on a non-integer value. */
export function odataInt(value: number | string): string {
  const numeric = Number(value)
  if (!Number.isInteger(numeric)) throw new Error(`Invalid integer for OData filter: ${value}`)
  return String(numeric)
}

function pad(value: number): string {
  return String(value).padStart(2, '0')
}

function asDate(value: unknown): Date | null {
  const date = value instanceof Date ? value : new Date(String(value))
  return Number.isNaN(date.getTime()) ? null : date
}

/** Format a raw filter value into its OData literal for the given field type. */
export function formatODataValue(value: unknown, type: ODataFieldType): string | null {
  if (value === null || value === undefined || value === '') return null

  switch (type) {
    case 'numeric': {
      const numeric = Number(value)
      return Number.isNaN(numeric) ? null : String(numeric)
    }
    case 'boolean':
      return value ? 'true' : 'false'
    case 'guid': {
      const guid = String(value)
      return GUID_RE.test(guid) ? guid : null
    }
    case 'date': {
      const date = asDate(value)
      return date
        ? `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
        : null
    }
    case 'datetime': {
      const date = asDate(value)
      return date ? date.toISOString() : null
    }
    case 'text':
    default:
      return `'${String(value).replace(/'/g, "''")}'`
  }
}

/** Build a single `$filter` clause from a PrimeVue match mode + a pre-formatted literal. */
export function buildFilterClause(
  field: string,
  matchMode: string | undefined,
  literal: string,
): string {
  switch (matchMode) {
    case 'startsWith':
      return `startswith(${field},${literal})`
    case 'endsWith':
      return `endswith(${field},${literal})`
    case 'notContains':
      return `not contains(${field},${literal})`
    case 'notEquals':
    case 'dateIsNot':
      return `${field} ne ${literal}`
    case 'lt':
    case 'dateBefore':
      return `${field} lt ${literal}`
    case 'lte':
      return `${field} le ${literal}`
    case 'gt':
    case 'dateAfter':
      return `${field} gt ${literal}`
    case 'gte':
      return `${field} ge ${literal}`
    case 'equals':
    case 'dateIs':
      return `${field} eq ${literal}`
    case 'contains':
      return `contains(${field},${literal})`
    default:
      // Strings default to contains; everything else to equality.
      return literal.startsWith("'") ? `contains(${field},${literal})` : `${field} eq ${literal}`
  }
}
