import { expect, type Locator, type Page } from '@playwright/test'

import { CATALOG, type RoleName, type SignupStatusName } from '../../api/catalogs'
import { t } from '../../i18n'
import { chooseOption } from '../../ui/element-plus'

/** How the API names each activity role, which the event page shows as it comes. */
export const ROLE_LABELS: Readonly<Record<RoleName, string>> = {
  leader: 'Líder',
  volunteer: 'Voluntario',
  participant: 'Participante',
}

/** How the API names each signup status, which the event page shows as it comes. */
export const STATUS_LABELS: Readonly<Record<SignupStatusName, string>> = {
  requested: 'Solicitada',
  confirmed: 'Confirmada',
  denied: 'Rechazada',
}

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
    this.status = root.getByTestId('activity-card-status')
    this.role = root.getByTestId('activity-card-role')
    this.signUp = root.getByTestId('activity-card-sign-up')
    this.withdraw = root.getByTestId('activity-card-withdraw')
    const household = root.getByTestId('activity-card-household')
    this.signUpFamily = household.filter({ hasText: t('pages.eventDetail.card.enrollFamily') })
    this.signUpAnother = household.filter({ hasText: t('pages.eventDetail.card.enrollAnother') })
    this.login = root.getByTestId('activity-card-login')
  }

  /** Picks `role` in the role select. */
  async chooseRole(role: RoleName): Promise<void> {
    await chooseOption(this.role, CATALOG.roles[role])
  }

  /** Picks `role` and presses «Apuntarme». */
  async signUpAs(role: RoleName): Promise<void> {
    await this.chooseRole(role)
    await this.signUp.click()
  }

  /** Opens the role select, checks it offers exactly `roles` in that order and closes it. */
  async expectRoles(roles: readonly RoleName[]): Promise<void> {
    await expectSelectOptions(
      this.page,
      this.role,
      roles.map((role) => CATALOG.roles[role]),
    )
  }

  /** The note beside the actions that reads `text`, such as «Tu inscripción: Voluntario». */
  note(text: string): Locator {
    return this.root.getByTestId('activity-card-note').filter({ hasText: text })
  }

  /** The line «{name} · {role}» of a household member's signup. */
  member(name: string, role: RoleName): Locator {
    return this.root
      .getByTestId('activity-card-member')
      .filter({ hasText: `${name} · ${ROLE_LABELS[role]}` })
  }

  /** The «Desapuntar» button of a household member's signup. */
  withdrawMember(name: string, role: RoleName): Locator {
    return this.member(name, role).getByTestId('activity-card-member-withdraw')
  }
}

/**
 * Opens an Element Plus select, given the element that carries its test id, checks it offers
 * exactly the options whose values are `values`, in that order, and closes it.
 */
export async function expectSelectOptions(
  page: Page,
  select: Locator,
  values: readonly string[],
): Promise<void> {
  const shown = page.getByTestId(/^option-/).filter({ visible: true })
  await select.click()
  await expect
    .poll(() => shown.evaluateAll((options) => options.map((option) => option.dataset.testid)))
    .toStrictEqual(values.map((value) => `option-${value}`))
  await select.click()
  await expect(shown).toHaveCount(0)
}
