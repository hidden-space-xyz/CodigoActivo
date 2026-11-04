import { expect, type Locator, type Page } from '@playwright/test'

import { dialog, setSwitch } from '../../ui/element-plus'
import { pickThumbnail, writeRichText } from '../../ui/forms'

/**
 * A create or edit dialog of the content administration (news, partners, resources and catalogs):
 * labelled fields, rich text editors, the image field and the «Guardar» button.
 */
export class FormDialog {
  /** The dialog itself, found by its title. */
  readonly root: Locator

  /** The button that sends the form. */
  readonly saveButton: Locator

  constructor(page: Page, title: string) {
    this.root = dialog(page, title)
    this.saveButton = this.root.getByRole('button', { name: 'Guardar', exact: true })
  }

  /** The control whose label is exactly `label`. */
  field(label: string): Locator {
    return this.root.getByLabel(label, { exact: true })
  }

  /** Replaces the text of each labelled field with its value, in order. */
  async fill(values: Readonly<Record<string, string>>): Promise<void> {
    for (const [label, value] of Object.entries(values)) {
      await this.field(label).fill(value)
    }
  }

  /** Replaces the content of the rich text editor labelled `label` with `text`. */
  async write(label: string, text: string): Promise<void> {
    await writeRichText(this.root, label, text)
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

  /** Turns the only switch of the dialog on or off. */
  async setSwitch(on: boolean): Promise<void> {
    await setSwitch(this.root.getByRole('switch'), on)
  }
}
