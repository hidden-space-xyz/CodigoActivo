import { describe, expect, it, vi } from 'vitest'
import { downloadBlob } from '@/shared/lib/download'

describe('downloadBlob', () => {
  it('clicks a temporary anchor and revokes the object URL afterwards', () => {
    vi.useFakeTimers()
    const revoke = vi.spyOn(URL, 'revokeObjectURL')
    const anchors: HTMLAnchorElement[] = []
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:file')
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      anchors.push(this)
      expect(document.body.contains(this)).toBe(true)
    })

    downloadBlob('report.pdf', new Blob(['x']))

    const [anchor] = anchors
    expect(anchor?.download).toBe('report.pdf')
    expect(anchor?.href).toBe('blob:file')
    expect(anchor?.rel).toBe('noopener')
    expect(document.body.querySelector('a')).toBeNull()
    expect(revoke).not.toHaveBeenCalled()

    vi.runAllTimers()
    expect(revoke).toHaveBeenCalledWith('blob:file')
  })
})
