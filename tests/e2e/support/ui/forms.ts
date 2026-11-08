import { fileURLToPath } from 'node:url'
import { expect, type Locator } from '@playwright/test'

/** The name of the image the suite uploads through the thumbnail fields. */
const THUMBNAIL_NAME = 'thumbnail.png'

/** The image the suite uploads through the thumbnail fields. */
const THUMBNAIL_PATH = fileURLToPath(new URL(`../assets/${THUMBNAIL_NAME}`, import.meta.url))

/**
 * Picks the suite's image in the thumbnail field (`entities/file`) inside `scope` through its
 * hidden file input; the form uploads it when it is saved.
 */
export async function pickThumbnail(scope: Locator): Promise<void> {
  const field = scope.getByTestId('thumbnail-field')
  await field.getByTestId('thumbnail-input').setInputFiles(THUMBNAIL_PATH)
  await expect(field.getByTestId('thumbnail-file-name')).toHaveText(THUMBNAIL_NAME)
}

/**
 * Replaces the content of the rich text editor (`shared/ui/rich-text-editor`) that carries a test
 * id with `text`.
 */
export async function writeRichText(field: Locator, text: string): Promise<void> {
  const editor = field.locator('.ProseMirror')
  await editor.click()
  await editor.press('ControlOrMeta+a')
  await editor.pressSequentially(text)
  await expect(editor).toContainText(text)
}
