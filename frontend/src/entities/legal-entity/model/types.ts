/**
 * Legal identity of the association that runs the site, the data controller named by the privacy
 * policy. Each deployment configures it through the `LEGAL_*` settings; a value is empty only
 * outside production.
 */
export interface LegalEntity {
  readonly name: string
  /** Tax identification number (NIF). */
  readonly taxId: string
  readonly address: string
  /** Register of associations and the entry number in it. */
  readonly registry: string
}
