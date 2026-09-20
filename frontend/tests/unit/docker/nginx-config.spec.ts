import { readFileSync } from 'node:fs'
import { join } from 'node:path'

import { describe, expect, it } from 'vitest'

interface NginxBlock {
  header: string
  body: string
  ownStatements: string
  depth: number
}

interface NginxToken {
  kind: 'open' | 'close' | 'end'
  index: number
}

interface NginxMapEntry {
  key: string
  value: string
}

const securityHeaderNames = [
  'X-Content-Type-Options',
  'X-Frame-Options',
  'Referrer-Policy',
  'X-Permitted-Cross-Domain-Policies',
  'X-DNS-Prefetch-Control',
  'Permissions-Policy',
  'Cross-Origin-Opener-Policy',
  'Cross-Origin-Resource-Policy',
  'Strict-Transport-Security',
  'Content-Security-Policy',
]

const proxiedLocationHeaders = [
  'location = /sitemap.xml',
  'location = /robots.txt',
  'location ~* ^/api/auth/(login|register|forgot-password|[^/]+/(verify|resend-verification|reset-password))/?$',
  'location ~* ^/api/users/[^/]+/(password|admin)/?$',
  'location /api/',
]

const proxyApiInclude = 'include /etc/nginx/snippets/proxy-api.conf;'

const dockerDirectory = join(import.meta.dirname, '..', '..', '..', 'docker')

function startsToken(source: string, index: number): boolean {
  if (index === 0) return true

  const previous = source[index - 1] as string

  return /\s/.test(previous) || previous === ';' || previous === '{' || previous === '}'
}

function stripComments(source: string): string {
  let result = ''
  let quote: string | null = null
  let commented = false

  for (let index = 0; index < source.length; index += 1) {
    const char = source[index] as string

    if (char === '\n') {
      commented = false
      result += char
      continue
    }

    if (commented) continue

    if (quote !== null) {
      if (char === quote) quote = null
    } else if (char === '"' || char === "'") {
      quote = char
    } else if (char === '#' && startsToken(source, index)) {
      commented = true
      continue
    }

    result += char
  }

  return result
}

function readConfig(name: string): string {
  return stripComments(readFileSync(join(dockerDirectory, name), 'utf8'))
}

const defaultConf = readConfig('default.conf')
const securityHeadersConf = readConfig('security-headers.conf')
const proxyApiConf = readConfig('proxy-api.conf')
const nginxConf = readConfig('nginx.conf')
const allConfigs = [defaultConf, securityHeadersConf, proxyApiConf, nginxConf]

function normalise(text: string): string {
  return text.replace(/\s+/g, ' ').trim()
}

function opensBlock(config: string, index: number): boolean {
  return index === 0 || /\s/.test(config[index - 1] as string)
}

function closesBlock(config: string, index: number): boolean {
  if (index === 0) return true

  const previous = config[index - 1] as string

  return previous === ';' || /\s/.test(previous)
}

function tokenize(config: string): NginxToken[] {
  const tokens: NginxToken[] = []
  let quote: string | null = null

  for (let index = 0; index < config.length; index += 1) {
    const char = config[index] as string

    if (quote !== null) {
      if (char === quote) quote = null
      continue
    }

    if (char === '"' || char === "'") {
      quote = char
      continue
    }

    if (char === ';') tokens.push({ kind: 'end', index })
    else if (char === '{' && opensBlock(config, index)) tokens.push({ kind: 'open', index })
    else if (char === '}' && closesBlock(config, index)) tokens.push({ kind: 'close', index })
  }

  return tokens
}

function statements(config: string): string[] {
  const collected: string[] = []
  let start = 0

  for (const token of tokenize(config)) {
    if (token.kind === 'end') {
      const statement = normalise(config.slice(start, token.index))
      if (statement.length > 0) collected.push(`${statement};`)
    }
    start = token.index + 1
  }

  return collected
}

function tokenizeStatement(statement: string): string[] {
  const tokens: string[] = []
  let current = ''
  let started = false
  let quote: string | null = null

  for (const char of statement.replace(/;$/, '')) {
    if (quote !== null) {
      if (char === quote) quote = null
      else current += char
      continue
    }

    if (char === '"' || char === "'") {
      quote = char
      started = true
      continue
    }

    if (/\s/.test(char)) {
      if (started) tokens.push(current)
      current = ''
      started = false
      continue
    }

    current += char
    started = true
  }

  if (started) tokens.push(current)

  return tokens
}

function statementsOf(config: string, directive: string): string[] {
  return statements(config).filter(
    (statement) => statement === `${directive};` || statement.startsWith(`${directive} `),
  )
}

function onlyStatement(config: string, directive: string): string {
  const found = statementsOf(config, directive)
  if (found.length !== 1) {
    throw new Error(`expected one "${directive}" statement, found ${found.length}`)
  }

  return found[0] as string
}

function parseBlocks(config: string): NginxBlock[] {
  const blocks: NginxBlock[] = []
  const open: { header: string; bodyStart: number; depth: number }[] = []
  let start = 0

  for (const token of tokenize(config)) {
    if (token.kind === 'open') {
      open.push({
        header: normalise(config.slice(start, token.index)),
        bodyStart: token.index + 1,
        depth: open.length,
      })
    } else if (token.kind === 'close') {
      const block = open.pop()
      if (block !== undefined) {
        const body = config.slice(block.bodyStart, token.index)
        blocks.push({
          header: block.header,
          body,
          ownStatements: stripNestedBlocks(body),
          depth: block.depth,
        })
      }
    }
    start = token.index + 1
  }

  return blocks
}

function directChildren(block: NginxBlock): NginxBlock[] {
  return parseBlocks(block.body).filter((child) => child.depth === 0)
}

function extractBlocks(config: string, keyword: string): NginxBlock[] {
  const headerPattern = new RegExp(`^${keyword}\\b`)

  return parseBlocks(config).filter((block) => headerPattern.test(block.header))
}

function stripNestedBlocks(body: string): string {
  let result = ''
  let pending = ''
  let depth = 0
  let start = 0

  for (const token of tokenize(body)) {
    if (depth === 0) pending += body.slice(start, token.index)

    if (token.kind === 'open') {
      pending = ''
      depth += 1
    } else if (token.kind === 'close') {
      pending = ''
      depth = Math.max(0, depth - 1)
    } else if (depth === 0) {
      result += `${pending};`
      pending = ''
    }

    start = token.index + 1
  }

  if (depth === 0) result += body.slice(start)

  return result
}

function includesOf(block: NginxBlock, include: string): string[] {
  return statementsOf(block.ownStatements, 'include').filter((statement) => statement === include)
}

function findBlock(blocks: NginxBlock[], predicate: (block: NginxBlock) => boolean): NginxBlock {
  const found = blocks.find(predicate)
  if (!found) throw new Error('expected nginx block was not found')

  return found
}

function mapBlock(variable: string): NginxBlock {
  return findBlock(extractBlocks(defaultConf, 'map'), (block) => block.header.endsWith(variable))
}

function mapEntries(block: NginxBlock): NginxMapEntry[] {
  return statements(block.body).map((statement) => {
    const tokens = tokenizeStatement(statement)
    if (tokens.length !== 2) throw new Error(`unreadable map entry "${statement}"`)

    return { key: tokens[0] as string, value: tokens[1] as string }
  })
}

function mapValue(block: NginxBlock, key: string): string {
  const found = mapEntries(block).filter((entry) => entry.key === key)
  if (found.length !== 1) {
    throw new Error(`expected one "${key}" entry in ${block.header}, found ${found.length}`)
  }

  return (found[0] as NginxMapEntry).value
}

function onlyRegexEntry(block: NginxBlock): NginxMapEntry {
  const found = mapEntries(block).filter((entry) => entry.key.startsWith('~'))
  if (found.length !== 1) {
    throw new Error(`expected one regular expression key in ${block.header}, found ${found.length}`)
  }

  return found[0] as NginxMapEntry
}

function mapKeyRegex(entry: NginxMapEntry): RegExp {
  const parsed = /^~(\*?)(.+)$/.exec(entry.key)
  if (!parsed) throw new Error(`unreadable map key "${entry.key}"`)

  return new RegExp(parsed[2] as string, parsed[1] === '*' ? 'i' : '')
}

describe('nginx configuration parsing', () => {
  it('reads every statement of the shared configurations', () => {
    expect(statements(nginxConf).length, 'nginx.conf').toBeGreaterThanOrEqual(20)
    expect(statements(proxyApiConf).length, 'proxy-api.conf').toBeGreaterThanOrEqual(17)
    expect(statements(securityHeadersConf).length, 'security-headers.conf').toBeGreaterThanOrEqual(
      10,
    )
  })
})

describe('nginx forwarded protocol sanitisation', () => {
  const block = mapBlock('$codigoactivo_forwarded_proto')

  it('never echoes the client header and falls back to http', () => {
    expect(block.header).toBe('map $http_x_forwarded_proto $codigoactivo_forwarded_proto')
    expect(block.body).not.toContain('$http_x_forwarded_proto')
    expect(block.body).not.toContain('$scheme')
    expect(mapValue(block, 'default')).toBe('http')
  })

  it('accepts only an exact https value', () => {
    const entry = onlyRegexEntry(block)
    const pattern = mapKeyRegex(entry)

    expect(mapEntries(block)).toHaveLength(2)
    expect(entry.value).toBe('https')
    expect(pattern.test('https')).toBe(true)
    expect(pattern.test('HTTPS')).toBe(true)
    expect(pattern.test('http')).toBe(false)
    expect(pattern.test('https, http')).toBe(false)
    expect(pattern.test('http, https')).toBe(false)
    expect(pattern.test('httpsx')).toBe(false)
    expect(pattern.test(' https')).toBe(false)
    expect(pattern.test('')).toBe(false)
  })

  it('is the value forwarded to the API', () => {
    expect(onlyStatement(proxyApiConf, 'proxy_set_header X-Forwarded-Proto')).toBe(
      'proxy_set_header X-Forwarded-Proto $codigoactivo_forwarded_proto;',
    )
  })
})

describe('nginx strict transport security', () => {
  const block = mapBlock('$codigoactivo_hsts')

  it('emits a two year policy with subdomains for https only', () => {
    expect(block.header).toBe('map $codigoactivo_forwarded_proto $codigoactivo_hsts')
    expect(mapEntries(block)).toHaveLength(2)
    expect(mapValue(block, 'https')).toBe('max-age=63072000; includeSubDomains')
    expect(mapValue(block, 'default')).toBe('')
  })

  it('never requests preloading', () => {
    for (const config of allConfigs) {
      expect(config).not.toContain('preload')
    }
  })

  it('is sent once, from the shared snippet', () => {
    expect(onlyStatement(securityHeadersConf, 'add_header Strict-Transport-Security')).toBe(
      'add_header Strict-Transport-Security $codigoactivo_hsts always;',
    )
    expect(statementsOf(defaultConf, 'add_header Strict-Transport-Security')).toHaveLength(0)
    expect(statementsOf(proxyApiConf, 'add_header Strict-Transport-Security')).toHaveLength(0)
  })
})

describe('nginx content security policy', () => {
  const directive = 'add_header Content-Security-Policy'

  it('is declared exactly once', () => {
    expect(
      allConfigs.flatMap((config) => statementsOf(config, directive)).length,
      'exactly one Content-Security-Policy header must exist',
    ).toBe(1)
  })

  it('pins the complete policy', () => {
    const expectedPolicy = [
      "default-src 'self'",
      "script-src 'self'",
      "style-src 'self' 'unsafe-inline'",
      "font-src 'self'",
      "img-src 'self' data: blob:",
      "connect-src 'self'",
      "frame-src 'none'",
      "worker-src 'none'",
      "media-src 'none'",
      "manifest-src 'none'",
      "object-src 'none'",
      "base-uri 'none'",
      "form-action 'self'",
      "frame-ancestors 'none'",
    ].join('; ')

    expect(onlyStatement(securityHeadersConf, directive)).toBe(
      `add_header Content-Security-Policy "${expectedPolicy}\${codigoactivo_csp_upgrade}" always;`,
    )
  })

  it('upgrades insecure requests only under https', () => {
    const policy = onlyStatement(securityHeadersConf, directive)
    const block = mapBlock('$codigoactivo_csp_upgrade')

    expect(policy).toContain('${codigoactivo_csp_upgrade}"')
    expect(policy).not.toContain('; upgrade-insecure-requests"')
    expect(block.header).toBe('map $codigoactivo_forwarded_proto $codigoactivo_csp_upgrade')
    expect(mapEntries(block)).toHaveLength(2)
    expect(mapValue(block, 'https')).toBe('; upgrade-insecure-requests')
    expect(mapValue(block, 'default')).toBe('')
  })
})

describe('nginx shared security headers', () => {
  it('sends every required header', () => {
    const expected = [
      'add_header X-Content-Type-Options nosniff always;',
      'add_header X-Frame-Options DENY always;',
      'add_header Referrer-Policy same-origin always;',
      'add_header X-Permitted-Cross-Domain-Policies none always;',
      'add_header X-DNS-Prefetch-Control off always;',
      'add_header Cross-Origin-Opener-Policy same-origin always;',
      'add_header Cross-Origin-Resource-Policy same-origin always;',
    ]

    for (const statement of expected) {
      expect(statements(securityHeadersConf)).toContain(statement)
    }

    expect(onlyStatement(securityHeadersConf, 'add_header Permissions-Policy')).toContain(
      'geolocation=()',
    )
  })

  it('marks every header as always', () => {
    const headers = statementsOf(securityHeadersConf, 'add_header')

    expect(headers).toHaveLength(10)
    for (const header of headers) {
      expect(header).toMatch(/ always;$/)
    }
  })

  it('declares no header twice', () => {
    for (const name of securityHeaderNames) {
      expect(statementsOf(securityHeadersConf, `add_header ${name}`)).toHaveLength(1)
      expect(
        statementsOf(proxyApiConf, `add_header ${name}`),
        `proxy-api.conf duplicates ${name}`,
      ).toHaveLength(0)
    }
  })

  it('avoids obsolete headers', () => {
    for (const config of allConfigs) {
      expect(config).not.toContain('Public-Key-Pins')
      expect(config).not.toContain('Expect-CT')
      expect(config).not.toContain('Feature-Policy')
      expect(statementsOf(config, 'add_header Pragma')).toHaveLength(0)

      for (const header of statementsOf(config, 'add_header X-XSS-Protection')) {
        expect(header).toMatch(/^add_header X-XSS-Protection "?0"?( always)?;$/)
      }
    }
  })
})

describe('nginx robots policy for API responses', () => {
  const block = mapBlock('$codigoactivo_api_robots')

  it('reads the upstream content type and defaults to no header', () => {
    expect(block.header).toBe('map $upstream_http_content_type $codigoactivo_api_robots')
    expect(mapValue(block, 'default')).toBe('')
    expect(onlyRegexEntry(block).value).toBe('noindex, nofollow')
  })

  it('matches only JSON payloads', () => {
    const pattern = mapKeyRegex(onlyRegexEntry(block))

    expect(pattern.test('application/json')).toBe(true)
    expect(pattern.test('application/json; charset=utf-8')).toBe(true)
    expect(pattern.test('APPLICATION/JSON')).toBe(true)
    expect(pattern.test('application/problem+json')).toBe(true)
    expect(pattern.test('application/problem+json; charset=utf-8')).toBe(true)
    expect(pattern.test('text/html; charset=utf-8')).toBe(false)
    expect(pattern.test('application/xml')).toBe(false)
    expect(pattern.test('text/plain')).toBe(false)
    expect(pattern.test('image/png')).toBe(false)
    expect(pattern.test('application/octet-stream')).toBe(false)
    expect(pattern.test('text/x-json')).toBe(false)
    expect(pattern.test('application/ld+json')).toBe(false)
    expect(pattern.test('application/jsonp')).toBe(false)
  })

  it('is sent only from the API proxy snippet', () => {
    expect(onlyStatement(proxyApiConf, 'add_header X-Robots-Tag')).toBe(
      'add_header X-Robots-Tag $codigoactivo_api_robots always;',
    )
    expect(statementsOf(defaultConf, 'add_header X-Robots-Tag')).toHaveLength(0)
    expect(statementsOf(securityHeadersConf, 'add_header X-Robots-Tag')).toHaveLength(0)
  })
})

describe('nginx logging', () => {
  function logStatements(config: string): string[] {
    return [...statementsOf(config, 'access_log'), ...statementsOf(config, 'error_log')]
  }

  it('turns the access log off for every request', () => {
    expect(statementsOf(nginxConf, 'access_log')).toEqual(['access_log off;'])
  })

  it('keeps only critical errors and hands them to the container', () => {
    expect(onlyStatement(nginxConf, 'error_log')).toBe('error_log /dev/stderr crit;')
  })

  it('never stores a log in a file', () => {
    for (const config of allConfigs) {
      for (const statement of logStatements(config)) {
        expect(statement).toMatch(/^(access_log off|error_log \/dev\/std(out|err) [a-z]+);$/)
      }

      expect(config).not.toContain('/var/log')
      expect(statementsOf(config, 'open_log_file_cache')).toHaveLength(0)
    }
  })

  it('declares no log format to write', () => {
    for (const config of allConfigs) {
      expect(statementsOf(config, 'log_format')).toHaveLength(0)
    }
  })
})

describe('nginx server hardening', () => {
  const blocks = extractBlocks(defaultConf, 'location')

  it('hides the server version', () => {
    expect(statementsOf(defaultConf, 'server_tokens')).toEqual(['server_tokens off;'])
  })

  it('rejects source maps in the asset and application locations', () => {
    const assets = findBlock(blocks, (block) => block.header.includes('^~ /assets/'))
    const application = findBlock(blocks, (block) => /location\s+\/$/.test(block.header))

    for (const block of [assets, application]) {
      const rejections = directChildren(block).filter((child) =>
        /^location\s+~\*\s+\\\.map\$$/.test(child.header),
      )

      expect(rejections, `${block.header} serves source maps`).toHaveLength(1)
      expect(statements((rejections[0] as NginxBlock).body)).toEqual(['return 404;'])
    }
  })

  it('keeps the cache control rules', () => {
    const servers = extractBlocks(defaultConf, 'server')
    const expected: [NginxBlock, string][] = [
      [
        findBlock(servers, (block) => block.header === 'server'),
        'add_header Cache-Control "no-store" always;',
      ],
      [
        findBlock(blocks, (block) => block.header === 'location @rate_limited'),
        'add_header Cache-Control "no-store" always;',
      ],
      [
        findBlock(blocks, (block) => block.header.includes('^~ /assets/')),
        'add_header Cache-Control $codigoactivo_immutable_cache always;',
      ],
      [
        findBlock(blocks, (block) => block.header.includes('favicon\\.ico')),
        'add_header Cache-Control $codigoactivo_daily_cache always;',
      ],
      [
        findBlock(blocks, (block) => /location\s+\/$/.test(block.header)),
        'add_header Cache-Control "no-cache" always;',
      ],
    ]

    for (const [block, statement] of expected) {
      expect(statementsOf(block.ownStatements, 'add_header Cache-Control'), block.header).toEqual([
        statement,
      ])
    }

    expect(onlyStatement(proxyApiConf, 'add_header Cache-Control')).toBe(
      'add_header Cache-Control $codigoactivo_api_cache always;',
    )
  })
})

describe('nginx add_header inheritance', () => {
  const blocks = [
    ...extractBlocks(defaultConf, 'server'),
    ...extractBlocks(defaultConf, 'location'),
  ]

  it('parses every block of the site configuration', () => {
    expect(blocks.length).toBeGreaterThanOrEqual(15)
  })

  it('sends the shared snippet from every server block', () => {
    const servers = extractBlocks(defaultConf, 'server')

    expect(servers.length).toBeGreaterThanOrEqual(1)
    for (const server of servers) {
      expect(
        statementsOf(server.ownStatements, 'include').filter((include) =>
          /\/etc\/nginx\/snippets\/security-headers\.conf;$/.test(include),
        ),
        `${server.header} does not include the security snippet`,
      ).toHaveLength(1)
    }
  })

  it('sends the shared snippet from the API proxy snippet', () => {
    expect(
      statementsOf(proxyApiConf, 'include').filter(
        (include) => include === 'include /etc/nginx/snippets/security-headers.conf;',
      ),
      'proxy-api.conf must include the security snippet exactly once',
    ).toHaveLength(1)
  })

  it('re-declares the security headers wherever a block sets its own header', () => {
    for (const block of blocks) {
      if (statementsOf(block.ownStatements, 'add_header').length === 0) continue

      const includes = statementsOf(block.ownStatements, 'include')
      expect(
        includes.some((include) =>
          /\/etc\/nginx\/snippets\/(security-headers|proxy-api)\.conf;$/.test(include),
        ),
        `${block.header} sets add_header without including the security snippet`,
      ).toBe(true)
    }
  })

  it('never duplicates a snippet header next to the include', () => {
    for (const block of blocks) {
      const includesSnippet = statementsOf(block.ownStatements, 'include').some((include) =>
        include.includes('/etc/nginx/snippets/'),
      )
      if (!includesSnippet) continue

      for (const name of securityHeaderNames) {
        expect(
          statementsOf(block.ownStatements, `add_header ${name}`),
          `${block.header} duplicates ${name}`,
        ).toHaveLength(0)
      }
    }
  })

  it('never overrides a proxied header next to the API proxy include', () => {
    for (const block of blocks) {
      if (includesOf(block, proxyApiInclude).length === 0) continue

      for (const name of [...securityHeaderNames, 'Cache-Control', 'X-Robots-Tag']) {
        expect(
          statementsOf(block.ownStatements, `add_header ${name}`),
          `${block.header} overrides ${name} set by the API proxy snippet`,
        ).toHaveLength(0)
      }
    }
  })

  it('never sets a header inside a conditional block', () => {
    for (const config of allConfigs) {
      for (const block of extractBlocks(config, 'if')) {
        expect(
          statementsOf(block.body, 'add_header'),
          `${block.header} sets add_header inside a conditional block`,
        ).toHaveLength(0)
      }
    }
  })

  it('declares no header at the http level', () => {
    expect(statementsOf(nginxConf, 'add_header')).toHaveLength(0)
  })
})

describe('nginx API proxying', () => {
  it('reaches the upstream only through the shared snippet', () => {
    expect(
      statementsOf(proxyApiConf, 'proxy_pass'),
      'proxy-api.conf must proxy exactly once',
    ).toHaveLength(1)

    for (const [name, config] of [
      ['default.conf', defaultConf],
      ['security-headers.conf', securityHeadersConf],
      ['nginx.conf', nginxConf],
    ] as const) {
      expect(
        statementsOf(config, 'proxy_pass'),
        `${name} proxies outside the snippet`,
      ).toHaveLength(0)
      expect(
        statementsOf(config, 'proxy_set_header'),
        `${name} forwards a request header outside the snippet`,
      ).toHaveLength(0)
    }
  })

  it('includes the proxy snippet in every proxied location', () => {
    const blocks = extractBlocks(defaultConf, 'location')

    for (const header of proxiedLocationHeaders) {
      const block = findBlock(blocks, (candidate) => candidate.header === header)

      expect(
        includesOf(block, proxyApiInclude),
        `${header} does not proxy through the shared snippet`,
      ).toHaveLength(1)
    }
  })
})
