import { defineComponent, h } from 'vue'
import { flushPromises, type VueWrapper } from '@vue/test-utils'
import { ElDialog } from 'element-plus'
import { vi } from 'vitest'

import type { QueryClient } from '@tanstack/vue-query'

import { i18n } from '@/shared/i18n'

import { createTestQueryClient, renderWithProviders } from '../../render'

/** All buttons under `root` whose accessible name (aria-label) or text equals `label`. */
export function findButtons(label: string, root: ParentNode = document.body): HTMLButtonElement[] {
  return Array.from(root.querySelectorAll('button')).filter(
    (button) => button.getAttribute('aria-label') === label || button.textContent.trim() === label,
  )
}

/** First button under `root` labelled `label`; throws when there is none. */
export function findButton(label: string, root: ParentNode = document.body): HTMLButtonElement {
  const [button] = findButtons(label, root)
  if (!button) throw new Error(`Button "${label}" not found`)
  return button
}

/** Clicks an element and waits for pending promises. */
export async function click(element: HTMLElement): Promise<void> {
  element.click()
  await flushPromises()
}

/** Open Element Plus dialog whose title is `title`; throws when it is not rendered and visible. */
export function openDialog(title: string): HTMLElement {
  const dialog = Array.from(document.body.querySelectorAll<HTMLElement>('.el-dialog')).find(
    (candidate) =>
      candidate.querySelector('.el-dialog__title')?.textContent.trim() === title &&
      isVisible(candidate),
  )
  if (!dialog) throw new Error(`Dialog "${title}" is not open`)
  return dialog
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

/** Whether a visible dialog titled `title` exists. */
export function isDialogOpen(title: string): boolean {
  try {
    openDialog(title)
    return true
  } catch {
    return false
  }
}

function isVisible(element: HTMLElement): boolean {
  for (let node: HTMLElement | null = element; node; node = node.parentElement) {
    if (node.style.display === 'none') return false
  }
  return true
}

/** Sets the value of the text input or textarea matching `selector` as a user would. */
export async function typeInto(selector: string, value: string): Promise<void> {
  const input = document.body.querySelector<HTMLInputElement | HTMLTextAreaElement>(selector)
  if (!input) throw new Error(`Input "${selector}" not found`)
  input.value = value
  input.dispatchEvent(new Event('input', { bubbles: true }))
  await flushPromises()
}

/** Current value of the input matching `selector`. */
export function inputValue(selector: string): string {
  const input = document.body.querySelector<HTMLInputElement | HTMLTextAreaElement>(selector)
  if (!input) throw new Error(`Input "${selector}" not found`)
  return input.value
}

/** Picks `files` in a native file input, dispatching `change`. */
export async function pickFiles(input: HTMLInputElement, files: File[]): Promise<void> {
  Object.defineProperty(input, 'files', { configurable: true, value: files })
  input.dispatchEvent(new Event('change', { bubbles: true }))
  await flushPromises()
}

/** Most recent message box (confirmation) element; throws when none was opened. */
export function messageBox(): HTMLElement {
  const box = Array.from(document.body.querySelectorAll<HTMLElement>('.el-message-box')).at(-1)
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
  await flushPromises()
}

/** Cancels the open confirmation message box. */
export async function cancelMessageBox(): Promise<void> {
  const [cancel] = Array.from(
    messageBox().querySelectorAll<HTMLButtonElement>('.el-message-box__btns .el-button'),
  )
  if (!cancel) throw new Error('Message box has no cancel button')
  await click(cancel)
  await flushPromises()
}

/** Text of every notification toast currently in the document. */
export function notificationTexts(): string[] {
  return Array.from(document.body.querySelectorAll('.el-notification')).map((node) =>
    node.textContent.trim(),
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

/** Translates a pluralized message exactly as components do with `$t(key, values, count)`. */
export function tp(key: string, count: number, values: Record<string, unknown> = {}): string {
  const translate = i18n.global.t as (
    key: string,
    values: Record<string, unknown>,
    plural: number,
  ) => string
  return translate(key, values, count)
}

/** Search parameters of a request URL as a plain object. */
export function queryOf(url: string): Record<string, string> {
  return Object.fromEntries(new URL(url).searchParams.entries())
}

/**
 * Runs `composable` inside a mounted component with the app providers and returns its result, the
 * wrapper and the query client (for spying on invalidations).
 */
export async function withSetup<T>(
  composable: () => T,
  queryClient: QueryClient = createTestQueryClient(),
) {
  let result: T | undefined
  const Host = defineComponent({
    setup() {
      result = composable()
      return () => h('div')
    },
  })
  const { wrapper } = await renderWithProviders(Host, { queryClient })
  if (result === undefined) throw new Error('Composable did not run')
  return { result, wrapper, queryClient }
}
