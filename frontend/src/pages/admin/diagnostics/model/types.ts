/** Severity of a diagnostic entry, from the most to the least verbose. */
export type DiagnosticLevel = 'Trace' | 'Debug' | 'Information' | 'Warning' | 'Error' | 'Critical'

/** One technical event the API logged; `eventName` and `traceId` are empty when it has none. */
export interface DiagnosticEntry {
  readonly id: string
  readonly occurredAt: string
  readonly level: DiagnosticLevel
  readonly category: string
  readonly eventName: string
  readonly message: string
  /** An exception with its stack trace is attached; only the detail carries it. */
  readonly hasException: boolean
  readonly traceId: string
}

/**
 * A diagnostic entry with its exception and the values of its message template, as indented JSON;
 * both are empty when the entry has none.
 */
export interface DiagnosticDetail {
  readonly id: string
  readonly occurredAt: string
  readonly level: DiagnosticLevel
  readonly category: string
  readonly eventName: string
  readonly message: string
  readonly exception: string
  readonly properties: string
  readonly traceId: string
}

/** Filters, sort and page of the diagnostic list; an unset filter keeps every entry. */
export interface DiagnosticListParams {
  readonly search?: string
  readonly level?: DiagnosticLevel
  readonly category?: string
  readonly traceId?: string
  readonly occurredFrom?: string
  readonly occurredTo?: string
  readonly page?: number
  readonly pageSize?: number
  readonly sort?: string
}
