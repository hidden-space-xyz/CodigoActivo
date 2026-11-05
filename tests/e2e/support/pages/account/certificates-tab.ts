import type { Locator, Page } from '@playwright/test'

/** The tab «Diplomas»: a tile per person and ended event with a confirmed signup. */
export class CertificatesTab {
  /** The message shown while the household has no diplomas. */
  readonly empty: Locator

  constructor(private readonly page: Page) {
    this.empty = page.getByTestId('certificates-empty')
  }

  /** The button of the tile `tile(name, event)` that opens its diploma. */
  open(name: string, event: string): Locator {
    return this.tile(name, event).getByTestId('certificate-tile-open')
  }

  /** The tile of the diploma of the person `name` for the event `event`, with its code and downloads. */
  tile(name: string, event: string): Locator {
    return this.page
      .getByTestId('certificate-tile')
      .filter({ hasText: name })
      .filter({ hasText: event })
  }
}

/** The dialog «Diploma de participación», which previews a diploma on a canvas. */
export class CertificateDialog {
  /** The dialog itself. */
  readonly root: Locator

  /** «Descargar PNG». */
  readonly downloadPng: Locator

  constructor(page: Page) {
    this.root = page.getByTestId('certificate-dialog')
    this.downloadPng = this.root.getByTestId('certificate-dialog-download-png')
  }

  /** The canvas that draws the diploma, an image labelled with the title of the event. */
  preview(event: string): Locator {
    return this.root
      .getByTestId('certificate-dialog-canvas')
      .and(this.root.locator(`[aria-label=${JSON.stringify(event)}]`))
  }
}

/** The name the page gives the PNG of the diploma of `name` for `event`. */
export function certificatePngName(event: string, name: string): string {
  return `diploma-${slug(event)}-${slug(name)}.png`
}

function slug(value: string): string {
  return value
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/gu, '-')
    .replace(/^-+|-+$/gu, '')
    .slice(0, 48)
}
