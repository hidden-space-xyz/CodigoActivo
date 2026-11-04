import { expect, type Locator, type Page } from '@playwright/test'

import type { Minor, Person } from '../data/people'
import { chooseOption, setChecked } from '../ui/element-plus'

const GENDER_LABELS = {
  Male: 'Hombre',
  Female: 'Mujer',
  Other: 'Otro',
  PreferNotToSay: 'Prefiero no decirlo',
} as const

/** What the registration form is filled in with. */
export interface RegistrationForm {
  readonly person: Person
  readonly password: string
  readonly confirmPassword?: string
  readonly minors?: readonly Minor[]
  readonly newsConsent?: boolean
}

/** The registration page, at `/register`: the age gate, the form and its confirmation. */
export class RegistrationPage {
  /** The button that confirms being an adult. */
  readonly confirmAdult: Locator

  /** The button that says the visitor is a minor. */
  readonly declineAdult: Locator

  /** The submit button of the form. */
  readonly submit: Locator

  /** The heading shown once the registration was accepted. */
  readonly success: Locator

  constructor(private readonly page: Page) {
    this.confirmAdult = page.getByRole('button', { name: 'Sí, soy mayor de edad' })
    this.declineAdult = page.getByRole('button', { name: 'No, soy menor' })
    this.submit = page.getByRole('button', { name: 'Completar registro' })
    this.success = page.getByRole('heading', { name: '¡Ya casi está!' })
  }

  /** Opens the page and passes the age gate. */
  async open(): Promise<void> {
    await this.page.goto('/register')
    await this.confirmAdult.click()
    await expect(this.section('Tus datos')).toBeVisible()
  }

  /** The form section under the heading `title`. */
  section(title: string): Locator {
    return this.page
      .locator('form section')
      .filter({ has: this.page.getByRole('heading', { name: title, exact: true }) })
  }

  /** The fieldset of the `n`-th minor. */
  minor(n: number): Locator {
    return this.page.getByRole('group', { name: `Menor ${n}` })
  }

  /** The field error linked to the control labelled `label` in `scope`. */
  fieldError(scope: Locator, label: string): Locator {
    return scope.locator('.ca-field').filter({ hasText: label }).locator('.ca-field__error')
  }

  /** Fills in every field of the form. */
  async fill(form: RegistrationForm): Promise<void> {
    const { person } = form
    const yourData = this.section('Tus datos')
    await yourData.getByLabel('Nombre', { exact: true }).fill(person.firstName)
    await yourData.getByLabel('Apellidos', { exact: true }).fill(person.lastName)
    await yourData.getByLabel('DNI/NIE').fill(person.nationalId)
    await chooseOption(yourData.getByLabel('Género'), GENDER_LABELS[person.gender])
    await this.section('Contacto').getByLabel('Teléfono', { exact: true }).fill(person.phone)
    const access = this.section('Acceso a tu cuenta')
    await access.getByLabel('Correo', { exact: true }).fill(person.email)
    await access.getByLabel('Contraseña', { exact: true }).fill(form.password)
    await access.getByLabel('Repite la contraseña').fill(form.confirmPassword ?? form.password)
    for (const [index, minor] of (form.minors ?? []).entries()) {
      await this.addMinor(index + 1, minor)
    }
    const consent = this.page.getByRole('checkbox', { name: /Quiero recibir por email novedades/ })
    await setChecked(consent, !!form.newsConsent)
  }

  /** Adds the `n`-th minor of the household. */
  async addMinor(n: number, minor: Minor): Promise<void> {
    await this.page.getByRole('button', { name: '+ Añadir menor' }).click()
    const fieldset = this.minor(n)
    await fieldset.getByLabel('Nombre', { exact: true }).fill(minor.firstName)
    await fieldset.getByLabel('Apellidos', { exact: true }).fill(minor.lastName)
    await fieldset.getByLabel('Fecha de nacimiento').fill(minor.birthDate)
    await chooseOption(fieldset.getByLabel('Género'), GENDER_LABELS[minor.gender])
  }
}

/** The label the interface shows for each gender. */
export const GENDER_NAMES = GENDER_LABELS
