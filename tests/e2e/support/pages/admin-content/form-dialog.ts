import { expect, type Locator, type Page } from '@playwright/test'

import { setSwitch } from '../../ui/element-plus'
import { pickThumbnail, writeRichText } from '../../ui/forms'

/**
 * A create or edit dialog of the content administration (news, partners, resources and catalogs):
 * fields with test ids, rich text editors, the image field and the «Guardar» button.
 */
export class FormDialog {
  /** The dialog itself, found by its test id. */
  readonly root: Locator

  /** The button that sends the form, the `form-dialog-save` of the dialog. */
  readonly saveButton: Locator

  constructor(page: Page, testId: string) {
    this.root = page.getByTestId(testId)
    this.saveButton = this.root.getByTestId('form-dialog-save')
  }

  /** The control, or the message, of the dialog that carries the test id `testId`. */
  field(testId: string): Locator {
    return this.root.getByTestId(testId)
  }

  /**
   * The text box inside the field that carries the test id `testId`: Element Plus date pickers and
   * number inputs keep the test id on the element around it.
   */
  input(testId: string): Locator {
    return this.field(testId).locator('input').first()
  }

  /** Replaces the text of each field, keyed by its test id, with its value, in order. */
  async fill(values: Readonly<Record<string, string>>): Promise<void> {
    for (const [testId, value] of Object.entries(values)) {
      await this.field(testId).fill(value)
    }
  }

  /** Replaces the content of the rich text editor that carries the test id `testId` with `text`. */
  async write(testId: string, text: string): Promise<void> {
    await writeRichText(this.field(testId), text)
  }

  /** Picks the suite's image in the «Imagen» field. */
  async pickImage(): Promise<void> {
    await pickThumbnail(this.root)
  }

  /** Sends the form with «Guardar». */
  async save(): Promise<void> {
    await this.saveButton.click()
  }

  /** Sends the form and waits for the dialog to close once the change was accepted. */
  async saveAndClose(): Promise<void> {
    await this.save()
    await expect(this.root).toBeHidden()
  }

  /** Turns the switch that carries the test id `testId` on or off. */
  async setSwitch(testId: string, on: boolean): Promise<void> {
    await setSwitch(this.field(testId), on)
  }
}
