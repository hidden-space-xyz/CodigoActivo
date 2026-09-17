import { flushPromises } from '@vue/test-utils'

/** Visible text of an element with collapsed whitespace. */
export function textOf(element: Element | null | undefined): string {
  return (element?.textContent ?? '').replace(/\s+/g, ' ').trim()
}

/** First `<button>` under `root` whose visible text is exactly `text`; throws when missing. */
export function buttonByText(text: string, root: ParentNode = document.body): HTMLButtonElement {
  const button = [...root.querySelectorAll('button')].find((item) => textOf(item) === text)
  if (!button) throw new Error(`Missing button "${text}"`)
  return button
}

/** Whether a button with exactly `text` exists under `root`. */
export function hasButton(text: string, root: ParentNode = document.body): boolean {
  return [...root.querySelectorAll('button')].some((item) => textOf(item) === text)
}

/** Clicks an element and waits for the resulting promises (requests, re-renders) to settle. */
export async function clickElement(element: HTMLElement): Promise<void> {
  element.click()
  await flushPromises()
}

/**
 * Open Element Plus dialog whose header title is `title`, or `undefined` when it is closed or was
 * never rendered.
 */
export function openDialog(title: string): HTMLElement | undefined {
  return [...document.body.querySelectorAll<HTMLElement>('.el-dialog')].find((dialog) => {
    const overlay = dialog.closest<HTMLElement>('.el-overlay')
    return (
      textOf(dialog.querySelector('.el-dialog__title')) === title &&
      overlay?.style.display !== 'none'
    )
  })
}

/** Title and message of every Element Plus notification currently shown. */
export function notifications(): { title: string; message: string }[] {
  return [...document.body.querySelectorAll('.el-notification')].map((item) => ({
    title: textOf(item.querySelector('.el-notification__title')),
    message: textOf(item.querySelector('.el-notification__content')),
  }))
}
