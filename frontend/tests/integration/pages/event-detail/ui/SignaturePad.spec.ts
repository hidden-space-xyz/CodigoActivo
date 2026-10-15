import { flushPromises } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import SignaturePad from '@/pages/event-detail/ui/SignaturePad.vue'

import { createFakeContext } from '../../../../support/canvas'
import { click, findButton } from '../../../../support/dom'
import { renderWithProviders, t } from '../../../../support/render'
import { FAKE_SIGNATURE, fakePads, resetFakePads } from '../../../../support/signature-pad-mock'

vi.mock(
  'signature_pad',
  async () => (await import('../../../../support/signature-pad-mock')).signaturePadModule,
)

const LABEL = 'Firma para aceptar Autorización'

function renderPad(modelValue = '') {
  return renderWithProviders(SignaturePad, {
    props: {
      label: LABEL,
      modelValue,
      'onUpdate:modelValue': () => undefined,
    },
    attach: true,
  })
}

function pad() {
  const created = fakePads.at(-1)
  if (!created) throw new Error('No signature pad was created')
  return created
}

describe('SignaturePad', () => {
  const fake = createFakeContext()

  beforeEach(() => {
    resetFakePads()
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(fake.context)
    vi.spyOn(HTMLElement.prototype, 'offsetWidth', 'get').mockReturnValue(300)
    vi.spyOn(HTMLElement.prototype, 'offsetHeight', 'get').mockReturnValue(160)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('names the drawing area and draws with the signature ink', async () => {
    await renderPad()

    const canvas = document.body.querySelector('canvas')
    expect(canvas?.getAttribute('aria-label')).toBe(LABEL)
    expect(pad().canvas).toBe(canvas)
    expect(pad().options.penColor).toBe('black')
  })

  it('sizes the canvas to its box at the screen density, at most twice', async () => {
    vi.stubGlobal('devicePixelRatio', 3)

    await renderPad()

    const canvas = pad().canvas
    expect(canvas.width).toBe(600)
    expect(canvas.height).toBe(320)
  })

  it('reports the drawing as a PNG data URL once a stroke ends', async () => {
    const { wrapper } = await renderPad()

    pad().draw()

    expect(wrapper.emitted('update:modelValue')).toEqual([[FAKE_SIGNATURE]])
  })

  it('clears the drawing and the value, and is only clearable once signed', async () => {
    const { wrapper } = await renderPad()
    expect(findButton(t('pages.eventDetail.terms.signature.clear')).disabled).toBe(true)

    pad().draw()
    await wrapper.setProps({ modelValue: FAKE_SIGNATURE })
    await click(findButton(t('pages.eventDetail.terms.signature.clear')))

    expect(pad().isEmpty()).toBe(true)
    expect(wrapper.emitted('update:modelValue')?.at(-1)).toEqual([''])
  })

  it('wipes the drawing when the value is emptied from outside', async () => {
    const { wrapper } = await renderPad()
    pad().draw()
    await wrapper.setProps({ modelValue: FAKE_SIGNATURE })

    await wrapper.setProps({ modelValue: '' })

    expect(pad().isEmpty()).toBe(true)
  })

  it('without a resize observer, keeps the strokes when the window is resized until unmounted', async () => {
    vi.stubGlobal('ResizeObserver', undefined)
    const { wrapper } = await renderPad()
    pad().draw()
    const cleared = pad().cleared

    window.dispatchEvent(new Event('resize'))

    expect(pad().cleared).toBe(cleared + 1)
    expect(pad().isEmpty()).toBe(false)

    wrapper.unmount()
    window.dispatchEvent(new Event('resize'))

    expect(pad().cleared).toBe(cleared + 1)
    expect(pad().disabled).toBe(true)
  })

  it('follows the size of its box when the browser can observe it', async () => {
    const observed: Element[] = []
    const disconnect = vi.fn()
    let notify: () => void = () => undefined
    vi.stubGlobal(
      'ResizeObserver',
      class {
        constructor(callback: () => void) {
          notify = callback
        }

        observe(element: Element): void {
          observed.push(element)
        }

        disconnect(): void {
          disconnect()
        }
      },
    )
    const { wrapper } = await renderPad()
    const cleared = pad().cleared

    notify()
    await flushPromises()

    expect(observed).toEqual([pad().canvas])
    expect(pad().cleared).toBe(cleared + 1)

    wrapper.unmount()

    expect(disconnect).toHaveBeenCalledOnce()
  })
})
