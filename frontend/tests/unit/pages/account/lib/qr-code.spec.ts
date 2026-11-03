import { describe, expect, it } from 'vitest'

import { toQrCodeDataUrl } from '@/pages/account/lib/qr-code'

describe('toQrCodeDataUrl', () => {
  it('renders the text as an SVG data URL', async () => {
    const url = await toQrCodeDataUrl('otpauth://totp/Test:ada%40example.test?secret=JBSWY3DP')

    expect(url.startsWith('data:image/svg+xml;charset=utf-8,')).toBe(true)
    const svg = decodeURIComponent(url.slice(url.indexOf(',') + 1))
    expect(svg).toContain('<svg')
    expect(svg).toContain('</svg>')
  })
})
