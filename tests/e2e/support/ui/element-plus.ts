import { expect, type Locator, type Page } from '@playwright/test'

/**
 * Opens an Element Plus select, given the element that carries its test id, and picks the option
 * whose test id is `option-{value}`. The option list is teleported, so it is searched in the page.
 */
export async function chooseOption(select: Locator, value: string): Promise<void> {
  await select.click()
  await select.page().getByTestId(`option-${value}`).filter({ visible: true }).click()
}

/**
 * Ticks or unticks an Element Plus checkbox or radio, given the element that carries its test id.
 * Its native input is hidden, so the click goes to that element, the way a person toggles it.
 */
export async function setChecked(checkbox: Locator, checked: boolean): Promise<void> {
  const input = checkbox.locator('input')
  if ((await input.isChecked()) !== checked) {
    await checkbox.click()
  }
  await (checked ? expect(input).toBeChecked() : expect(input).not.toBeChecked())
}

/** Expects the Element Plus checkbox or radio that carries a test id to be ticked or not. */
export async function expectChecked(checkbox: Locator, checked: boolean): Promise<void> {
  const input = checkbox.locator('input')
  await (checked ? expect(input).toBeChecked() : expect(input).not.toBeChecked())
}

/**
 * Types a `DD/MM/YYYY` date or `DD/MM/YYYY HH:mm` moment into an Element Plus picker, given the
 * field that holds it: a test id on the picker itself would land on its teleported panel.
 */
export async function fillPicker(field: Locator, value: string): Promise<void> {
  const input = field.locator('input').first()
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

/** The open confirmation box (`useActionConfirm` or `useDeleteConfirm`). */
export function messageBox(page: Page): Locator {
  return page.locator('.el-message-box').filter({ visible: true })
}

/** Accepts the open confirmation box with its confirm button and waits for it to close. */
export async function confirm(page: Page): Promise<void> {
  const box = messageBox(page)
  await box.locator('.el-message-box__btns .el-button--primary').click()
  await expect(box).toBeHidden()
}

/**
 * Turns an Element Plus switch on or off, given the element that carries its test id. Its native
 * input is hidden, so the click goes to that element, the way a person flips it.
 */
export async function setSwitch(toggle: Locator, on: boolean): Promise<void> {
  const input = toggle.locator('input')
  if ((await input.getAttribute('aria-checked')) !== String(on)) {
    await toggle.click()
  }
  await expect(input).toHaveAttribute('aria-checked', String(on))
}
