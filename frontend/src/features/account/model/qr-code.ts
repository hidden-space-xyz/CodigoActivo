import QRCode from 'qrcode'

/**
 * Renders `text` as a QR code and returns it as an SVG data URL for an `<img>` source. Rendering
 * is pure JavaScript, so it also works where no canvas is available.
 */
export async function toQrCodeDataUrl(text: string): Promise<string> {
  const svg = await QRCode.toString(text, { type: 'svg', margin: 1, errorCorrectionLevel: 'M' })
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`
}
