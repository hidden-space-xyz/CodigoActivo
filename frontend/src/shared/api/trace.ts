const SHORT_TRACE_LENGTH = 8

/** First characters of a trace id, enough to tell requests apart at a glance. */
export function shortTrace(traceId: string): string {
  return traceId.slice(0, SHORT_TRACE_LENGTH)
}
