# End-to-end suite guidance

How to run the Playwright suite is in [CONTRIBUTING.md](../../CONTRIBUTING.md#end-to-end-tests).
`npm run check` is the complete gate: TypeScript, ESLint (type-checked rules, eslint-plugin-playwright, JSDoc
on the library), Knip and Prettier enforce further conventions that this file leaves out.

## Layout

- `support/` is the library: `config/` holds the stack's addresses, accounts and time zone, `api/` talks to
  the API as the SPA does (CSRF, cookies, typed payloads), `mail/` reads Mailpit, `data/` builds unique people,
  titles and Madrid dates, `fixtures/` defines `test` and its actors, `ui/` wraps Element Plus controls and
  admin tables, `pages/` holds the page objects and `assets/` the uploaded image.
- `specs/` has one folder per area, plus `setup/`, the project every other project depends on: `auth/`
  (registration, login, recovery and who reaches the administration), `account/` («Mi cuenta»), `public/` (what
  visitors see), `signups/` (signing up for activities) and `admin-content/`, `admin-events/`,
  `admin-inventory/` and `admin-people/` (the administration).
- Request and response types come from the frontend's generated models (`@contract/*`), so a contract change
  breaks the type check; catalog identifiers are the stable ones declared in `support/api/catalogs.ts`.
- `support/i18n.ts` formats the SPA's messages (`@locales/es.json`) with `t()`, so a renamed key breaks the
  type check too.

## What belongs here

The suite proves what only the whole stack can: a person going through the SPA, the API, the database and the
mail. A rule of one layer is tested in that layer (`backend/tests`, `frontend/tests`), here at most once, as
part of a journey.

- A test title states a behavior and the test asserts every clause of it.
- A behavior has one test: code the pages share, such as the access guard or the search box, is not repeated
  page by page.
- What a page reports is also checked at its source: through the API or in Mailpit.
- Text that only copies `es.json` proves nothing: assert what the text depends on, such as data or state.

## Rules

- Locate elements by their `data-testid` with `getByTestId`; ESLint rejects the other `getBy*` locators.
  Use CSS and XPath only for what carries no test id (Element Plus internals, table rows, toasts and
  confirmation boxes), and keep them in `support/`.
- Compare visible text with `t()` or with the data the test created, never with a literal, and filter lists
  by that data. The exception is the catalog names the API seeds, which come from the `*_LABELS` of
  `support/api/catalogs.ts`.
- Each test creates what it needs through the API, with unique names, and asserts only on its own data, never
  on totals or on the order of lists other tests also fill. Spec files run in parallel with each other.
- Worker actors (`editor`, `member`, `participant`, `guardian`), `admin` and the shared `initialAdmin` and
  `privacyOfficer` stay signed in across tests. A test that logs out, changes credentials, the second factor
  or the household, or deletes an account uses `accounts.actor(...)`. Shared actors never sign in through the
  login pages, since a new password step invalidates the open one.
- The API limits each account to 600 requests and 30 uploads a minute, so `admin` changes every few tests and
  the `seed` fixture, which creates content through the API, shares one image per worker.
- The featured event and news item, the legal policies and the designation for data protection exist once per
  site: besides `setup`, which designates `privacyOfficer`, only `specs/public/featured.spec.ts`,
  `specs/admin-content/legal-policies.spec.ts` and `specs/admin-people/data-protection.spec.ts` change them, and
  only that last spec signs in as `privacyOfficer`.
- Selects share their option ids and a closing list stays visible, so open selects with `chooseOption`, which
  waits for the previous list to close.
- Codes and links come from Mailpit through `Mailbox`, and resent ones through `waitForResent`, which waits
  out the one-minute resend cooldown; authenticator codes come from `otpauth`, never from hand-written code.
