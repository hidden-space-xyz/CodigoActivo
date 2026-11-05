import { expect, type Locator, type Page } from '@playwright/test'

import { t } from '../../i18n'
import { writeRichText } from '../../ui/forms'
import { AdminShell } from '../admin-shell'

const ANY_DATE = '\n'

/** A tab of the «Políticas» page, named after its policy. */
export type PolicyTab = 'privacy' | 'cookies'

/** The editor of one policy: its status line, rich text editor, actions and list of versions. */
export class PolicyEditor {
  /** The tab panel of the policy. */
  readonly root: Locator

  /** The editable area of the rich text editor of the policy. */
  readonly editor: Locator

  /** The note shown while the draft differs from the version in force. */
  readonly unpublishedChanges: Locator

  /** The button that puts the version in force back into the editor. */
  readonly discard: Locator

  /** The button that asks to publish the draft as a new version. */
  readonly publish: Locator

  /** The list of published versions, newest first. */
  readonly versions: Locator

  private readonly field: Locator

  constructor(
    private readonly page: Page,
    name: PolicyTab,
  ) {
    this.root = page.getByTestId(`legal-policies-${name}`)
    this.field = this.root.getByTestId('legal-policy-editor-text')
    this.editor = this.field.locator('.ProseMirror')
    this.unpublishedChanges = this.root.getByTestId('legal-policy-editor-dirty')
    this.discard = this.root.getByTestId('legal-policy-editor-discard')
    this.publish = this.root.getByTestId('legal-policy-editor-publish')
    this.versions = this.root.getByTestId('legal-policy-editor-versions')
  }

  /** The status line, «Versión {version} en vigor, publicada el …». */
  status(version: number): Locator {
    const [inForce = ''] = t('pages.admin.legalPolicies.status.published', {
      version,
      date: ANY_DATE,
    }).split(ANY_DATE)
    return this.root.getByTestId('legal-policy-editor-status').filter({ hasText: inForce })
  }

  /** The entry of the version `version` in the list of versions. */
  version(version: number): Locator {
    const number = t('pages.admin.legalPolicies.versions.number', { version })
    return this.versions.getByTestId('legal-policy-editor-version').filter({
      has: this.page
        .getByTestId('legal-policy-editor-version-number')
        .filter({ hasText: new RegExp(`^\\s*${number}\\s*$`) }),
    })
  }

  /** Replaces the draft with `text`. */
  async write(text: string): Promise<void> {
    await writeRichText(this.field, text)
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
    await this.shell.open('admin-legal-policies')
    await expect(this.policy('privacy').editor).toBeVisible()
  }

  /** The tab of the policy `name`. */
  tab(name: PolicyTab): Locator {
    return this.page.getByTestId(`legal-policies-${name}-tab`)
  }

  /** The editor of the policy `name`. */
  policy(name: PolicyTab): PolicyEditor {
    return new PolicyEditor(this.page, name)
  }
}
