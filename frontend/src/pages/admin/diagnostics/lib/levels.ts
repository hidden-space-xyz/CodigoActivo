import type { TranslationKey } from '@/shared/i18n'

import type { DiagnosticLevel } from '../model/types'

/** Element Plus tag colour a level is shown with. */
type LevelTagType = 'info' | 'primary' | 'warning' | 'danger'

/** Every level, from the least to the most severe, in the order the level filter lists them. */
export const DIAGNOSTIC_LEVELS: readonly DiagnosticLevel[] = [
  'Trace',
  'Debug',
  'Information',
  'Warning',
  'Error',
  'Critical',
]

const LEVEL_LABEL_KEYS: Record<DiagnosticLevel, TranslationKey> = {
  Trace: 'pages.admin.diagnostics.levels.Trace',
  Debug: 'pages.admin.diagnostics.levels.Debug',
  Information: 'pages.admin.diagnostics.levels.Information',
  Warning: 'pages.admin.diagnostics.levels.Warning',
  Error: 'pages.admin.diagnostics.levels.Error',
  Critical: 'pages.admin.diagnostics.levels.Critical',
}

const LEVEL_TAG_TYPES: Record<DiagnosticLevel, LevelTagType> = {
  Trace: 'info',
  Debug: 'info',
  Information: 'primary',
  Warning: 'warning',
  Error: 'danger',
  Critical: 'danger',
}

/** Translation key of the name of a level. */
export function levelLabelKey(level: DiagnosticLevel): TranslationKey {
  return LEVEL_LABEL_KEYS[level]
}

/**
 * Tag colour of a level: failures in red, warnings in amber, information in neutral grey (the
 * theme's `primary` tag) and trace or debug in blue (`info`).
 */
export function levelTagType(level: DiagnosticLevel): LevelTagType {
  return LEVEL_TAG_TYPES[level]
}
