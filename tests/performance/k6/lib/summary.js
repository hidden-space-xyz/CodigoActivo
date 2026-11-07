import { ENDPOINT_KINDS, ENDPOINTS } from './catalog.js'
import { RESULTS_DIR } from './config.js'
import { PROFILE } from './profiles.js'
import { arrivalsMetric, OBJECTIVE, runMetric, windowMetric } from './thresholds.js'

const ms = (value) => (value >= 100 ? value.toFixed(0) : value.toFixed(1))
const percent = (value) => `${(value * 100).toFixed(2)}%`
const objective = `p95 < ${OBJECTIVE.p95} ms, p99 < ${OBJECTIVE.p99} ms, failures < ${percent(OBJECTIVE.failures)} and at least ${percent(OBJECTIVE.delivered)} of its load delivered`

function values(data, metric) {
  return data.metrics[metric]?.values ?? {}
}

function latency(data, metric) {
  const duration = values(data, metric)
  return {
    count: duration.count || 0,
    med: duration.med || 0,
    p95: duration['p(95)'] || 0,
    p99: duration['p(99)'] || 0,
    max: duration.max || 0,
  }
}

function endpointStatus(row) {
  if (!row.count) {
    return 'not exercised'
  }
  if (row.p95 >= row.budget) {
    return 'over budget'
  }
  return row.errors >= OBJECTIVE.failures ? 'errors' : 'ok'
}

function endpointRows(data) {
  return ENDPOINTS.map(({ name, kind, budget }) => {
    const row = {
      name,
      kind,
      budget,
      ...latency(data, runMetric('http_req_duration', `name:${name}`)),
      errors: values(data, runMetric('http_req_failed', `name:${name}`)).rate || 0,
    }
    return { ...row, ratio: row.p95 / budget, status: endpointStatus(row) }
  }).sort((a, b) => (b.count > 0) - (a.count > 0) || b.ratio - a.ratio)
}

function windowRows(data) {
  return PROFILE.windows.map((window) => {
    const row = {
      ...window,
      ...latency(data, windowMetric('http_req_duration', window.name)),
      failures: values(data, windowMetric('http_req_failed', window.name)).rate || 0,
      limited: values(data, windowMetric('rate_limited', window.name)).count || 0,
      delivered: (values(data, arrivalsMetric(window.name)).count || 0) / window.arrivals,
      kinds: Object.fromEntries(
        ENDPOINT_KINDS.map((kind) => [
          kind,
          values(data, windowMetric('http_req_duration', window.name, `kind:${kind}`))['p(95)'] ||
            0,
        ]),
      ),
    }
    const met =
      row.count > 0 &&
      row.p95 < OBJECTIVE.p95 &&
      row.p99 < OBJECTIVE.p99 &&
      row.failures < OBJECTIVE.failures &&
      row.delivered >= OBJECTIVE.delivered
    const verdict = row.count ? (met ? 'met' : 'missed') : 'not reached'
    return { ...row, met, verdict, rps: row.count / (window.to - window.from) }
  })
}

function table(columns, rows) {
  const cells = rows.map((row) => columns.map(({ value }) => String(value(row))))
  const widths = columns.map(({ title }, index) =>
    Math.max(title.length, 3, ...cells.map((line) => line[index].length)),
  )
  const pad = (text, index) =>
    columns[index].right ? text.padStart(widths[index]) : text.padEnd(widths[index])
  const line = (texts) => `| ${texts.map(pad).join(' | ')} |`
  const rule = widths.map((width, index) =>
    columns[index].right ? `${'-'.repeat(width - 1)}:` : '-'.repeat(width),
  )
  return [line(columns.map(({ title }) => title)), `| ${rule.join(' | ')} |`, ...cells.map(line)]
}

const column = (title, value, right = true) => ({ title, value, right })

function overview(data, endpoints) {
  const api = latency(data, runMetric('http_req_duration', 'target:api'))
  const count = (metric) => `${values(data, metric).count || 0}`
  const rows = [
    ['Profile', PROFILE.name],
    ['Duration', `${(data.state.testRunDurationMs / 1000).toFixed(0)} s`],
    ['API requests', `${api.count}`],
    ['API p95 / p99', `${ms(api.p95)} / ${ms(api.p99)} ms`],
    [
      'API failed requests',
      percent(values(data, runMetric('http_req_failed', 'target:api')).rate || 0),
    ],
    ['Rate limited (429)', count(runMetric('rate_limited', 'target:api'))],
    ['Unanswered (no status)', count(runMetric('http_reqs', 'target:api,status:0'))],
    ['Dropped iterations', count('dropped_iterations')],
    ['Emailed code p95', `${ms(latency(data, 'mail_delay').p95)} ms`],
    ['Endpoints exercised', `${endpoints.filter((row) => row.count).length} / ${endpoints.length}`],
    ['Over budget', `${endpoints.filter((row) => row.status === 'over budget').length}`],
  ]
  return table([column('Metric', ([label]) => label, false), column('Value', ([, v]) => v)], rows)
}

function capacity(windows) {
  const steps = windows.filter((row) => row.users && row.count)
  if (!steps.length) {
    return []
  }
  const missed = steps.findIndex((row) => !row.met)
  if (missed === 0) {
    return [`Estimated capacity: below ${steps[0].users} concurrent users.`, '']
  }
  if (missed < 0) {
    return [`Estimated capacity: at least ${steps[steps.length - 1].users} concurrent users.`, '']
  }
  return [
    `Estimated capacity: ${steps[missed - 1].users} concurrent users (${steps[missed].users} missed the objective).`,
    '',
  ]
}

function windowsSection(windows) {
  if (!windows.length) {
    return []
  }
  const reached = windows.filter((row) => row.count)
  return [
    '## Windows',
    '',
    `Each window measures the API while its load holds; the objective is ${objective}.`,
    '',
    ...capacity(windows),
    ...table(
      [
        column('Window', (row) => row.name, false),
        column('Load', (row) => percent(row.delivered)),
        column('Req/s', (row) => row.rps.toFixed(0)),
        column('p50', (row) => ms(row.med)),
        column('p95', (row) => ms(row.p95)),
        column('p99', (row) => ms(row.p99)),
        column('Failed', (row) => percent(row.failures)),
        column('429', (row) => row.limited),
        column('Verdict', (row) => row.verdict, false),
      ],
      windows,
    ),
    '',
    'p95 in milliseconds by kind of endpoint:',
    '',
    ...table(
      [
        column('Window', (row) => row.name, false),
        ...ENDPOINT_KINDS.map((kind) => column(kind, (row) => ms(row.kinds[kind]))),
      ],
      reached,
    ),
    '',
  ]
}

function hotspots(endpoints) {
  const flagged = endpoints.filter(({ status }) => status !== 'ok')
  if (!flagged.length) {
    return ['- None']
  }
  return flagged.map((row) =>
    row.count
      ? `- \`${row.name}\`: p95 ${ms(row.p95)} ms (budget ${row.budget} ms), errors ${percent(row.errors)}`
      : `- \`${row.name}\`: not exercised`,
  )
}

function markdown(data, endpoints, windows) {
  return [
    '# Backend performance',
    '',
    ...overview(data, endpoints),
    '',
    ...windowsSection(windows),
    '## Hotspots',
    '',
    ...hotspots(endpoints),
    '',
    '## Endpoints',
    '',
    'Latencies in milliseconds; the budget applies to p95.',
    '',
    ...table(
      [
        column('Endpoint', (row) => `\`${row.name}\``, false),
        column('Kind', (row) => row.kind, false),
        column('Reqs', (row) => row.count),
        column('p50', (row) => ms(row.med)),
        column('p95', (row) => ms(row.p95)),
        column('p99', (row) => ms(row.p99)),
        column('Max', (row) => ms(row.max)),
        column('Errors', (row) => percent(row.errors)),
        column('Budget', (row) => row.budget),
        column('Status', (row) => row.status, false),
      ],
      endpoints,
    ),
    '',
  ].join('\n')
}

/** Writes the summary to the console and the results folder. */
export function report(data) {
  const endpoints = endpointRows(data)
  const windows = windowRows(data)
  const summary = markdown(data, endpoints, windows)
  return {
    stdout: `\n${summary}`,
    [`${RESULTS_DIR}/summary.md`]: summary,
    [`${RESULTS_DIR}/summary.json`]: JSON.stringify(
      { profile: PROFILE.name, windows, endpoints, metrics: data.metrics },
      null,
      2,
    ),
  }
}
