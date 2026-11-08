# Performance suite guidance

These are the conventions of the k6 suite. How to run it and what each profile proves is in
[CONTRIBUTING.md](../../CONTRIBUTING.md#performance-tests).

## Layout

- `k6/main.js` turns the profile into k6 scenarios, prepares accounts and data in `setup` and runs each
  iteration.
- `k6/scenarios/` has one file per kind of visit; `k6/lib/pages.js` holds the requests of the pages several
  visits open.
- `k6/lib/profiles.js` holds rates, account pools and gates, `catalog.js` the endpoints and their budgets,
  `thresholds.js` the objective and the gates, and `summary.js` the report.
- `k6/lib/http.js` holds `Session`, the simulated browser; `accounts.js`, `mail.js` and `fixtures.js` serve setup
  and the sign-in flows; `pool.js` deals accounts to iterations.
- `k6/lib/config.js` reads the environment, `data.js` holds catalog ids and shared values, and `windows.js`
  tells setup from the measured windows and counts arrivals.
- `volume.sql` fills the database between the demo seed and setup.

## Rules

- A scenario sends what the SPA sends: the same paths and query parameters, the images a page loads and the
  refetches after a change. A request the SPA never sends has no place here, not even to clean up.
- Every request goes through `Session`, named `METHOD /api/route/{param}`, plus ` [anonymous]` for a GET without
  a session. An endpoint a scenario requests is catalogued with the budget of its kind, or k6 warns about it; a
  catalogued endpoint nobody requests fails the run.
- `expect` lists the statuses a request may answer. Anything else stops the run during setup, so no scenario
  runs on broken data. During the scenarios it counts as a failed request and the iteration goes on, so code
  that reads a response survives a null body.
- A rate belongs to a profile and a budget to a kind of endpoint: a run that misses them is a finding to fix in
  the API, or a reasoned change of the figure, never a looser threshold to get a pass.
- Keep scenarios within `ApiRateLimitOptions` and `EmailGuardOptions`. A pool must be large enough that an
  account never runs two iterations at once, and never signs in again within the minute `TwoFactorOptions`
  makes it wait for a new code.
- `volume.sql` writes the tables directly, so a schema change that breaks it stops the run. It only adds rows
  and leaves the demo data alone, because setup and the scenarios read it.
- Format with the Prettier configuration of `tests/e2e/`, from that folder:
  `npx prettier --config .prettierrc.json --write ../performance/k6`. Exported functions carry a JSDoc line;
  code carries no other comments.
