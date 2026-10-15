/** PNG data URL every fake pad exports once something is drawn on it. */
export const FAKE_SIGNATURE = 'data:image/png;base64,ZmFrZS1zaWduYXR1cmU='

/** Every fake pad created since the last `resetFakePads()`, in creation order. */
export const fakePads: FakeSignaturePad[] = []

/**
 * Minimal stand-in for signature_pad's default export: records its canvas and options, keeps the
 * strokes it is given and lets a test draw with `draw()`, which fires `endStroke` like a real pad.
 */
export class FakeSignaturePad {
  strokes: unknown[] = []
  disabled = false
  cleared = 0
  private readonly listeners = new Map<string, (() => void)[]>()

  constructor(
    readonly canvas: HTMLCanvasElement,
    readonly options: { penColor?: string } = {},
  ) {
    fakePads.push(this)
  }

  addEventListener(type: string, listener: () => void): void {
    this.listeners.set(type, [...(this.listeners.get(type) ?? []), listener])
  }

  /** Draws one stroke as a finger or a mouse would. */
  draw(): void {
    this.strokes.push({ points: [{ x: 1, y: 1 }] })
    for (const listener of this.listeners.get('endStroke') ?? []) listener()
  }

  isEmpty(): boolean {
    return this.strokes.length === 0
  }

  clear(): void {
    this.cleared += 1
    this.strokes = []
  }

  toData(): unknown[] {
    return [...this.strokes]
  }

  fromData(strokes: unknown[]): void {
    this.strokes = [...strokes]
  }

  toDataURL(): string {
    return FAKE_SIGNATURE
  }

  off(): void {
    this.disabled = true
  }
}

/** Forgets the pads created by previous tests. */
export function resetFakePads(): void {
  fakePads.length = 0
}

/** Module shape returned from `vi.mock('signature_pad', ...)`. */
export const signaturePadModule = { default: FakeSignaturePad }
