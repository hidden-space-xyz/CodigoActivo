import type { Locator, Page } from '@playwright/test'

import { dialog } from '../../ui/element-plus'

/**
 * The dialog «Términos y condiciones» that opens before a signup while the event has documents
 * the person has not accepted: a checkbox per document, a signature pad for the checked ones that
 * ask for it and a preview of each document.
 */
export class TermsDialog {
  /** The dialog box, found by its title. */
  readonly root: Locator

  /** «Confirmar inscripción», which sends the signup with the decisions taken. */
  readonly confirm: Locator

  constructor(private readonly page: Page) {
    this.root = dialog(page, 'Términos y condiciones')
    this.confirm = this.root.getByRole('button', { name: 'Confirmar inscripción' })
  }

  /** The checkbox «Acepto {document}». */
  accept(document: string): Locator {
    return this.root.getByRole('checkbox', { name: `Acepto ${document}`, exact: true })
  }

  /** The button with the name of `document`, which opens its preview. */
  open(document: string): Locator {
    return this.root.getByRole('button', { name: document, exact: true })
  }

  /** The preview dialog of `document`, with «Aceptar» and «Rechazar». */
  preview(document: string): Locator {
    return dialog(this.page, document)
  }

  /** The signature pad «Firma para aceptar {document}». */
  signature(document: string): Locator {
    return this.root.getByRole('img', { name: `Firma para aceptar ${document}`, exact: true })
  }

  /** The «Borrar firma» button under the signature pad of `document`. */
  clearSignature(document: string): Locator {
    return this.root
      .getByRole('listitem')
      .filter({ has: this.page.getByRole('img', { name: `Firma para aceptar ${document}` }) })
      .getByRole('button', { name: 'Borrar firma' })
  }

  /** Draws a stroke with the mouse inside the signature pad of `document`. */
  async sign(document: string): Promise<void> {
    const pad = this.signature(document)
    await pad.scrollIntoViewIfNeeded()
    const box = await pad.boundingBox()
    if (!box) throw new Error(`The signature pad of ${document} is not on screen`)
    const at = (x: number, y: number) => [box.x + box.width * x, box.y + box.height * y] as const
    await this.page.mouse.move(...at(0.2, 0.6))
    await this.page.mouse.down()
    await this.page.mouse.move(...at(0.4, 0.3), { steps: 8 })
    await this.page.mouse.move(...at(0.6, 0.7), { steps: 8 })
    await this.page.mouse.move(...at(0.8, 0.4), { steps: 8 })
    await this.page.mouse.up()
  }
}
