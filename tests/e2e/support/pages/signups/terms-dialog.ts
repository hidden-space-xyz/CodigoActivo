import type { Locator, Page } from '@playwright/test'

/**
 * The dialog «Términos y condiciones» that opens before a signup while the event has documents
 * the person has not accepted: a checkbox per document, a signature pad for the checked ones that
 * ask for it and a preview of each document.
 */
export class TermsDialog {
  /** The dialog box. */
  readonly root: Locator

  /** «Confirmar inscripción», which sends the signup with the decisions taken. */
  readonly confirm: Locator

  constructor(private readonly page: Page) {
    this.root = page.getByTestId('terms-dialog')
    this.confirm = this.root.getByTestId('terms-dialog-confirm')
  }

  /** The checkbox «Acepto {document}». */
  accept(document: string): Locator {
    return this.row(document).getByTestId('terms-dialog-accept')
  }

  /** The button with the name of `document`, which opens its preview. */
  open(document: string): Locator {
    return this.row(document).getByTestId('terms-dialog-document')
  }

  /** The preview dialog of `document`, with «Aceptar» and «Rechazar». */
  preview(document: string): Locator {
    return this.page.getByTestId('terms-dialog-preview').filter({ hasText: document })
  }

  /** The signature pad «Firma para aceptar {document}». */
  signature(document: string): Locator {
    return this.row(document).getByTestId('signature-pad-canvas')
  }

  /** The «Borrar firma» button under the signature pad of `document`. */
  clearSignature(document: string): Locator {
    return this.row(document).getByTestId('signature-pad-clear')
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

  private row(document: string): Locator {
    return this.root.getByTestId('terms-dialog-row').filter({
      has: this.page.getByTestId('terms-dialog-document').filter({ hasText: document }),
    })
  }
}
