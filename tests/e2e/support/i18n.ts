import messages from '@locales/es.json' with { type: 'json' }

type MessagePath<T> = {
  [Key in keyof T & string]: T[Key] extends string
    ? Key
    : T[Key] extends Record<string, unknown>
      ? `${Key}.${MessagePath<T[Key]>}`
      : never
}[keyof T & string]

/** Dot-separated path to a message of the SPA's `es.json`; a key it no longer has fails the type check. */
export type MessageKey = MessagePath<typeof messages>

/** The values of the named placeholders of a message, such as `{name}`. */
export type MessageValues = Readonly<Record<string, string | number>>

const PLACEHOLDER = /\{\s*(?:'([^']*)'|(\w+))\s*\}/g

/**
 * The text the SPA shows for `key`, formatted as `t` of Vue I18n does: named placeholders take
 * `values`, quoted literals such as `{'@'}` are unquoted and, in a message with `|` choices, `count`
 * or else `n` picks the choice. Throws when a placeholder has no value, so a test never compares
 * against `{name}`.
 */
export function t(key: MessageKey, values: MessageValues = {}): string {
  const message = key
    .split('.')
    .reduce<unknown>((node, part) => (node as Record<string, unknown>)[part], messages) as string
  return choose(message, values).replace(
    PLACEHOLDER,
    (_placeholder, literal: string | undefined, name: string) => {
      if (literal !== undefined) return literal
      const value = values[name]
      if (value === undefined) throw new Error(`The message ${key} needs a value for {${name}}`)
      return String(value)
    },
  )
}

function choose(message: string, values: MessageValues): string {
  const choices = message.split(/\s*\|\s*/)
  if (choices.length === 1) return message
  const count = Math.abs(Number(values.count ?? values.n))
  const index = choices.length === 2 ? (count === 1 ? 0 : 1) : Math.min(count, 2)
  return choices[index] ?? message
}
