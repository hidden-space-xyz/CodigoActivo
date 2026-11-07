import { readFile } from 'node:fs/promises'
import type { Page } from '@playwright/test'

/** A file the browser saved, with the name the page gave it and its bytes. */
export interface SavedFile {
  readonly name: string
  readonly content: Buffer
}

/** Runs `trigger` and returns the file the page downloads because of it. */
export async function saveDownload(page: Page, trigger: () => Promise<void>): Promise<SavedFile> {
  const [download] = await Promise.all([page.waitForEvent('download'), trigger()])
  const content = await readFile(await download.path())
  return { name: download.suggestedFilename(), content }
}
