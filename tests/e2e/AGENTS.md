# End-to-end suite guidance

These are the conventions of the Playwright suite. How to run it is in
[CONTRIBUTING.md](../../CONTRIBUTING.md#end-to-end-tests). `npm run check` is the complete gate: TypeScript,
ESLint (type-checked rules, eslint-plugin-playwright, JSDoc on the library), Knip and Prettier enforce further
conventions that this file leaves out.

## Layout

- `support/` is the library: `api/` talks to the API as the SPA does (CSRF, cookies, typed payloads),
  `mail/` reads Mailpit, `data/` builds unique people, titles and Madrid dates, `fixtures/` defines `test`
  and its actors, `ui/` wraps Element Plus controls and admin tables, and `pages/` holds the page objects.
- `specs/` has one folder per area and `setup/`, the project every other one depends on.
- Request and response types come from the frontend's generated models (`@contract/*`), so a contract change
  breaks the type check; catalog identifiers are the stable ones in `support/api/catalogs.ts`.

## Rules

- Specs use accessible locators only; CSS and XPath selectors live in `support/`.
- Each test creates what it needs through the API, with unique names, and asserts only on its own data, never
  on totals or on the order of lists other tests also fill. Tests run in parallel with each other.
- Worker actors (`admin`, `editor`, `member`, `participant`, `guardian`) and the shared `initialAdmin` and
  `privacyOfficer` stay signed in for every test of a worker: a test that logs out, changes credentials, the
  second factor or the household, or deletes an account uses `accounts.actor(...)`. Shared actors never sign
  in through the login pages, since a new password step invalidates the open one.
- The featured event and news item and the legal policies exist once per site: only
  `specs/public/featured.spec.ts` and `specs/admin-content/legal-policies.spec.ts` change them, serially.
- Codes and links come from Mailpit through `Mailbox`; authenticator codes come from `otpauth`, never from
  code of our own.
