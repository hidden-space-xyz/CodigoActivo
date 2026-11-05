import { expect, type Locator, type Page } from '@playwright/test'
import type { Gender } from '@contract/gender'

import type { Minor, Person } from '../data/people'
import { t } from '../i18n'
import { chooseOption, setChecked } from '../ui/element-plus'

const GENDER_LABELS: Readonly<Record<Gender, string>> = {
  Male: t('entities.user.gender.Male'),
  Female: t('entities.user.gender.Female'),
  Other: t('entities.user.gender.Other'),
  PreferNotToSay: t('entities.user.gender.PreferNotToSay'),
}

/** A section of the registration form. */
export type RegistrationSection = 'your-data' | 'contact' | 'access'

/** A field of the registration form that shows its own error. */
export type RegistrationField = 'password' | 'password-confirm'

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

  /** The confirmation shown once the registration was accepted. */
  readonly success: Locator

  constructor(private readonly page: Page) {
    this.confirmAdult = page.getByTestId('register-age-confirm')
    this.declineAdult = page.getByTestId('register-age-decline')
    this.submit = page.getByTestId('register-submit')
    this.success = page.getByTestId('register-success')
  }

  /** Opens the page and passes the age gate. */
  async open(): Promise<void> {
    await this.page.goto('/register')
    await this.confirmAdult.click()
    await expect(this.section('your-data')).toBeVisible()
  }

  /** The form section `name`. */
  section(name: RegistrationSection): Locator {
    return this.page.getByTestId(`register-${name}`)
  }

  /** The fieldset of the `n`-th minor. */
  minor(n: number): Locator {
    return this.page.getByTestId('register-minor').nth(n - 1)
  }

  /** The error of the field `field` in `scope`. */
  fieldError(scope: Locator, field: RegistrationField): Locator {
    return scope.getByTestId(`register-${field}-error`)
  }

  /** Fills in every field of the form. */
  async fill(form: RegistrationForm): Promise<void> {
    const { person } = form
    const yourData = this.section('your-data')
    await yourData.getByTestId('register-first-name').fill(person.firstName)
    await yourData.getByTestId('register-last-name').fill(person.lastName)
    await yourData.getByTestId('register-national-id').locator('input').fill(person.nationalId)
    await chooseOption(yourData.getByTestId('register-gender'), person.gender)
    await this.section('contact').getByTestId('register-phone').fill(person.phone)
    const access = this.section('access')
    await access.getByTestId('register-email').fill(person.email)
    await access.getByTestId('register-password').fill(form.password)
    await access
      .getByTestId('register-password-confirm')
      .fill(form.confirmPassword ?? form.password)
    for (const [index, minor] of (form.minors ?? []).entries()) {
      await this.addMinor(index + 1, minor)
    }
    await setChecked(this.page.getByTestId('register-news-consent'), !!form.newsConsent)
  }

  /** Adds the `n`-th minor of the household. */
  async addMinor(n: number, minor: Minor): Promise<void> {
    await this.page.getByTestId('register-add-minor').click()
    const fieldset = this.minor(n)
    await fieldset.getByTestId('register-minor-first-name').fill(minor.firstName)
    await fieldset.getByTestId('register-minor-last-name').fill(minor.lastName)
    await fieldset.getByTestId('register-minor-birth-date').fill(minor.birthDate)
    await chooseOption(fieldset.getByTestId('register-minor-gender'), minor.gender)
  }
}

/** The label the interface shows for each gender. */
export const GENDER_NAMES = GENDER_LABELS
