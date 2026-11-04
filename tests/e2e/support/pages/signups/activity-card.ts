import { expect, type Locator, type Page } from '@playwright/test'

import { chooseOption } from '../../ui/element-plus'

/**
 * The card of one activity in the activities tab of the event page: its signup status, the role
 * select and the buttons to sign oneself or the family up and to withdraw.
 */
export class ActivityCard {
  /** The status tag of the signed-in person's own signup, such as «Solicitada». */
  readonly status: Locator

  /** The role select, shown when the person may take more than one role. */
  readonly role: Locator

  /** «Apuntarme», which signs the person up with the chosen role. */
  readonly signUp: Locator

  /** «Desapuntarme», which withdraws the person's own signup. */
  readonly withdraw: Locator

  /** «Apuntar a mi familia», which opens the household dialog before anyone is signed up. */
  readonly signUpFamily: Locator

  /** «Apuntar a otro miembro», which opens the household dialog once someone is signed up. */
  readonly signUpAnother: Locator

  /** «Inicia sesión para apuntarte», what a visitor is offered instead. */
  readonly login: Locator

  private readonly page: Page

  constructor(readonly root: Locator) {
    this.page = root.page()
    this.status = root.locator('.act__head .el-tag')
    this.role = root.getByRole('combobox')
    this.signUp = root.getByRole('button', { name: 'Apuntarme', exact: true })
    this.withdraw = root.getByRole('button', { name: 'Desapuntarme', exact: true })
    this.signUpFamily = root.getByRole('button', { name: 'Apuntar a mi familia' })
    this.signUpAnother = root.getByRole('button', { name: 'Apuntar a otro miembro' })
    this.login = root.getByRole('button', { name: 'Inicia sesión para apuntarte' })
  }

  /** Picks `role` in the role select. */
  async chooseRole(role: string): Promise<void> {
    await chooseOption(this.role, role)
  }

  /** Picks `role` and presses «Apuntarme». */
  async signUpAs(role: string): Promise<void> {
    await this.chooseRole(role)
    await this.signUp.click()
  }

  /** Opens the role select, checks it offers exactly `roles` in that order and closes it. */
  async expectRoles(roles: readonly string[]): Promise<void> {
    await expectSelectOptions(this.page, this.role, roles)
  }

  /** The line «{name} · {role}» of a household member's signup. */
  member(name: string, role: string): Locator {
    return this.root.getByRole('listitem').filter({ hasText: `${name} · ${role}` })
  }

  /** The «Desapuntar» button of a household member's signup. */
  withdrawMember(name: string, role: string): Locator {
    return this.member(name, role).getByRole('button', { name: 'Desapuntar', exact: true })
  }
}

/** Opens an Element Plus select, checks it offers exactly `options` in order and closes it. */
export async function expectSelectOptions(
  page: Page,
  select: Locator,
  options: readonly string[],
): Promise<void> {
  const shown = page.getByRole('option').filter({ visible: true })
  const box = select.locator('xpath=ancestor::div[contains(@class, "el-select__wrapper")][1]')
  await box.click()
  await expect(shown).toHaveText(options)
  await box.click()
  await expect(shown).toHaveCount(0)
}
