import { expect, type Locator, type Page } from '@playwright/test'

import { writeRichText } from '../../ui/forms'
import { AdminShell } from '../admin-shell'

/** A tab of the «Políticas» page, named after its policy. */
export type PolicyTab = 'Política de privacidad' | 'Política de cookies'

/** The editor of one policy: its status line, rich text editor, actions and list of versions. */
export class PolicyEditor {
  /** The tab panel of the policy. */
  readonly root: Locator

  /** The rich text editor, named after the policy. */
  readonly editor: Locator

  /** The note shown while the draft differs from the version in force. */
  readonly unpublishedChanges: Locator

  /** The button that puts the version in force back into the editor. */
  readonly discard: Locator

  /** The button that asks to publish the draft as a new version. */
  readonly publish: Locator

  /** The list of published versions, newest first. */
  readonly versions: Locator

  constructor(
    private readonly page: Page,
    private readonly name: PolicyTab,
  ) {
    this.root = page.getByRole('tabpanel', { name, exact: true })
    this.editor = this.root.locator('.ProseMirror')
    this.unpublishedChanges = this.root.getByText('Hay cambios sin publicar.')
    this.discard = this.root.getByRole('button', { name: 'Descartar cambios', exact: true })
    this.publish = this.root.getByRole('button', { name: 'Publicar versión nueva', exact: true })
    this.versions = this.root.getByRole('region', { name: 'Versiones', exact: true })
  }

  /** The status line, «Versión {version} en vigor, publicada el …». */
  status(version: number): Locator {
    return this.root.getByText(new RegExp(`^Versión ${version} en vigor, publicada el `))
  }

  /** The entry of the version `version` in the list of versions. */
  version(version: number): Locator {
    return this.versions
      .getByRole('listitem')
      .filter({ has: this.page.getByText(`Versión ${version}`, { exact: true }) })
  }

  /** Replaces the draft with `text`. */
  async write(text: string): Promise<void> {
    await writeRichText(this.root, this.name, text)
  }

  /** Empties the draft. */
  async clear(): Promise<void> {
    await this.editor.click()
    await this.editor.press('ControlOrMeta+a')
    await this.editor.press('Delete')
    await expect(this.editor).toHaveText('')
  }
}

/** The «Políticas» page: one tab per legal policy, each with its editor and its versions. */
export class LegalPoliciesPage {
  /** The administration layout around the page. */
  readonly shell: AdminShell

  constructor(private readonly page: Page) {
    this.shell = new AdminShell(page)
  }

  /** Opens the page on its first tab and waits for the privacy policy to load. */
  async open(): Promise<void> {
    await this.shell.open('Políticas')
    await expect(this.policy('Política de privacidad').editor).toBeVisible()
  }

  /** The tab of the policy `name`. */
  tab(name: PolicyTab): Locator {
    return this.page.getByRole('tab', { name, exact: true })
  }

  /** The editor of the policy `name`. */
  policy(name: PolicyTab): PolicyEditor {
    return new PolicyEditor(this.page, name)
  }
}
