import type { Locator, Page } from '@playwright/test'

import { DataTable } from '../../ui/data-table'
import { saveDownload } from '../../ui/download'
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
 * The legal archive, at `/admin/blocked-data`: the lists of consultations and of holds every
 * administrator sees, the privacy officer's «Abrir consulta» dialog and the panel «Consulta
 * abierta» that looks accounts up, downloads an extract about one of them and holds its history.
 */
export class BlockedDataPage {
  /** The list of consultations. */
  readonly consultations: DataTable

  /** The list of holds that keep the history of an account while a claim is open. */
  readonly holds: DataTable

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
    this.holds = new DataTable(page, page.getByTestId('blocked-data-holds'))
    this.openConsultation = page.getByTestId('blocked-data-open')
    this.dialog = page.getByTestId('blocked-data-dialog')
    this.panel = page.getByTestId('blocked-data-panel')
    this.candidates = this.panel.getByTestId('blocked-data-panel-candidates')
  }

  /** Opens the page and waits for the list of consultations. */
  async open(): Promise<void> {
    await new AdminShell(this.page).open('admin-blocked-data')
    await this.consultations.expectSettled()
  }

  /** Looks `text` up in the open consultation. */
  async search(text: string): Promise<void> {
    await this.panel.getByTestId('blocked-data-panel-query').fill(text)
    await this.panel.getByTestId('blocked-data-panel-search').click()
  }

  /** Downloads the extract about the picked account and reads it. */
  async downloadExtract(): Promise<DownloadedExtract> {
    const button = this.panel.getByTestId('blocked-data-panel-download')
    const file = await saveDownload(this.page, () => button.click())
    return { fileName: file.name, extract: JSON.parse(file.content.toString('utf8')) as Extract }
  }
}
