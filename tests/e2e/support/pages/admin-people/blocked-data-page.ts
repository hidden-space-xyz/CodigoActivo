import { readFile } from 'node:fs/promises'
import type { Locator, Page } from '@playwright/test'

import { DataTable } from '../../ui/data-table'
import { AdminShell } from '../admin-shell'

/** One history entry of a downloaded extract. */
interface ExtractEntry {
  readonly tipo: string
  readonly fecha: string
}

/** The extract of blocked data the panel downloads, with the Spanish keys the file uses. */
interface Extract {
  readonly destinatario: string
  readonly referencia: string
  readonly cuenta: string
  readonly entradas: readonly ExtractEntry[]
}

/** A downloaded extract: the name the browser saves it as and its content. */
interface DownloadedExtract {
  readonly fileName: string
  readonly extract: Extract
}

/**
 * The legal archive, at `/admin/blocked-data`: the list of consultations every administrator
 * sees, the privacy officer's «Abrir consulta» dialog and the panel «Consulta abierta» that looks
 * accounts up and downloads an extract about one of them.
 */
export class BlockedDataPage {
  /** The list of consultations. */
  readonly consultations: DataTable

  /** The button that starts a consultation, shown only to the privacy officer. */
  readonly openConsultation: Locator

  /** The dialog that opens a consultation in two steps. */
  readonly dialog: Locator

  /** The panel of the open consultation. */
  readonly panel: Locator

  /** The accounts the last search found, as radios. */
  readonly candidates: Locator

  constructor(private readonly page: Page) {
    this.consultations = new DataTable(page)
    this.openConsultation = page.getByRole('button', { name: 'Abrir consulta', exact: true })
    this.dialog = page.getByRole('dialog', { name: 'Abrir una consulta de datos bloqueados' })
    this.panel = page.getByRole('region', { name: 'Consulta abierta' })
    this.candidates = this.panel.getByRole('radiogroup', { name: 'Cuentas encontradas' })
  }

  /** Opens the page and waits for the list of consultations. */
  async open(): Promise<void> {
    await new AdminShell(this.page).open('Archivo legal')
    await this.consultations.expectSettled()
  }

  /** Looks `text` up in the open consultation. */
  async search(text: string): Promise<void> {
    await this.panel.getByLabel('Buscar una cuenta').fill(text)
    await this.panel.getByRole('button', { name: 'Buscar', exact: true }).click()
  }

  /** Downloads the extract about the picked account and reads it. */
  async downloadExtract(): Promise<DownloadedExtract> {
    const [download] = await Promise.all([
      this.page.waitForEvent('download'),
      this.panel.getByRole('button', { name: 'Descargar extracto' }).click(),
    ])
    const extract = JSON.parse(await readFile(await download.path(), 'utf8')) as Extract
    return { fileName: download.suggestedFilename(), extract }
  }
}
