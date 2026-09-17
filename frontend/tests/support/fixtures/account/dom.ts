import { flushPromises } from '@vue/test-utils'

/** Buttons under `root` whose trimmed text is exactly `text`. */
export function buttonsByText(root: ParentNode, text: string): HTMLButtonElement[] {
  return [...root.querySelectorAll('button')].filter((button) => button.textContent.trim() === text)
}

/** The only button under `root` with text `text`; throws when missing or ambiguous. */
export function buttonByText(root: ParentNode, text: string): HTMLButtonElement {
  const matches = buttonsByText(root, text)
  if (matches.length !== 1) {
    throw new Error(`Expected one "${text}" button, found ${String(matches.length)}`)
  }
  return matches[0] as HTMLButtonElement
}

/** Clicks the element and lets Vue and pending requests settle. */
export async function click(element: Element): Promise<void> {
  ;(element as HTMLElement).click()
  await flushPromises()
}

/** Visible dialogs currently in the document (Element Plus hides closed ones with `v-show`). */
export function openDialogs(): HTMLElement[] {
  return [...document.querySelectorAll<HTMLElement>('.el-overlay')]
    .filter((overlay) => overlay.style.display !== 'none')
    .map((overlay) => overlay.querySelector<HTMLElement>('.el-dialog'))
    .filter((dialog): dialog is HTMLElement => dialog !== null)
}

/** The single open dialog whose header title is `title`. */
export function dialogByTitle(title: string): HTMLElement {
  const dialog = openDialogs().find(
    (candidate) => candidate.querySelector('.el-dialog__title')?.textContent.trim() === title,
  )
  if (!dialog) throw new Error(`No open dialog titled "${title}"`)
  return dialog
}

/** Text of every toast notification currently shown. */
export function notificationTexts(): string[] {
  return [...document.querySelectorAll('.el-notification')].map(
    (notification) => notification.textContent,
  )
}

/** Sets a native or Element Plus input's value and dispatches `input`. */
export async function fill(root: ParentNode, selector: string, value: string): Promise<void> {
  const input = root.querySelector<HTMLInputElement | HTMLTextAreaElement>(selector)
  if (!input) throw new Error(`No input matches ${selector}`)
  input.value = value
  input.dispatchEvent(new Event('input', { bubbles: true }))
  await flushPromises()
}
