/** One recorded `fillText` call with the drawing state that applied to it. */
export interface TextDraw {
  readonly text: string
  readonly x: number
  readonly y: number
  readonly font: string
  readonly fillStyle: string
}

/** Options for `createFakeContext`. */
export interface FakeContextOptions {
  /** Returns `true` for fonts whose glyphs should measure zero width. */
  readonly zeroWidth?: (font: string) => boolean
  /** Pixels returned by `getImageData` (RGBA). Defaults to a 2x1 image. */
  readonly imageData?: { width: number; height: number; data: Uint8ClampedArray }
}

interface DrawState {
  font: string
  fillStyle: string
  strokeStyle: string
  lineWidth: number
  textAlign: CanvasTextAlign
  textBaseline: CanvasTextBaseline
  globalAlpha: number
}

/**
 * Minimal recording 2D context for jsdom, which has no canvas backend. Glyph widths are
 * proportional to the pixel size in the current `font`, so layout code that fits, wraps and
 * ellipsizes text behaves deterministically.
 */
export function createFakeContext(options: FakeContextOptions = {}) {
  const texts: TextDraw[] = []
  const images: { image: unknown; args: number[] }[] = []
  const calls: string[] = []
  const scales: [number, number][] = []
  const stack: DrawState[] = []
  const gradients: { stops: [number, string][] }[] = []

  const state: DrawState = {
    font: '10px sans-serif',
    fillStyle: '#000000',
    strokeStyle: '#000000',
    lineWidth: 1,
    textAlign: 'start',
    textBaseline: 'alphabetic',
    globalAlpha: 1,
  }

  const record = (name: string) => (): void => {
    calls.push(name)
  }

  const ctx = {
    get font() {
      return state.font
    },
    set font(value: string) {
      state.font = value
    },
    get fillStyle() {
      return state.fillStyle
    },
    set fillStyle(value: string | { stops: unknown }) {
      state.fillStyle = typeof value === 'string' ? value : 'gradient'
    },
    get strokeStyle() {
      return state.strokeStyle
    },
    set strokeStyle(value: string) {
      state.strokeStyle = value
    },
    get lineWidth() {
      return state.lineWidth
    },
    set lineWidth(value: number) {
      state.lineWidth = value
    },
    get textAlign() {
      return state.textAlign
    },
    set textAlign(value: CanvasTextAlign) {
      state.textAlign = value
    },
    get textBaseline() {
      return state.textBaseline
    },
    set textBaseline(value: CanvasTextBaseline) {
      state.textBaseline = value
    },
    get globalAlpha() {
      return state.globalAlpha
    },
    set globalAlpha(value: number) {
      state.globalAlpha = value
    },
    lineJoin: 'miter' as CanvasLineJoin,
    save: () => {
      calls.push('save')
      stack.push({ ...state })
    },
    restore: () => {
      calls.push('restore')
      const previous = stack.pop()
      if (previous) Object.assign(state, previous)
    },
    scale: (x: number, y: number) => {
      scales.push([x, y])
    },
    measureText: (text: string) => {
      if (options.zeroWidth?.(state.font)) return { width: 0 }
      const size = Number(/(\d+(?:\.\d+)?)px/u.exec(state.font)?.[1] ?? '10')
      return { width: [...text].length * size * 0.6 }
    },
    fillText: (text: string, x: number, y: number) => {
      texts.push({ text, x, y, font: state.font, fillStyle: state.fillStyle })
    },
    drawImage: (image: unknown, ...args: number[]) => {
      images.push({ image, args })
    },
    createRadialGradient: () => {
      const gradient = {
        stops: [] as [number, string][],
        addColorStop(offset: number, color: string) {
          gradient.stops.push([offset, color])
        },
      }
      gradients.push(gradient)
      return gradient
    },
    getImageData: () =>
      options.imageData ?? {
        width: 2,
        height: 1,
        data: new Uint8ClampedArray([10, 20, 30, 255, 40, 50, 60, 255]),
      },
    translate: record('translate'),
    rotate: record('rotate'),
    beginPath: record('beginPath'),
    closePath: record('closePath'),
    moveTo: record('moveTo'),
    lineTo: record('lineTo'),
    stroke: record('stroke'),
    fill: record('fill'),
    fillRect: record('fillRect'),
    strokeRect: record('strokeRect'),
    arc: record('arc'),
    rect: record('rect'),
    clip: record('clip'),
    setLineDash: record('setLineDash'),
  }

  return {
    context: ctx as unknown as CanvasRenderingContext2D,
    texts,
    images,
    calls,
    scales,
    gradients,
    get depth() {
      return stack.length
    },
  }
}
