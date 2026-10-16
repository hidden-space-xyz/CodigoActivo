import { flushPromises, DOMWrapper, type VueWrapper } from '@vue/test-utils'
import { ElDialog } from 'element-plus'
import { vi } from 'vitest'

import { i18n } from '@/shared/i18n'

/** Visible text of an element with collapsed whitespace. */
export function textOf(element: Element | null | undefined): string {
  return (element?.textContent ?? '').replace(/\s+/g, ' ').trim()
}

/** Buttons under `root` whose visible text or accessible name (aria-label) equals `label`. */
export function findButtons(label: string, root: ParentNode = document.body): HTMLButtonElement[] {
  return [...root.querySelectorAll('button')].filter(
    (button) => textOf(button) === label || button.getAttribute('aria-label') === label,
  )
}

/** First button under `root` labelled `label`; throws when there is none. */
export function findButton(label: string, root: ParentNode = document.body): HTMLButtonElement {
  const [button] = findButtons(label, root)
  if (!button) throw new Error(`Button "${label}" not found`)
  return button
}

/** Clicks the first button under `root` labelled `label`, without waiting for anything. */
export function clickButton(label: string, root: ParentNode = document.body): void {
  findButton(label, root).click()
}

/** Clicks an element and waits for the resulting promises (requests, re-renders) to settle. */
export async function click(element: Element): Promise<void> {
  ;(element as HTMLElement).click()
  await flushPromises()
}

/** Finds an element anywhere in `document.body` (teleported dialogs included). */
export function bodyFind<E extends Element = HTMLElement>(selector: string): DOMWrapper<E> {
  const element = document.body.querySelector<E>(selector)
  if (!element) throw new Error(`Element not found: ${selector}`)
  return new DOMWrapper(element)
}

function isVisible(element: HTMLElement): boolean {
  for (let node: HTMLElement | null = element; node; node = node.parentElement) {
    if (node.style.display === 'none') return false
  }
  return true
}

/** Visible Element Plus dialogs currently in the document. */
export function openDialogs(): HTMLElement[] {
  return [...document.body.querySelectorAll<HTMLElement>('.el-dialog')].filter(isVisible)
}

/** Open dialog whose header title is `title`, or `undefined` when it is closed or not rendered. */
export function findDialog(title: string): HTMLElement | undefined {
  return openDialogs().find((dialog) => textOf(dialog.querySelector('.el-dialog__title')) === title)
}

/** Open dialog whose header title is `title`; throws when it is not open. */
export function openDialog(title: string): HTMLElement {
  const dialog = findDialog(title)
  if (!dialog) throw new Error(`Dialog "${title}" is not open`)
  return dialog
}

/** Whether a visible dialog titled `title` exists. */
export function isDialogOpen(title: string): boolean {
  return findDialog(title) !== undefined
}

/**
 * Dismisses the dialog titled `title` as its close (X) button or Escape would. Element Plus only
 * emits `update:modelValue` from its leave-transition hook, which never runs under the stubbed
 * transitions of the test renderer, so the event is emitted on the dialog component directly.
 */
export async function dismissDialog(wrapper: VueWrapper, title: string): Promise<void> {
  const dialog = wrapper
    .findAllComponents(ElDialog)
    .find((candidate) => candidate.props('title') === title)
  if (!dialog) throw new Error(`Dialog "${title}" not found`)
  dialog.vm.$emit('update:modelValue', false)
  await flushPromises()
}

/** Sets the value of the input or textarea matching `selector` as a user would. */
export async function typeInto(
  selector: string,
  value: string,
  root: ParentNode = document.body,
): Promise<void> {
  const input = root.querySelector<HTMLInputElement | HTMLTextAreaElement>(selector)
  if (!input) throw new Error(`Input "${selector}" not found`)
  input.value = value
  input.dispatchEvent(new Event('input', { bubbles: true }))
  await flushPromises()
}

/** Current value of the input matching `selector`. */
export function inputValue(selector: string, root: ParentNode = document.body): string {
  const input = root.querySelector<HTMLInputElement | HTMLTextAreaElement>(selector)
  if (!input) throw new Error(`Input "${selector}" not found`)
  return input.value
}

/** Picks `files` in a native file input, dispatching `change`. */
export async function pickFiles(input: HTMLInputElement, files: File[]): Promise<void> {
  Object.defineProperty(input, 'files', { configurable: true, value: files })
  input.dispatchEvent(new Event('change', { bubbles: true }))
  await flushPromises()
}

/** Picks `file` in the first thumbnail file input inside `root`, without waiting. */
export function pickThumbnail(file: File, root: ParentNode = document.body): void {
  const input = root.querySelector<HTMLInputElement>('.thumb input[type="file"]')
  if (!input) throw new Error('File input not found')
  Object.defineProperty(input, 'files', { configurable: true, value: [file] })
  input.dispatchEvent(new Event('change'))
}

/** Most recent message box (confirmation) element; throws when none was opened. */
export function messageBox(): HTMLElement {
  const box = [...document.body.querySelectorAll<HTMLElement>('.el-message-box')].at(-1)
  if (!box) throw new Error('No message box is open')
  return box
}

/** Accepts the open confirmation message box. */
export async function acceptMessageBox(): Promise<void> {
  const accept = messageBox().querySelector<HTMLButtonElement>(
    '.el-message-box__btns .el-button--primary',
  )
  if (!accept) throw new Error('Message box has no accept button')
  await click(accept)
}

/** Cancels the open confirmation message box. */
export async function cancelMessageBox(): Promise<void> {
  const [cancel] = messageBox().querySelectorAll<HTMLButtonElement>(
    '.el-message-box__btns .el-button',
  )
  if (!cancel) throw new Error('Message box has no cancel button')
  await click(cancel)
}

/**
 * Opens the "more actions" menu of a table row and resolves with the rendered menu
 * (ul[role=menu] labelled by the trigger).
 */
export async function openRowMenu(row: Element): Promise<HTMLElement> {
  const trigger = row.querySelector<HTMLElement>('.row-actions__more')
  if (!trigger) throw new Error('Row has no "more actions" menu')
  const findMenu = () =>
    document.body.querySelector<HTMLElement>(`ul[role="menu"][aria-labelledby="${trigger.id}"]`)
  await vi.waitFor(
    () => {
      const previous = findMenu()
      if (trigger.getAttribute('aria-expanded') === 'true' || (previous && isVisible(previous))) {
        throw new Error('Row menu is still closing')
      }
    },
    { timeout: 3000 },
  )
  await click(trigger)
  let menu: HTMLElement | null = null
  await vi.waitFor(
    () => {
      menu = findMenu()
      if (!menu || !isVisible(menu)) throw new Error('Row menu did not open')
    },
    { timeout: 3000 },
  )
  return menu as unknown as HTMLElement
}

function rowMenuItems(menu: ParentNode): HTMLElement[] {
  return [...menu.querySelectorAll<HTMLElement>('li[role="menuitem"]')]
}

function rowMenuItemLabel(item: Element): string {
  return textOf(item.querySelector('.row-actions__item-label'))
}

/** Labels of the items of an open row menu, in order. */
export function rowMenuLabels(menu: ParentNode): string[] {
  return rowMenuItems(menu).map(rowMenuItemLabel)
}

/** Item (li[role=menuitem]) of an open row menu labelled `label`; throws when there is none. */
export function rowMenuItem(menu: ParentNode, label: string): HTMLElement {
  const item = rowMenuItems(menu).find((candidate) => rowMenuItemLabel(candidate) === label)
  if (!item) {
    const labels = JSON.stringify(rowMenuLabels(menu))
    throw new Error(`Row menu item "${label}" not found; got ${labels}`)
  }
  return item
}

/** Title and message of every notification toast currently shown. */
export function notifications(): { title: string; message: string }[] {
  return [...document.body.querySelectorAll('.el-notification')].map((item) => ({
    title: textOf(item.querySelector('.el-notification__title')),
    message: textOf(item.querySelector('.el-notification__content')),
  }))
}

/** Text of every notification toast currently shown. */
export function notificationTexts(): string[] {
  return [...document.body.querySelectorAll('.el-notification')].map((node) =>
    (node.textContent ?? '').trim(),
  )
}

/** Waits until a notification containing `text` appears. */
export async function expectNotification(text: string): Promise<void> {
  await vi.waitFor(() => {
    if (!notificationTexts().some((message) => message.includes(text))) {
      throw new Error(
        `Notification "${text}" not shown; got ${JSON.stringify(notificationTexts())}`,
      )
    }
  })
}

/** Reads a component prop without the narrow typing of `wrapper.props(name)`. */
export function propOf(wrapper: { props: () => unknown }, name: string): unknown {
  return (wrapper.props() as Record<string, unknown>)[name]
}

/** Search parameters of a request URL as a plain object. */
export function queryOf(url: string): Record<string, string> {
  return Object.fromEntries(new URL(url).searchParams.entries())
}

/** Translates a pluralized message exactly as components do with `$t(key, values, count)`. */
export function tp(key: string, count: number, values: Record<string, unknown> = {}): string {
  const translate = i18n.global.t as (
    key: string,
    values: Record<string, unknown>,
    plural: number,
  ) => string
  return translate(key, values, count)
}

/** Stubs `document.fonts.ready`, which jsdom does not implement. Returns a cleanup function. */
export function stubDocumentFonts(): () => void {
  Object.defineProperty(document, 'fonts', {
    configurable: true,
    value: { ready: Promise.resolve() },
  })
  return () => {
    Reflect.deleteProperty(document, 'fonts')
  }
}
