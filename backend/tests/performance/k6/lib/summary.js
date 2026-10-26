import {
  CAPACITY_SLO,
  CATALOG_ENDPOINTS,
  ENDPOINT_KINDS,
  runMetric,
  stepMetric,
} from './catalog.js'
import { PROFILE, PROFILE_NAME, STEP } from './config.js'

const RESULTS_DIR = __ENV.RESULTS_DIR || '/results'

function values(data, metric) {
  return data.metrics[metric]?.values ?? {}
}

function status(row) {
  if (!row.count) {
    return 'not exercised'
  }
  if (row.p95 > row.budget) {
    return 'over budget'
  }
  return row.errors >= 0.01 ? 'errors' : 'ok'
}

function rows(data) {
  return CATALOG_ENDPOINTS.map(({ name, kind, budget }) => {
    const duration = values(data, runMetric('http_req_duration', `name:${name}`))
    const row = {
      name,
      kind,
      budget,
      count: duration.count || 0,
      med: duration.med || 0,
      p95: duration['p(95)'] || 0,
      p99: duration['p(99)'] || 0,
      max: duration.max || 0,
      errors: values(data, runMetric('http_req_failed', `name:${name}`)).rate || 0,
    }
    return { ...row, ratio: row.p95 / budget, status: status(row) }
  }).sort((a, b) => (b.count > 0) - (a.count > 0) || b.ratio - a.ratio)
}

const ms = (value) => (value >= 100 ? value.toFixed(0) : value.toFixed(1))
const percent = (value) => `${(value * 100).toFixed(2)}%`

function steps(data) {
  return (PROFILE.steps ?? []).map((users) => {
    const duration = values(data, stepMetric('http_req_duration', users))
    const row = {
      users,
      count: duration.count || 0,
      med: duration.med || 0,
      p95: duration['p(95)'] || 0,
      p99: duration['p(99)'] || 0,
      failures: values(data, stepMetric('http_req_failed', users)).rate || 0,
      limited: values(data, stepMetric('rate_limited', users)).count || 0,
      kinds: Object.fromEntries(
        ENDPOINT_KINDS.map((kind) => [
          kind,
          values(data, stepMetric('http_req_duration', users, `kind:${kind}`))['p(95)'] || 0,
        ]),
      ),
    }
    const healthy =
      row.count > 0 &&
      row.p95 < CAPACITY_SLO.p95 &&
      row.p99 < CAPACITY_SLO.p99 &&
      row.failures < CAPACITY_SLO.failures
    return { ...row, rps: row.count / STEP.hold, healthy }
  })
}

function capacity(stepRows) {
  const missed = stepRows.findIndex((row) => !row.healthy)
  if (missed === 0) {
    return `below ${stepRows[0].users} concurrent users`
  }
  if (missed < 0) {
    return `at least ${stepRows[stepRows.length - 1].users} concurrent users (every step met the objective)`
  }
  return `${stepRows[missed - 1].users} concurrent users (${stepRows[missed].users} missed the objective)`
}

const objective = `p95 < ${CAPACITY_SLO.p95} ms, p99 < ${CAPACITY_SLO.p99} ms and failures < ${percent(CAPACITY_SLO.failures)}`

function verdict(row) {
  if (!row.count) {
    return 'not reached'
  }
  return row.healthy ? 'ok' : 'missed'
}

const stepCells = (row) => [
  row.users,
  row.rps.toFixed(0),
  ms(row.med),
  ms(row.p95),
  ms(row.p99),
  percent(row.failures),
  row.limited,
  verdict(row),
]

function capacityText(stepRows) {
  if (!stepRows.length) {
    return ''
  }
  const widths = [7, 8, 8, 8, 8, 8, 7, 12]
  const line = (cells) => cells.map((cell, index) => String(cell).padStart(widths[index])).join(' ')
  return [
    '',
    `Capacity: ${capacity(stepRows)}; objective ${objective}`,
    line(['Users', 'Req/s', 'p50', 'p95', 'p99', 'Failed', '429', 'Verdict']),
    ...stepRows.map((row) => line(stepCells(row))),
    '',
  ].join('\n')
}

function capacityMarkdown(stepRows) {
  if (!stepRows.length) {
    return []
  }
  const kinds = stepRows
    .filter((row) => row.count)
    .map(
      (row) =>
        `| ${row.users} | ${ENDPOINT_KINDS.map((kind) => ms(row.kinds[kind])).join(' | ')} |`,
    )
  return [
    '## Capacity',
    '',
    `Estimated capacity: ${capacity(stepRows)}. Each step holds its users for ${STEP.hold} s; the objective is ${objective}.`,
    '',
    '| Users | Req/s | p50 | p95 | p99 | Failed | 429 | Verdict |',
    '| ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |',
    ...stepRows.map((row) => `| ${stepCells(row).join(' | ')} |`),
    '',
    'p95 in milliseconds by kind of endpoint:',
    '',
    `| Users | ${ENDPOINT_KINDS.join(' | ')} |`,
    `| ---: | ${ENDPOINT_KINDS.map(() => '---:').join(' | ')} |`,
    ...kinds,
    '',
  ]
}

function overview(data, endpoints) {
  const duration = values(data, runMetric('http_req_duration', 'target:api'))
  const exercised = endpoints.filter((row) => row.count).length
  return [
    ['Profile', PROFILE_NAME],
    ['Duration', `${(data.state.testRunDurationMs / 1000).toFixed(0)} s`],
    ['API requests', `${duration.count || 0}`],
    ['API p95 / p99', `${ms(duration['p(95)'] || 0)} / ${ms(duration['p(99)'] || 0)} ms`],
    [
      'API failed requests',
      percent(values(data, runMetric('http_req_failed', 'target:api')).rate || 0),
    ],
    ['Rate limited (429)', `${values(data, runMetric('rate_limited', 'target:api')).count || 0}`],
    [
      'Unexpected responses',
      `${values(data, runMetric('unexpected_responses', 'target:api')).count || 0}`,
    ],
    ['Dropped iterations', `${values(data, 'dropped_iterations').count || 0}`],
    ['Endpoints exercised', `${exercised} / ${endpoints.length}`],
    ['Over budget', `${endpoints.filter((row) => row.status === 'over budget').length}`],
  ]
}

function text(data, endpoints, stepRows) {
  const head = overview(data, endpoints)
    .map(([label, value]) => `  ${label.padEnd(22)} ${value}`)
    .join('\n')
  const columns = ['Endpoint', 'Reqs', 'p50', 'p95', 'p99', 'Max', 'Errors', 'Budget', 'Status']
  const widths = [Math.max(...endpoints.map((row) => row.name.length)), 7, 8, 8, 8, 8, 8, 7, 13]
  const line = (cells) =>
    cells
      .map((cell, index) =>
        index ? String(cell).padStart(widths[index]) : String(cell).padEnd(widths[0]),
      )
      .join(' ')
  const table = endpoints.map((row) =>
    line([
      row.name,
      row.count,
      ms(row.med),
      ms(row.p95),
      ms(row.p99),
      ms(row.max),
      percent(row.errors),
      row.budget,
      row.status,
    ]),
  )
  return `\nCódigo Activo backend performance\n${head}\n${capacityText(stepRows)}\n${line(columns)}\n${table.join('\n')}\n`
}

function markdown(data, endpoints, stepRows) {
  const head = overview(data, endpoints)
    .map(([label, value]) => `| ${label} | ${value} |`)
    .join('\n')
  const hotspots = endpoints.filter(
    (row) => row.status === 'over budget' || row.status === 'errors',
  )
  const table = endpoints
    .map(
      (row) =>
        `| \`${row.name}\` | ${row.kind} | ${row.count} | ${ms(row.med)} | ${ms(row.p95)} | ${ms(row.p99)} | ${ms(row.max)} | ${percent(row.errors)} | ${row.budget} | ${row.status} |`,
    )
    .join('\n')
  const hotspotList = hotspots.length
    ? hotspots
        .map(
          (row) =>
            `- \`${row.name}\`: p95 ${ms(row.p95)} ms (budget ${row.budget} ms), errors ${percent(row.errors)}`,
        )
        .join('\n')
    : '- None'
  return [
    '# Backend performance',
    '',
    '| Metric | Value |',
    '| --- | --- |',
    head,
    '',
    ...capacityMarkdown(stepRows),
    '## Hotspots',
    '',
    hotspotList,
    '',
    '## Endpoints',
    '',
    'Latencies in milliseconds; the budget applies to p95.',
    '',
    '| Endpoint | Kind | Reqs | p50 | p95 | p99 | Max | Errors | Budget | Status |',
    '| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |',
    table,
    '',
  ].join('\n')
}

export function report(data) {
  const endpoints = rows(data)
  const stepRows = steps(data)
  return {
    stdout: text(data, endpoints, stepRows),
    [`${RESULTS_DIR}/summary.md`]: markdown(data, endpoints, stepRows),
    [`${RESULTS_DIR}/summary.json`]: JSON.stringify(
      { profile: PROFILE_NAME, steps: stepRows, endpoints, metrics: data.metrics },
      null,
      2,
    ),
  }
}
