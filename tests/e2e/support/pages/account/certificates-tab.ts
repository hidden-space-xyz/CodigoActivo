import type { Locator, Page } from '@playwright/test'

/** The tab «Diplomas»: a tile per person and ended event with a confirmed signup. */
export class CertificatesTab {
  /** The message shown while the household has no diplomas. */
  readonly empty: Locator

  constructor(private readonly page: Page) {
    this.empty = page.getByText('Todavía no tienes diplomas.')
  }

  /** The button that opens the diploma of the person `name` for the event `event`. */
  open(name: string, event: string): Locator {
    return this.page.getByRole('button', { name: `Ver el diploma de ${name} en ${event}` })
  }

  /** The tile around the button `open(name, event)`, with the code and the download buttons. */
  tile(name: string, event: string): Locator {
    return this.page.locator('li.cert-tile').filter({ has: this.open(name, event) })
  }
}

/** The dialog «Diploma de participación», which previews a diploma on a canvas. */
export class CertificateDialog {
  /** The dialog itself. */
  readonly root: Locator

  /** «Descargar PNG». */
  readonly downloadPng: Locator

  constructor(page: Page) {
    this.root = page.getByRole('dialog', { name: 'Diploma de participación' })
    this.downloadPng = this.root.getByRole('button', { name: 'Descargar PNG' })
  }

  /** The canvas that draws the diploma, an image named after the event. */
  preview(event: string): Locator {
    return this.root.getByRole('img', { name: event })
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
