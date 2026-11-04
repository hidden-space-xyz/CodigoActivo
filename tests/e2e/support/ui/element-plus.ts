import { expect, type Locator, type Page } from '@playwright/test'

/**
 * Opens an Element Plus select, given its combobox, and picks the option with exactly that name.
 * The click goes to the box around the combobox, since its placeholder covers the input.
 */
export async function chooseOption(select: Locator, option: string): Promise<void> {
  await select.locator('xpath=ancestor::div[contains(@class, "el-select__wrapper")][1]').click()
  await select.page().getByRole('option', { name: option, exact: true }).click()
}

/**
 * Ticks or unticks an Element Plus checkbox. Its native input is hidden, so the click goes to the
 * label that wraps it, the way a person toggles it.
 */
export async function setChecked(checkbox: Locator, checked: boolean): Promise<void> {
  if ((await checkbox.isChecked()) !== checked) {
    await checkbox.locator('xpath=ancestor::label[1]').click()
  }
  await (checked ? expect(checkbox).toBeChecked() : expect(checkbox).not.toBeChecked())
}

/** Types a `DD/MM/YYYY` date or `DD/MM/YYYY HH:mm` moment into an Element Plus picker. */
export async function fillPicker(input: Locator, value: string): Promise<void> {
  await input.fill(value)
  await input.press('Enter')
}

/**
 * The newest toast notification (top right) that shows `message`; an earlier one with the same text
 * may still be fading out.
 */
export function notification(page: Page, message: string | RegExp): Locator {
  return page.locator('.el-notification[role="alert"]').filter({ hasText: message }).last()
}

/** The confirmation box or dialog titled `title`. */
export function dialog(page: Page, title: string | RegExp): Locator {
  return page.getByRole('dialog', { name: title })
}

/** Confirms the confirmation box titled `title` with its `confirm` button. */
export async function confirm(page: Page, title: string, confirm: string): Promise<void> {
  const box = dialog(page, title)
  await box.getByRole('button', { name: confirm, exact: true }).click()
  await expect(box).toBeHidden()
}

/**
 * Turns an Element Plus switch on or off. Its native input is hidden and often unnamed, so the
 * click goes to the box around it, the way a person flips it.
 */
export async function setSwitch(toggle: Locator, on: boolean): Promise<void> {
  if ((await toggle.getAttribute('aria-checked')) !== String(on)) {
    await toggle.locator('xpath=ancestor::div[contains(@class, "el-switch")][1]').click()
  }
  await expect(toggle).toHaveAttribute('aria-checked', String(on))
}
