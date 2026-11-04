import { fileURLToPath } from 'node:url'
import { expect, type Locator } from '@playwright/test'

/** The image the suite uploads through the thumbnail fields. */
const THUMBNAIL_PATH = fileURLToPath(new URL('../assets/thumbnail.png', import.meta.url))

/**
 * Picks the suite's image in the thumbnail field (`entities/file`) inside `scope`, the group named
 * «Imagen», through its hidden file input; the form uploads it when it is saved.
 */
export async function pickThumbnail(scope: Locator): Promise<void> {
  const field = scope.getByRole('group', { name: 'Imagen' })
  await field.locator('input.thumb__input').setInputFiles(THUMBNAIL_PATH)
  await expect(field.getByText('thumbnail.png')).toBeVisible()
}

/** Replaces the content of the rich text editor labelled `label` inside `scope` with `text`. */
export async function writeRichText(scope: Locator, label: string, text: string): Promise<void> {
  const editor = scope.locator('.ProseMirror').and(scope.getByLabel(label, { exact: true }))
  await editor.click()
  await editor.press('ControlOrMeta+a')
  await editor.pressSequentially(text)
  await expect(editor).toContainText(text)
}
