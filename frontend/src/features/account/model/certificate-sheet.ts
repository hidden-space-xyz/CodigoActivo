import type { AccountCertificate } from '@/entities/account'
import { logoMarkLarge } from '@/shared/branding'
import { i18n } from '@/shared/i18n'
import { downloadBlob, fullName, parseDateOnly } from '@/shared/lib'

export const SHEET_WIDTH_MM = 297
export const SHEET_HEIGHT_MM = 210
export const SHEET_RATIO = SHEET_WIDTH_MM / SHEET_HEIGHT_MM

const EXPORT_PX_PER_MM = 11.811
const PREVIEW_PX_PER_MM = 3.6
const MAX_PREVIEW_DPR = 2

const TAU = Math.PI * 2
const PT_TO_MM = 0.352778
const AXIS = SHEET_WIDTH_MM / 2

const PAPER = '#fdfbf7'
const INK_NAME = '#171717'
const INK_TITLE = '#262626'
const INK_BODY = '#616161'
const INK_META = '#737373'
const ENGRAVE = '#8f5900'
const ORANGE = '#f9a320'
const LIME = '#7cb518'
const AZURE = '#159fde'

const STRANDS = [ORANGE, LIME, AZURE]

const DISPLAY_FAMILY = "'Space Grotesk', system-ui, sans-serif"
const BODY_FAMILY = "'Hanken Grotesk', system-ui, sans-serif"
const MONO_FAMILY = "'JetBrains Mono', ui-monospace, monospace"

const BAND_INSET = 9.2
const BAND_HALF = 3
const BAND_AXIS = BAND_INSET + BAND_HALF

const KEYLINE_OUTER = 17.5
const MICROTEXT_INSET = 19.7
const KEYLINE_INNER = 21.8

const SEAL_X = AXIS
const SEAL_Y = 169.5
const SEAL_R = 14.5

const longDateFormatter = new Intl.DateTimeFormat('es-ES', {
  day: 'numeric',
  month: 'long',
  year: 'numeric',
})

function engraveInk(alpha: number): string {
  return `rgba(143, 89, 0, ${alpha})`
}

function font(weight: number, sizePt: number, family: string): string {
  return `${weight} ${(sizePt * PT_TO_MM).toFixed(4)}px ${family}`
}

function tracking(sizePt: number, em: number): number {
  return sizePt * PT_TO_MM * em
}

function t(key: string): string {
  return i18n.global.t(`features.account.certificates.sheet.${key}`)
}

let logoPromise: Promise<HTMLImageElement | null> | null = null

function loadLogo(): Promise<HTMLImageElement | null> {
  logoPromise ??= new Promise<HTMLImageElement | null>((resolve) => {
    const image = new Image()
    image.addEventListener('load', () => resolve(image))
    image.addEventListener('error', () => {
      logoPromise = null
      resolve(null)
    })
    image.src = logoMarkLarge
  })
  return logoPromise
}

let fontsPromise: Promise<void> | null = null

async function loadFontsOnce(): Promise<void> {
  const specimens = [
    `700 12px ${DISPLAY_FAMILY}`,
    `600 12px ${DISPLAY_FAMILY}`,
    `400 12px ${BODY_FAMILY}`,
    `600 12px ${BODY_FAMILY}`,
    `400 12px ${MONO_FAMILY}`,
    `500 12px ${MONO_FAMILY}`,
    `600 12px ${MONO_FAMILY}`,
  ]
  await Promise.all(specimens.map((specimen) => document.fonts.load(specimen)))
  await document.fonts.ready
}

function loadFonts(): Promise<void> {
  fontsPromise ??= loadFontsOnce().catch((error: unknown) => {
    fontsPromise = null
    throw error
  })
  return fontsPromise
}

function measureTracked(ctx: CanvasRenderingContext2D, text: string, gap: number): number {
  const glyphs = [...text]
  if (glyphs.length === 0) return 0
  let total = -gap
  for (const glyph of glyphs) {
    total += ctx.measureText(glyph).width + gap
  }
  return total
}

function fillTracked(
  ctx: CanvasRenderingContext2D,
  text: string,
  x: number,
  y: number,
  gap: number,
  align: 'left' | 'center' | 'right' = 'center',
): void {
  const glyphs = [...text]
  if (glyphs.length === 0) return

  const widths = glyphs.map((glyph) => ctx.measureText(glyph).width)
  const total = widths.reduce((sum, width) => sum + width + gap, -gap)
  const previousAlign = ctx.textAlign
  ctx.textAlign = 'left'
  let cursor = x
  if (align === 'center') cursor -= total / 2
  if (align === 'right') cursor -= total
  glyphs.forEach((glyph, index) => {
    ctx.fillText(glyph, cursor, y)
    cursor += (widths[index] ?? 0) + gap
  })
  ctx.textAlign = previousAlign
}

function ellipsize(ctx: CanvasRenderingContext2D, text: string, maxWidth: number): string {
  if (ctx.measureText(text).width <= maxWidth) return text
  const glyphs = [...text]
  while (glyphs.length > 1 && ctx.measureText(`${glyphs.join('')}…`).width > maxWidth) {
    glyphs.pop()
  }
  return `${glyphs.join('').trimEnd()}…`
}

function fitSize(
  ctx: CanvasRenderingContext2D,
  text: string,
  maxWidth: number,
  weight: number,
  family: string,
  basePt: number,
  minPt: number,
): number {
  let size = basePt
  ctx.font = font(weight, size, family)
  while (size > minPt && ctx.measureText(text).width > maxWidth) {
    size = Math.max(minPt, size - 0.25)
    ctx.font = font(weight, size, family)
  }
  return size
}

function wrapLines(
  ctx: CanvasRenderingContext2D,
  text: string,
  maxWidth: number,
  maxLines: number,
): string[] {
  const words = text.split(/\s+/u).filter(Boolean)
  if (words.length === 0) return []

  const lines: string[] = []
  let current = ''
  let index = 0
  while (index < words.length) {
    const word = words[index] ?? ''
    const candidate = current ? `${current} ${word}` : word
    if (!current || ctx.measureText(candidate).width <= maxWidth) {
      current = candidate
      index += 1
      continue
    }
    if (lines.length === maxLines - 1) break
    lines.push(current)
    current = word
    index += 1
  }
  if (current) lines.push(current)

  const rest = words.slice(index).join(' ')
  const last = lines.length - 1
  if (rest && last >= 0) lines[last] = `${lines[last] ?? ''} ${rest}`

  return lines.map((line) => ellipsize(ctx, line, maxWidth))
}

const CHAIN: readonly (readonly [string, string])[] = [
  ['</>', ORANGE],
  ['{}', LIME],
  ['[]', AZURE],
  ['()', ORANGE],
  [';', LIME],
  ['->', AZURE],
]

const CHAIN_PITCH = 9.4

function tokenChain(ctx: CanvasRenderingContext2D, length: number, angle: number): void {
  ctx.save()
  ctx.lineWidth = 0.14
  ctx.strokeStyle = engraveInk(0.3)
  ctx.beginPath()
  ctx.moveTo(0, 0)
  ctx.lineTo(length, 0)
  ctx.stroke()

  ctx.font = font(600, 9, MONO_FAMILY)
  ctx.textAlign = 'center'
  ctx.textBaseline = 'middle'

  const count = Math.max(1, Math.round(length / CHAIN_PITCH))
  const pitch = length / count
  for (let i = 0; i < count; i++) {
    const token = CHAIN[i % CHAIN.length]
    if (!token) continue
    ctx.save()
    ctx.translate(pitch * (i + 0.5), 0)
    ctx.rotate(-angle)
    const width = ctx.measureText(token[0]).width
    ctx.fillStyle = PAPER
    ctx.fillRect(-width / 2 - 0.8, -2, width + 1.6, 4)
    ctx.fillStyle = token[1]
    ctx.fillText(token[0], 0, 0)
    ctx.restore()
  }
  ctx.restore()
}

function drawFrame(ctx: CanvasRenderingContext2D): void {
  const spanH = SHEET_WIDTH_MM - BAND_INSET * 2
  const spanV = SHEET_HEIGHT_MM - BAND_INSET * 2

  const bands: [number, number, number, number][] = [
    [BAND_INSET, BAND_AXIS, 0, spanH],
    [BAND_INSET, SHEET_HEIGHT_MM - BAND_AXIS, 0, spanH],
    [BAND_AXIS, BAND_INSET, Math.PI / 2, spanV],
    [SHEET_WIDTH_MM - BAND_AXIS, BAND_INSET, Math.PI / 2, spanV],
  ]
  for (const [x, y, angle, length] of bands) {
    ctx.save()
    ctx.translate(x, y)
    ctx.rotate(angle)
    tokenChain(ctx, length, angle)
    ctx.restore()
  }

  ctx.lineWidth = 0.7
  ctx.strokeStyle = ORANGE
  ctx.strokeRect(7, 7, SHEET_WIDTH_MM - 14, SHEET_HEIGHT_MM - 14)

  ctx.lineWidth = 0.28
  ctx.strokeStyle = engraveInk(0.45)
  inset(ctx, KEYLINE_OUTER)

  ctx.lineWidth = 0.12
  ctx.strokeStyle = engraveInk(0.3)
  inset(ctx, KEYLINE_INNER)
}

function inset(ctx: CanvasRenderingContext2D, value: number): void {
  ctx.strokeRect(value, value, SHEET_WIDTH_MM - value * 2, SHEET_HEIGHT_MM - value * 2)
}

function microLine(ctx: CanvasRenderingContext2D, unit: string, length: number): void {
  const unitWidth = ctx.measureText(unit).width
  if (unitWidth <= 0) return
  ctx.save()
  ctx.beginPath()
  ctx.rect(0, -1.4, length, 2.8)
  ctx.clip()
  ctx.textAlign = 'left'
  ctx.textBaseline = 'middle'
  ctx.fillText(unit.repeat(Math.ceil(length / unitWidth) + 1), 0, 0)
  ctx.restore()
}

function drawMicrotext(ctx: CanvasRenderingContext2D): void {
  const span = MICROTEXT_INSET
  const width = SHEET_WIDTH_MM - span * 2
  const height = SHEET_HEIGHT_MM - span * 2
  const unit = `${t('microtext').toLocaleUpperCase('es-ES')}  ·  `

  ctx.save()
  ctx.font = font(500, 3.1, MONO_FAMILY)
  ctx.fillStyle = engraveInk(0.33)

  const sides: [number, number, number, number][] = [
    [span, span, 0, width],
    [span, SHEET_HEIGHT_MM - span, 0, width],
    [SHEET_WIDTH_MM - span, span, Math.PI / 2, height],
    [span, SHEET_HEIGHT_MM - span, -Math.PI / 2, height],
  ]
  for (const [x, y, angle, length] of sides) {
    ctx.save()
    ctx.translate(x, y)
    ctx.rotate(angle)
    microLine(ctx, unit, length)
    ctx.restore()
  }
  ctx.restore()
}

function rosettePath(
  ctx: CanvasRenderingContext2D,
  cx: number,
  cy: number,
  a: number,
  b: number,
  petals: number,
  phase: number,
): void {
  const steps = 720
  ctx.beginPath()
  for (let i = 0; i <= steps; i++) {
    const angle = (i / steps) * TAU
    const x = cx + a * Math.cos(angle + phase) + b * Math.cos(petals * angle + phase)
    const y = cy + a * Math.sin(angle + phase) - b * Math.sin(petals * angle + phase)
    if (i === 0) ctx.moveTo(x, y)
    else ctx.lineTo(x, y)
  }
  ctx.closePath()
}

function latheBand(
  ctx: CanvasRenderingContext2D,
  cx: number,
  cy: number,
  a: number,
  b: number,
  petals: number,
  color: string,
  copies: number,
): void {
  const drift = TAU / (petals + 1) / copies
  ctx.strokeStyle = color
  for (let i = 0; i < copies; i++) {
    rosettePath(ctx, cx, cy, a, b, petals, i * drift)
    ctx.stroke()
  }
}

const SOURCE = [
  '# codigoactivo.es',
  '# learning to code is learning to create',
  '',
  'class Certificate:',
  '    def __init__(self, member, event):',
  '        self.member = member',
  '        self.event = event',
  '        self.issued_on = event.ends_on',
  '        self.seal = "codigo-activo"',
  '',
  '    def credential(self):',
  '        return self.member.full_name',
  '',
  '',
  'def issue(event):',
  '    awarded = []',
  '    for member in event.attendees:',
  '        if member.attendance is not CONFIRMED:',
  '            continue',
  '        awarded.append(Certificate(member, event))',
  '        print("Congratulations,", member.first_name)',
  '    return awarded',
  '',
  '',
  'if event.has_finished():',
  '    for certificate in issue(event):',
  '        certificate.credential()',
]

const KEYWORDS = new Set([
  'class',
  'def',
  'for',
  'in',
  'if',
  'is',
  'not',
  'return',
  'continue',
  'self',
  'None',
  'True',
  'False',
])

interface CodeRun {
  readonly text: string
  readonly color: string
}

function classifyCode(line: string): CodeRun[] {
  if (line.trimStart().startsWith('#')) {
    return [{ text: line, color: engraveInk(0.55) }]
  }

  const runs: CodeRun[] = []
  const pattern = /"[^"]*"|[A-Za-z_][A-Za-z0-9_]*|\s+|[^\sA-Za-z0-9_"]+|"/gu
  for (const [token] of line.matchAll(pattern)) {
    let color = INK_TITLE
    if (token.startsWith('"')) color = LIME
    else if (KEYWORDS.has(token)) color = ORANGE
    else if (/^[A-Z_]+$/u.test(token) && token.length > 1) color = AZURE
    else if (/^[^\sA-Za-z0-9_]+$/u.test(token)) color = engraveInk(0.75)
    runs.push({ text: token, color })
  }
  return runs
}

const CLASSIFIED_SOURCE = SOURCE.map(classifyCode)

function drawCodeField(ctx: CanvasRenderingContext2D): void {
  const lines = CLASSIFIED_SOURCE
  const sizePt = 11
  const lineHeight = 4.2
  const top = 31

  ctx.save()
  ctx.font = font(500, sizePt, MONO_FAMILY)
  ctx.textAlign = 'left'
  ctx.textBaseline = 'alphabetic'
  ctx.globalAlpha = 0.055

  const charWidth = ctx.measureText('M').width
  const left = AXIS - charWidth * 27
  const gutter = left - charWidth * 2.4

  lines.forEach((runs, index) => {
    const y = top + index * lineHeight

    ctx.textAlign = 'right'
    ctx.fillStyle = engraveInk(0.6)
    ctx.fillText(String(index + 1).padStart(2, ' '), gutter, y)

    ctx.textAlign = 'left'
    let cursor = left
    for (const run of runs) {
      ctx.fillStyle = run.color
      ctx.fillText(run.text, cursor, y)
      cursor += run.text.length * charWidth
    }
  })

  ctx.lineWidth = 0.12
  ctx.strokeStyle = engraveInk(0.6)
  ctx.beginPath()
  ctx.moveTo(gutter + charWidth * 0.9, top - lineHeight)
  ctx.lineTo(gutter + charWidth * 0.9, top + lines.length * lineHeight - lineHeight)
  ctx.stroke()
  ctx.restore()

  const burn = ctx.createRadialGradient(AXIS, 100, 6, AXIS, 100, 118)
  burn.addColorStop(0, 'rgba(253, 251, 247, 0.92)')
  burn.addColorStop(0.5, 'rgba(253, 251, 247, 0.66)')
  burn.addColorStop(1, 'rgba(253, 251, 247, 0.12)')
  ctx.fillStyle = burn
  ctx.fillRect(0, 0, SHEET_WIDTH_MM, SHEET_HEIGHT_MM)
}

function drawMedallion(ctx: CanvasRenderingContext2D, cx: number, cy: number, glyph: string): void {
  ctx.save()

  ctx.fillStyle = PAPER
  ctx.beginPath()
  ctx.arc(cx, cy, 4.6, 0, TAU)
  ctx.fill()

  ctx.lineWidth = 0.35
  ctx.strokeStyle = ENGRAVE
  ctx.beginPath()
  ctx.arc(cx, cy, 4.3, 0, TAU)
  ctx.stroke()

  ctx.save()
  ctx.setLineDash([0.32, 1.643])
  ctx.lineWidth = 0.7
  ctx.strokeStyle = engraveInk(0.4)
  ctx.beginPath()
  ctx.arc(cx, cy, 3.75, 0, TAU)
  ctx.stroke()
  ctx.restore()

  ctx.save()
  ctx.globalAlpha = 0.22
  ctx.lineWidth = 0.06
  latheBand(ctx, cx, cy, 3.25, 0.6, 12, ORANGE, 6)
  ctx.restore()

  ctx.lineWidth = 0.18
  ctx.strokeStyle = 'rgba(21, 159, 222, 0.6)'
  ctx.beginPath()
  ctx.arc(cx, cy, 2.7, 0, TAU)
  ctx.stroke()

  ctx.font = font(600, 9.6, MONO_FAMILY)
  ctx.fillStyle = ENGRAVE
  ctx.textAlign = 'center'
  ctx.textBaseline = 'middle'
  ctx.fillText(glyph, cx, cy)

  ctx.restore()
}

function drawCorners(ctx: CanvasRenderingContext2D): void {
  const right = SHEET_WIDTH_MM - BAND_AXIS
  const bottom = SHEET_HEIGHT_MM - BAND_AXIS
  drawMedallion(ctx, BAND_AXIS, BAND_AXIS, '{')
  drawMedallion(ctx, right, BAND_AXIS, '}')
  drawMedallion(ctx, BAND_AXIS, bottom, '[')
  drawMedallion(ctx, right, bottom, ']')
}

function drawCrest(ctx: CanvasRenderingContext2D, logo: HTMLImageElement | null): void {
  const halo = ctx.createRadialGradient(AXIS, 37, 0, AXIS, 37, 16)
  halo.addColorStop(0, 'rgba(249, 163, 32, 0.12)')
  halo.addColorStop(0.68, 'rgba(249, 163, 32, 0)')
  halo.addColorStop(1, 'rgba(249, 163, 32, 0)')
  ctx.fillStyle = halo
  ctx.beginPath()
  ctx.arc(AXIS, 37, 16, 0, TAU)
  ctx.fill()

  if (logo) ctx.drawImage(logo, AXIS - 13, 24, 26, 26)
}

function drawEyebrow(ctx: CanvasRenderingContext2D): void {
  const text = `// ${t('eyebrow')}`.toLocaleUpperCase('es-ES')
  const gap = tracking(9.5, 0.14)

  ctx.font = font(600, 9.5, MONO_FAMILY)
  ctx.fillStyle = ENGRAVE
  ctx.textAlign = 'center'
  ctx.textBaseline = 'alphabetic'
  fillTracked(ctx, text, AXIS, 60, gap)

  const half = measureTracked(ctx, text, gap) / 2
  ctx.lineWidth = 0.25
  ctx.strokeStyle = engraveInk(0.4)
  ctx.beginPath()
  ctx.moveTo(AXIS - half - 6, 58.8)
  ctx.lineTo(AXIS - half - 28, 58.8)
  ctx.moveTo(AXIS + half + 6, 58.8)
  ctx.lineTo(AXIS + half + 28, 58.8)
  ctx.stroke()
}

function drawComment(ctx: CanvasRenderingContext2D, text: string, y: number): void {
  ctx.font = font(400, 12, BODY_FAMILY)
  const hashWidth = ctx.measureText('# ').width
  const total = hashWidth + ctx.measureText(text).width

  ctx.textAlign = 'left'
  ctx.fillStyle = ORANGE
  ctx.fillText('#', AXIS - total / 2, y)
  ctx.fillStyle = INK_BODY
  ctx.fillText(text, AXIS - total / 2 + hashWidth, y)
  ctx.textAlign = 'center'
}

function drawLiteral(
  ctx: CanvasRenderingContext2D,
  text: string,
  y: number,
  sizePt: number,
  weight: number,
  open = true,
  close = true,
): void {
  ctx.font = font(500, sizePt, MONO_FAMILY)
  const quote = ctx.measureText('"').width
  ctx.font = font(weight, sizePt, DISPLAY_FAMILY)
  const body = ctx.measureText(text).width
  const lead = open ? quote : 0
  const start = AXIS - (body + lead + (close ? quote : 0)) / 2

  ctx.textAlign = 'left'
  ctx.fillStyle = INK_NAME
  ctx.fillText(text, start + lead, y)

  ctx.font = font(500, sizePt, MONO_FAMILY)
  ctx.fillStyle = LIME
  if (open) ctx.fillText('"', start, y)
  if (close) ctx.fillText('"', start + lead + body, y)
  ctx.textAlign = 'center'
}

function drawNameRule(ctx: CanvasRenderingContext2D, nameWidth: number): void {
  const width = Math.min(215, Math.max(110, nameWidth + 30))
  const y = 101

  ctx.save()
  ctx.font = font(600, 10, MONO_FAMILY)
  ctx.textAlign = 'center'
  ctx.textBaseline = 'middle'
  const mark = ctx.measureText('</>').width

  ctx.lineWidth = 0.35
  ctx.strokeStyle = engraveInk(0.42)
  ctx.beginPath()
  ctx.moveTo(AXIS - width / 2, y)
  ctx.lineTo(AXIS - mark / 2 - 3.5, y)
  ctx.moveTo(AXIS + mark / 2 + 3.5, y)
  ctx.lineTo(AXIS + width / 2, y)
  ctx.stroke()

  ctx.lineWidth = 0.14
  ctx.strokeStyle = engraveInk(0.28)
  ctx.beginPath()
  ctx.moveTo(AXIS - width / 2, y + 1.4)
  ctx.lineTo(AXIS - mark / 2 - 3.5, y + 1.4)
  ctx.moveTo(AXIS + mark / 2 + 3.5, y + 1.4)
  ctx.lineTo(AXIS + width / 2, y + 1.4)
  ctx.stroke()

  ctx.fillStyle = ORANGE
  ctx.fillText('</>', AXIS, y)
  ctx.restore()
}

function arcText(
  ctx: CanvasRenderingContext2D,
  text: string,
  radius: number,
  centreAngle: number,
  gap: number,
  clockwise: boolean,
): void {
  const glyphs = [...text]
  if (glyphs.length === 0) return

  const widths = glyphs.map((glyph) => ctx.measureText(glyph).width)
  const span = widths.reduce((total, width) => total + width + gap, -gap) / radius
  const direction = clockwise ? 1 : -1
  let angle = centreAngle - (direction * span) / 2

  ctx.textAlign = 'center'
  ctx.textBaseline = 'middle'
  glyphs.forEach((glyph, index) => {
    const width = widths[index] ?? 0
    const mid = angle + (direction * (width / radius)) / 2
    ctx.save()
    ctx.translate(SEAL_X + radius * Math.cos(mid), SEAL_Y + radius * Math.sin(mid))
    ctx.rotate(mid + (clockwise ? Math.PI / 2 : -Math.PI / 2))
    ctx.fillText(glyph, 0, 0)
    ctx.restore()
    angle += (direction * (width + gap)) / radius
  })
}

function drawSeal(ctx: CanvasRenderingContext2D): void {
  ctx.save()

  const relief = ctx.createRadialGradient(
    SEAL_X - SEAL_R * 0.28,
    SEAL_Y - SEAL_R * 0.4,
    SEAL_R * 0.04,
    SEAL_X,
    SEAL_Y,
    SEAL_R,
  )
  relief.addColorStop(0, '#ffffff')
  relief.addColorStop(0.55, 'rgba(249, 163, 32, 0.05)')
  relief.addColorStop(1, 'rgba(249, 163, 32, 0.18)')
  ctx.fillStyle = relief
  ctx.beginPath()
  ctx.arc(SEAL_X, SEAL_Y, SEAL_R, 0, TAU)
  ctx.fill()

  ctx.lineWidth = 0.5
  ctx.strokeStyle = 'rgba(255, 255, 255, 0.9)'
  ctx.beginPath()
  ctx.arc(SEAL_X, SEAL_Y, SEAL_R - 0.3, 0, TAU)
  ctx.stroke()

  ctx.lineWidth = 0.4
  ctx.strokeStyle = ENGRAVE
  ctx.beginPath()
  ctx.arc(SEAL_X, SEAL_Y, 13.9, 0, TAU)
  ctx.stroke()

  ctx.save()
  ctx.setLineDash([0.26, 0.4])
  ctx.lineWidth = 0.75
  ctx.strokeStyle = engraveInk(0.4)
  ctx.beginPath()
  ctx.arc(SEAL_X, SEAL_Y, 13.3, 0, TAU)
  ctx.stroke()
  ctx.restore()

  ctx.lineWidth = 0.15
  ctx.strokeStyle = engraveInk(0.5)
  for (const radius of [12.6, 9.4]) {
    ctx.beginPath()
    ctx.arc(SEAL_X, SEAL_Y, radius, 0, TAU)
    ctx.stroke()
  }

  ctx.lineWidth = 0.3
  ctx.strokeStyle = ENGRAVE
  ctx.beginPath()
  ctx.arc(SEAL_X, SEAL_Y, 8.9, 0, TAU)
  ctx.stroke()

  ctx.save()
  ctx.lineWidth = 0.14
  ctx.strokeStyle = engraveInk(0.55)
  for (let i = 0; i < 72; i++) {
    const angle = (i / 72) * TAU
    const outer = i % 6 === 0 ? 8.5 : 8.1
    ctx.beginPath()
    ctx.moveTo(SEAL_X + Math.cos(angle) * 7.2, SEAL_Y + Math.sin(angle) * 7.2)
    ctx.lineTo(SEAL_X + Math.cos(angle) * outer, SEAL_Y + Math.sin(angle) * outer)
    ctx.stroke()
  }
  ctx.globalAlpha = 0.35
  ctx.lineWidth = 0.08
  latheBand(ctx, SEAL_X, SEAL_Y, 4.6, 1.7, 11, ORANGE, 10)
  ctx.restore()

  ctx.fillStyle = ENGRAVE
  ctx.font = font(500, 5.6, MONO_FAMILY)
  arcText(ctx, t('sealTop').toLocaleUpperCase('es-ES'), 11.1, -Math.PI / 2, 0.32, true)
  arcText(ctx, t('sealBottom').toLocaleUpperCase('es-ES'), 11.1, Math.PI / 2, 0.32, false)

  ctx.font = font(600, 16, MONO_FAMILY)
  ctx.textAlign = 'center'
  ctx.textBaseline = 'middle'
  ctx.fillText('</>', SEAL_X, SEAL_Y - 1.4)

  STRANDS.forEach((color, index) => {
    ctx.fillStyle = color
    ctx.beginPath()
    ctx.arc(SEAL_X + (index - 1) * 2.8, SEAL_Y + 5, 0.45, 0, TAU)
    ctx.fill()
  })

  ctx.restore()
}

function drawFooter(ctx: CanvasRenderingContext2D, certificate: AccountCertificate): void {
  ctx.lineWidth = 0.25
  ctx.strokeStyle = engraveInk(0.35)
  ctx.beginPath()
  ctx.moveTo(30, 157)
  ctx.lineTo(130, 157)
  ctx.moveTo(167, 157)
  ctx.lineTo(267, 157)
  ctx.stroke()

  ctx.textBaseline = 'alphabetic'
  const labelGap = tracking(6.5, 0.1)

  ctx.font = font(500, 6.5, MONO_FAMILY)
  ctx.fillStyle = INK_META
  fillTracked(ctx, t('issuerLabel').toLocaleUpperCase('es-ES'), 30, 166, labelGap, 'left')
  fillTracked(ctx, t('registryLabel').toLocaleUpperCase('es-ES'), 267, 166, labelGap, 'right')

  ctx.font = font(500, 9, MONO_FAMILY)
  ctx.textAlign = 'left'
  const prompt = '$ '
  ctx.fillStyle = LIME
  ctx.fillText(prompt, 30, 173.5)
  ctx.fillStyle = INK_TITLE
  ctx.fillText(t('issuerValue'), 30 + ctx.measureText(prompt).width, 173.5)

  ctx.font = font(400, 9, MONO_FAMILY)
  const quote = ctx.measureText('"').width
  const codeWidth = ctx.measureText(certificate.code).width
  const refWidth = ctx.measureText('ref = ').width
  const start = 267 - (refWidth + quote * 2 + codeWidth)

  ctx.fillStyle = AZURE
  ctx.fillText('ref', start, 173.5)
  ctx.fillStyle = engraveInk(0.75)
  ctx.fillText(' = ', start + ctx.measureText('ref').width, 173.5)
  ctx.fillStyle = LIME
  ctx.fillText('"', start + refWidth, 173.5)
  ctx.fillText('"', start + refWidth + quote + codeWidth, 173.5)
  ctx.fillStyle = INK_TITLE
  ctx.fillText(certificate.code, start + refWidth + quote, 173.5)
  ctx.textAlign = 'center'
}

function ceremonialDate(certificate: AccountCertificate): string {
  const start = parseDateOnly(certificate.startsAt)
  const end = parseDateOnly(certificate.endsAt)
  if (!start) return ''
  if (!end || end <= start) return longDateFormatter.format(start)
  return longDateFormatter.formatRange(start, end)
}

function drawBody(ctx: CanvasRenderingContext2D, certificate: AccountCertificate): void {
  ctx.textAlign = 'center'
  ctx.textBaseline = 'alphabetic'

  drawComment(ctx, t('preamble'), 75)

  const name = fullName(certificate)
  const namePt = fitSize(ctx, name, 188, 700, DISPLAY_FAMILY, 32, 16)
  ctx.font = font(700, namePt, DISPLAY_FAMILY)
  const shownName = ellipsize(ctx, name, 188)
  const nameWidth = ctx.measureText(shownName).width
  drawLiteral(ctx, shownName, 94, namePt, 700)

  drawNameRule(ctx, nameWidth)

  drawComment(ctx, t('connector'), 116)

  const titlePt = fitSize(ctx, certificate.eventTitle, 400, 600, DISPLAY_FAMILY, 19, 13)
  const titleLines = wrapLines(ctx, certificate.eventTitle, 200, 2)
  titleLines.forEach((line, index) => {
    drawLiteral(
      ctx,
      line,
      129 + index * titlePt * PT_TO_MM * 1.22,
      titlePt,
      600,
      index === 0,
      index === titleLines.length - 1,
    )
  })

  const dateBaseline = titleLines.length > 1 ? 149 : 144
  ctx.font = font(500, 9, MONO_FAMILY)
  ctx.fillStyle = INK_BODY
  fillTracked(
    ctx,
    ceremonialDate(certificate).toLocaleUpperCase('es-ES'),
    AXIS,
    dateBaseline,
    tracking(9, 0.1),
  )
}

function paint(
  ctx: CanvasRenderingContext2D,
  certificate: AccountCertificate,
  logo: HTMLImageElement | null,
): void {
  ctx.fillStyle = PAPER
  ctx.fillRect(0, 0, SHEET_WIDTH_MM, SHEET_HEIGHT_MM)

  const vignette = ctx.createRadialGradient(AXIS, 105, 60, AXIS, 105, 190)
  vignette.addColorStop(0, 'rgba(143, 89, 0, 0)')
  vignette.addColorStop(1, 'rgba(143, 89, 0, 0.05)')
  ctx.fillStyle = vignette
  ctx.fillRect(0, 0, SHEET_WIDTH_MM, SHEET_HEIGHT_MM)

  drawCodeField(ctx)
  drawFrame(ctx)
  drawMicrotext(ctx)
  drawCorners(ctx)
  drawCrest(ctx, logo)
  drawEyebrow(ctx)
  drawBody(ctx, certificate)
  drawFooter(ctx, certificate)
  drawSeal(ctx)
}

async function renderTo(
  canvas: HTMLCanvasElement,
  certificate: AccountCertificate,
  pxPerMm: number,
): Promise<void> {
  const [logo] = await Promise.all([loadLogo(), loadFonts()])

  canvas.width = Math.round(SHEET_WIDTH_MM * pxPerMm)
  canvas.height = Math.round(SHEET_HEIGHT_MM * pxPerMm)

  const ctx = canvas.getContext('2d')
  if (!ctx) throw renderError()

  ctx.scale(canvas.width / SHEET_WIDTH_MM, canvas.height / SHEET_HEIGHT_MM)
  ctx.lineJoin = 'round'
  paint(ctx, certificate, logo)
}

export async function renderCertificatePreview(
  canvas: HTMLCanvasElement,
  certificate: AccountCertificate,
): Promise<void> {
  const dpr = Math.min(window.devicePixelRatio || 1, MAX_PREVIEW_DPR)
  await renderTo(canvas, certificate, PREVIEW_PX_PER_MM * dpr)
}

function slug(value: string): string {
  return (
    value
      .normalize('NFD')
      .replace(/\p{Diacritic}/gu, '')
      .toLowerCase()
      .replace(/[^a-z0-9]+/gu, '-')
      .replace(/^-+|-+$/gu, '')
      .slice(0, 48) || 'certificate'
  )
}

export function certificateFileName(certificate: AccountCertificate, extension: string): string {
  const prefix = i18n.global.t('features.account.certificates.fileNamePrefix')
  return `${slug(prefix)}-${slug(certificate.eventTitle)}-${slug(fullName(certificate))}.${extension}`
}

function renderError(): Error {
  return new Error(i18n.global.t('features.account.certificates.renderError'))
}

function release(canvas: HTMLCanvasElement): void {
  canvas.width = 0
  canvas.height = 0
}

async function deflate(data: Uint8Array): Promise<Uint8Array> {
  const compressed = new Blob([data as BlobPart])
    .stream()
    .pipeThrough(new CompressionStream('deflate'))
  return new Uint8Array(await new Response(compressed).arrayBuffer())
}

function toRgb(image: ImageData): Uint8Array {
  const rgb = new Uint8Array(image.width * image.height * 3)
  const source = image.data
  for (let read = 0, write = 0; read < source.length; read += 4, write += 3) {
    rgb[write] = source[read] ?? 0
    rgb[write + 1] = source[read + 1] ?? 0
    rgb[write + 2] = source[read + 2] ?? 0
  }
  return rgb
}

function buildPdf(width: number, height: number, image: Uint8Array): Blob {
  const encoder = new TextEncoder()
  const parts: BlobPart[] = []
  const offsets: number[] = []
  let cursor = 0

  const push = (chunk: Uint8Array | string): void => {
    const bytes = typeof chunk === 'string' ? encoder.encode(chunk) : chunk
    parts.push(bytes as BlobPart)
    cursor += bytes.length
  }
  const openObject = (): void => {
    offsets.push(cursor)
  }

  const pageWidth = ((SHEET_WIDTH_MM * 72) / 25.4).toFixed(2)
  const pageHeight = ((SHEET_HEIGHT_MM * 72) / 25.4).toFixed(2)
  const content = `q ${pageWidth} 0 0 ${pageHeight} 0 0 cm /Im0 Do Q\n`

  push('%PDF-1.4\n')
  openObject()
  push('1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n')
  openObject()
  push('2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n')
  openObject()
  push(
    `3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 ${pageWidth} ${pageHeight}]` +
      ' /Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>\nendobj\n',
  )
  openObject()
  push(
    `4 0 obj\n<< /Type /XObject /Subtype /Image /Width ${width} /Height ${height}` +
      ' /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode' +
      ` /Length ${image.length} >>\nstream\n`,
  )
  push(image)
  push('\nendstream\nendobj\n')
  openObject()
  push(`5 0 obj\n<< /Length ${content.length} >>\nstream\n${content}endstream\nendobj\n`)

  const xrefOffset = cursor
  let xref = 'xref\n0 6\n0000000000 65535 f \n'
  for (const offset of offsets) {
    xref += `${String(offset).padStart(10, '0')} 00000 n \n`
  }
  push(xref)
  push(`trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n${xrefOffset}\n%%EOF\n`)

  return new Blob(parts, { type: 'application/pdf' })
}

export async function downloadCertificatePng(certificate: AccountCertificate): Promise<void> {
  const canvas = document.createElement('canvas')
  try {
    await renderTo(canvas, certificate, EXPORT_PX_PER_MM)

    const blob = await new Promise<Blob | null>((resolve) => {
      canvas.toBlob(resolve, 'image/png')
    })
    if (!blob) throw renderError()

    downloadBlob(certificateFileName(certificate, 'png'), blob)
  } finally {
    release(canvas)
  }
}

export async function downloadCertificatePdf(certificate: AccountCertificate): Promise<void> {
  const canvas = document.createElement('canvas')
  try {
    await renderTo(canvas, certificate, EXPORT_PX_PER_MM)

    const ctx = canvas.getContext('2d')
    if (!ctx) throw renderError()

    const image = ctx.getImageData(0, 0, canvas.width, canvas.height)
    const pdf = buildPdf(canvas.width, canvas.height, await deflate(toRgb(image)))

    downloadBlob(certificateFileName(certificate, 'pdf'), pdf)
  } finally {
    release(canvas)
  }
}
