# Security Policy

## Reporting a vulnerability

Report security issues privately through **Security → Advisories → Report a vulnerability** in the GitHub
repository. Do not open a public issue. Include the affected version or commit, impact, reproduction steps
and any suggested mitigation. The project supports the latest deployed version from `master`.

## Security model

The application assumes a same-origin deployment behind a trusted TLS reverse proxy. The browser reaches
nginx, which serves the SPA and forwards `/api` to the private API container. Cross-origin API use and JWT
authentication are not supported.

### Authentication and authorization

- Authentication uses an ASP.NET Core session cookie: `HttpOnly`, `SameSite=Lax`, non-sliding, expiring after
  30 days by default. It is a browser-session cookie unless the user ticks «Mantener la sesión iniciada en este
  dispositivo» on the password step (`keepSignedIn`), which makes it persistent; the challenge cookie carries
  that choice to the second factor, and a claims refresh keeps it. The session, challenge and CSRF cookies are
  `Secure` in every environment except Development and use a `__Host-` name in Production. The ticket is not self-sufficient: completing the second factor also writes a
  `user_sessions` row whose id travels in the ticket's `sid` claim, dropping that user's already-expired rows;
  a background worker additionally deletes every expired row every hour. The row carries the expiry that
  decides access: refreshing the claims re-issues the cookie with a later expiry, so a cookie can outlive its
  row, and the ticket is rejected as soon as the row is missing or expired.
- Every authenticated request revalidates account status, password lock, password fingerprint, administrator
  flag and the `sid` row in one query, and rejects the ticket when any of them is missing or expired.
  Blocking or demoting a user takes effect on existing sessions; changing or resetting the password
  invalidates them through the fingerprint, and the change then deletes that user's session rows. When the API answers `AuthenticationRequired` or
  `CurrentUserNotFound` to a signed-in SPA, or right after the user changes their password, the SPA drops its
  cached session and data and sends protected pages back to login.
- `POST /api/auth/logout` deletes the row of the presented session before clearing the cookies, so a copy of
  that cookie stops working immediately instead of lasting until its expiry. It is idempotent: it asks for no
  valid session, only the CSRF token, and always answers 204 after clearing the session and challenge
  cookies, including when the row is already gone or its deletion fails, which is logged. Deleting an account
  removes its rows by cascade.
- Every endpoint requires an authenticated session unless it is marked `[AllowAnonymous]` (fallback
  authorization policy).
- Authorization is a boolean administrator flag, not a role system. `[AllowOnlyAdmin]` protects
  administration endpoints; `[AllowOnlySelf]` accepts the target user, that user's guardian or an
  administrator, except on `PUT /api/users/{id}` and `POST /api/users/{id}/children`
  (`AdministratorsAllowed = false`): personal data, consents and dependents change only through their
  holder or guardian, never an administrator. Catalog values
  such as `UserType` are not authorization roles. Activity leadership grants one read, decided on every
  request from the session user's own signups: `GET /api/events/{eventId}/leader-roster` returns only
  the event's activities that have not ended and in which the caller holds a confirmed leader signup,
  each with its confirmed attendees by role. Anyone else, administrators and guardians of a leading dependent
  included, gets an empty list; the client sends no activity id. Each attendee carries only names and
  signup time, plus their own email and primary phone (independent accounts) or their age on the
  activity's day, computed on the server, and the guardian's names, email and primary phone (dependents):
  never ids, DNI/NIE, birth dates or secondary phones. The SPA keys the cached list by user id. A person
  holds at most one signup per activity, enforced by the primary key (`user_id`, `activity_id`), so
  concurrent signups cannot leave a spare leader row and an administrator's status or role change always
  hits the row they see.
- The email is the only login identifier and the only unique personal value. The DNI/NIE, the phone and
  the optional secondary phone are not unique, so no route checks them against other accounts and neither
  registration nor profile updates reveal whether someone else uses them; the secondary phone only has to
  differ from the same account's phone (`SecondaryPhoneSameAsPrimary`). `POST /api/auth/login` resolves the account by email only.
- No anonymous route tells whether an email has an account. `POST /api/auth/register` answers 204 to every
  valid request: a new address gets the verification link; an address whose account was verified gets a
  notice instead, saying that it already has an account and that nothing changed, and nothing is created;
  an address held by an account nobody verified gets the link of a new account that replaces it, erased
  with its blocked copy, so registering someone else's email reserves nothing. The notice is budgeted as the
  verification mail it replaces. `POST /api/auth/resend-verification` (by email) and `forgot-password` also
  answer 204 alike. Registration refuses a disposable domain first (`DisposableEmailNotAllowed`), which depends
  only on the domain, and then hashes the password before looking the address up, so every other outcome costs
  the same Argon2 work. An account still unverified 24 hours after registering is erased with its
  dependents and blocked copy by `PurgeUnverifiedAccounts`, which `UnverifiedAccountPurger` runs 30 seconds
  after startup and then hourly; each account is locked and checked again before its erasure.
- Only someone who knows an account's password learns its state: after a correct password, the password step
  answers a blocked account with `UserAccountBlocked`, an unverified one with `UserAccountPendingVerification`
  and a locked second factor with `TwoFactorLocked`.
- Granting the administrator flag requires the acting administrator to re-enter their password (a stolen
  session cookie alone cannot promote another account); a wrong password returns
  `UserCurrentPasswordIncorrect` and changes nothing. Revoking needs no password (the SPA asks for
  confirmation), but the body must state `isAdmin`: one without it is refused instead of being read as a
  revocation. Public registration never grants administrator access; on an empty database, startup creates
  the initial administrator from `BOOTSTRAP_ADMIN_EMAIL`, ignored once a user exists, under the fixed id
  `InitialAdministrator.Id` and with a random, discarded password, so its first password is set through a
  password reset to that address. That generic account can be neither demoted (`UserCannotRemoveInitialAdmin`)
  nor deleted, so the application always keeps an administrator; the SPA disables both actions for it. A
  dependent, who never signs in, is never granted the flag (`UserDependentCannotBeAdmin`); the SPA offers the
  action only for independent accounts.
- Nobody changes their own role, administrators included: on the caller's own id, `PATCH
  /api/users/{id}/admin` returns `UserSelfAdminChangeForbidden` before any password is checked, and `PATCH
  /api/users/{id}/change-type` returns `UserSelfTypeChangeForbidden`. Another administrator must make the
  change; the SPA disables both actions on the caller's row.
- `PUT /api/users/{id}` asks the caller (the user or their guardian) for their own
  password whenever the update would replace the account's email, phone or secondary phone. A missing or
  wrong password returns `UserCurrentPasswordIncorrect` and changes nothing; edits that leave all three
  untouched need none.
  Changing the DNI/NIE needs no password. Holders who change their own email keep
  the current one until they open the link emailed to the new address (`PATCH /api/auth/{id}/confirm-email`,
  no session needed): the rest of the edit applies at once, nothing changes when the mail cannot be queued
  (`OtpResendCooldownActive`, `EmailSendFailed`), and a newer request replaces the pending one. When the new
  address belongs to a verified account, its holder gets the same notice as registration instead of the link,
  so the answer never tells whether the address is in use; an account nobody verified that holds it is
  erased, with its blocked copy, when the link is confirmed, and one verified in the meantime keeps it
  (`UserEmailAlreadyInUse`). The stored account,
  never the request, decides the rest: an account that is not already a dependent is refused a guardian
  (`UserParentNotAllowedForAdult`) and any birth date (`UserBirthDateNotAllowedForAdult`), must supply a
  DNI/NIE (`UserNationalIdRequired`) and may set `newsConsent`,
  so no request can demote an account into somebody's dependent; a dependent only accepts its own guardian
  repeated or omitted (`UserParentReassignmentForbidden` otherwise) and is never reassigned, must supply a
  birth date (`UserChildBirthDateRequired`) that stays a minor's only when it changes from the stored value
  (`UserChildBirthDateNotMinor`), and never stores a DNI/NIE, news consent or password. Dependents are
  created by a registration (its
  `minors` list, at most 20, `RequestValidationFailed` beyond) or through `POST /api/users/{id}/children`
  (at most 20 per guardian, counted with the guardian's row locked, `UserChildLimitReached` beyond). They must be
  minors at creation and leave their guardian when the guardian deletes them or when they come of age, since
  the guardian no longer represents them then: `PurgeAdultDependents` erases a dependent, with its blocked
  copy, on the day they turn 18 in `APP_TIMEZONE` (1 March for a 29 February birth in a common year), and
  `SendComingOfAgeNotices` emails the guardian 30 days before, recording it in
  `users.coming_of_age_notice_sent_at`. A changed birth date clears that record, so the corrected day gets its
  own notice; a notice the email limits refuse is retried on the next run, and a guardian whose email nobody
  verified gets none. `AdultDependentPurger` runs both 30 seconds after startup and then hourly, and each
  dependent is locked and checked again first. A household signup list holds at most 21 people.
- `PATCH /api/users/{id}/password` changes only the caller's own password and asks for the current one;
  guardians and administrators get `AccessDenied` (403) for any other account, so nobody else can spend that
  account's password attempts.

### Two-factor authentication

Every account logs in in two steps; there is no opt-out. `POST /api/auth/login` verifies the password and
issues a short-lived challenge cookie (`__Host-CodigoActivo.TwoFactor` in Production, 10 minutes,
non-sliding) instead of a session; it is bound to the password fingerprint and account status, so a password
change or block invalidates it. The ticket is not self-sufficient either: the password step stores a random
challenge id and the time it opened on the account and puts the id in the ticket, so a copied challenge
cookie is refused as soon as that id changes or is cleared — a newer password step rotates it, and an
accepted second factor, a second-factor lockout, a password lockout, a password change or reset and signing
out all clear it, making each challenge single-use — and the account itself refuses the challenge once its
lifetime has passed, whatever the cookie says. Answering, reading or resending a challenge needs the open
challenge the cookie names on an account that may still sign in; the use cases check that themselves, while
the password fingerprint is only checked through the cookie. A challenge obtained just before the account
was locked by wrong passwords is refused at both the ticket and the handler. The account flows serialize on
the account row only when they will act (`AccountLock`, see
[ARCHITECTURE.md](ARCHITECTURE.md#domain-and-persistence)), so parallel requests each answer as they would
alone: a repeated request finds the cooldown and emails nothing, and a correct password whose account was reset
or locked since it was checked is answered like a wrong one (not counted) and opens no challenge. Reading or
resending the current challenge keeps working across reloads. The session cookie is only issued by `POST /api/auth/login/two-factor`, which
accepts the second factor, records the login timestamp and stores the session in one transaction.

Each user chooses one second factor from their account:

- **Email (default)**: a 6-digit code, emailed on the password step, stored as a keyed hash, expiring
  with the challenge, single use. The password step reuses a code sent within the last minute instead of
  resending; `POST /api/auth/login/two-factor/resend` enforces the same 60-second cooldown. These emails
  count as credential mail in the automatic-message limiter.
- **Authenticator application**: RFC 6238 TOTP (HMAC-SHA1, six digits, 30-second steps, one step of clock
  drift) via Otp.NET; the project implements no cryptographic algorithm itself. Enrollment starts with the
  user's password, stores the shared secret encrypted with ASP.NET Data Protection, and only activates once
  the first code is confirmed; an unconfirmed enrollment expires after 15 minutes. Each accepted time step is
  remembered so a code cannot be replayed. Returning to email requires the password and a current code, and
  an account that already uses an authenticator cannot enroll another one (`AuthenticatorAlreadyEnabled`)
  until it returns to email, so a session and the password alone cannot replace it.

Wrong codes are counted per account across logins, authenticator removal and account deletion
(`SecondFactorGuard`): after five failures the second factor locks for 15 minutes (`TwoFactorLocked`). A
successful code resets the counter. These checks run with the account row locked and reloaded, so parallel
attempts are counted one by one and an emailed code or a time step is accepted only once. Confirming a new
authenticator enrollment is not counted: it needs the session and the secret just issued to that session after
its password. Enrollment URIs and shared keys are only returned to the authenticated
owner over HTTPS and never logged.

**Account deletion**: erasing one's own account requires the current password **and** the second factor on `POST
/api/me/deletion` (email code via `POST /api/me/deletion/code`, or the authenticator code). The emailed deletion
code is the login code: same stored code, lifetime and cooldown, so a code sent for either flow is accepted by
the other, and each still needs the password. Deletion signs the caller out, cascades to remove the user, their dependents
and their signups and terms decisions (past ratings stay, see [Event rating
anonymity](#event-rating-anonymity)), and is refused only to the initial administrator
(`UserDeleteInitialAdminForbidden`), before the password is checked. `GET /api/me/deletion` tells the caller
whether they may delete their account, so the SPA hides the action from the initial administrator. `DELETE
/api/users/{id}` refuses the initial administrator and the caller's own id
(`UserSelfDeleteRequiresVerification`); the SPA disables that action on both rows. An administrator can delete
any other administrator without re-entering a password, as with any other user. Every deletion credits the
initial administrator instead of the household as author, last editor or uploader of events, activities, news,
partners, resources and files, and as the account that recorded a terms decision about someone else, in the same
transaction; the original credit is not kept anywhere.

**Blocked copy of deleted accounts** (LOPDGDD article 32): every deletion — self-service, an administrator's,
a guardian's, that of an unverified account whose email another account claims, that of one nobody
verified within 24 hours, or that of a dependent who came of age — first stores one JSON
copy in `deleted_accounts` (`id` is the former user id with no foreign key, `deleted_at`, `data` as
`jsonb`), in the same transaction that locks (`FOR UPDATE`) the user,
their dependents and the household's signups and terms decisions, and then deletes them; a failed deletion
keeps no copy, a concurrent signup, status change or cancellation cannot slip in between, and an erasure that
PostgreSQL aborts to resolve a deadlock is attempted up to three times in total. The copy holds the personal data of the
account and each dependent, the guardian's identification when
a dependent is deleted alone, every signup of the household with activity, event, role, status and signup
time, every terms decision (accepted or rejected, with who decided and its handwritten signature as base64
PNG, and its history: every acceptance, rejection and withdrawal with who decided it, who recorded it, when and
the signature it was accepted with) about the household — or about the guardian for the dependent's events — with each document's text as it stood at
deletion and the earlier versions any of those decisions was taken on, every change of each person's news consent with its instant
and source, and who asked
for the deletion. It never holds password or code hashes, an unconfirmed email change, authenticator
secrets, the login challenge, lockout counters or sessions. Nothing in the application reads the table: the
data is plain JSON protected only by database access. The `DeletedAccountPurger` hosted service runs
`PurgeDeletedAccounts`, which deletes
each copy two years after `deleted_at`, 30 seconds after startup and then hourly. Users are deleted only
through `AccountEraser`, and `DeletedAccountGuard` refuses any commit that deletes the initial administrator,
deletes a user without its copy or changes a stored copy.
The SPA only tells users that they and their dependents lose access and cannot recover the account; the
privacy policy (`/privacy`) states the two-year retention.

**Blocked copy of removed participation** (LOPDGDD article 32): deleting an event (`DeleteEvent`) or an activity
(`DeleteActivity`), or cancelling a signup (`UnassignActivity`), commits through `ParticipationEraser`, which in
the same transaction locks (`FOR UPDATE`) what the removal takes away — the event, its activities, their
signups and its terms decisions; the activity and its signups; or the one signup — so no signup or decision
added meanwhile escapes the copy, stores one JSON copy in `deleted_participations` (`id`, `deleted_at`, `data` as
`jsonb`, no foreign key) and then removes it; a deadlock is retried as for accounts. The copy holds what was
removed (`EventDeleted`, `ActivityDeleted` or `SignupCancelled`), who removed it, the event's title and dates,
the signups removed with activity, role, status and signup time, and, when the event goes, its terms decisions
with who decided, the signature, the history, each document's text and the earlier versions they were taken on, plus the first and last names of the
people they are about or who decided them. A removal that takes away no signup or decision stores nothing.
Like the account copies, nothing in the application reads it, `DeletedAccountGuard` refuses any change to a stored
copy, and `DeletedAccountPurger` runs `PurgeDeletedParticipations`, which deletes each copy two years after
`deleted_at`.

**Recovery**: an administrator can reset a user's second factor to email
(`POST /api/users/{id}/two-factor/reset`) after re-entering their own password, which also clears any
lockout; the user is only notified when the reset removed an authenticator. Losing the Data Protection keys makes stored authenticator secrets unreadable; affected users then
need the same administrator reset.

### CSRF

`CsrfValidationMiddleware` validates every unsafe HTTP method with ASP.NET antiforgery. The client obtains a
token from `GET /api/auth/csrf`, which also sets the `HttpOnly`, `SameSite=Lax` CSRF cookie
(`__Host-CodigoActivo.Csrf` in Production), and returns it in `X-CSRF-TOKEN`; the SPA client handles this
automatically. A missing or invalid token answers 400 `InvalidCsrfToken` and is not logged. The only
exception is `POST /api/emails/unsubscribe/one-click` (`[RequireAntiforgeryToken(false)]`), which mail clients
call without cookies; it uses no session or other ambient credential, only the unforgeable token in its own
URL, so a forged cross-site request can do nothing that the token's holder could not already do.

### Passwords and credential endpoints

Passwords require 12–128 characters and are hashed with Argon2id, never stored or logged in plaintext. Login
performs fallback Argon2 work for unknown identifiers, and registration hashes the password before looking
the address up, to reduce timing differences. A wrong password for an existing account also writes its
failure count, so it is not timed exactly like an unknown address.

Emailed one-time codes and link tokens (login and account-deletion codes, verification, password reset and
email-change links) are random, short-lived and stored only as HMAC-SHA256 under a server-side key
(`HmacOneTimeCodeHasher`), so a copy of the database alone cannot test guesses against them. Production
derives that key with HKDF-SHA256 from the Data Protection certificate's private key, so it exists only in
`api-dataprotection`; other environments use a random key per process, so a restart invalidates pending codes
and links.

Five consecutive wrong passwords for the same account — counted together by `PasswordAttemptGuard` across the
login password step and every route that asks for a password again — lock the account.
Once the lock commits, the `PasswordLocked` listeners delete its `user_sessions` rows and email its owner, even
if the client has gone; one correct password before the limit clears the count.
A locked account answers every password, right or wrong, with the same `InvalidCredentials` after the same
Argon2 work, and only a completed password reset lifts it: `forgot-password` keeps working, while an
administrator's second-factor reset does not unlock. The accepted trade-off is that anyone who knows an
account's email can lock it, so recovery depends on the owner's mailbox.

Credential routes (both login steps and code resend, registration, verification, email-change confirmation,
password recovery/change, authenticator enrollment/removal, administrator grants, second-factor resets, user
updates and the two self-service account-deletion steps) have layered controls:

- nginx caps `login`, `register`, `forgot-password`, `resend-verification`,
  `/api/auth/{id}/(verify|reset-password|confirm-email)` and
  `/api/users/{id}/(password|admin)` at 10 requests/second per client IP, burst 100; the remaining
  credential routes (two-factor login/resend/authenticator/email, `users/{id}`,
  `users/{id}/two-factor/reset`, `me/deletion*`) rely only on nginx's general 200 requests/second limit and
  the API controls below.
- the API enforces 120 requests/minute per client IP in every environment on every credential route;
- the credential routes share a process-wide concurrency limit of 4 executing requests and a FIFO queue of 16,
  which bounds the memory-hard Argon2 work.

Normal API traffic uses different limits, since an IP may represent many legitimate users: nginx allows 200
requests/second per IP, burst 500; the API applies a sliding one-minute budget of 300 requests per
authenticated user, or 3,000 per IP for anonymous traffic, with 128 requests executing and 256 waiting
process-wide. Report routes add a 30-request/minute budget per user (16 executing, 32 waiting); multipart
file writes allow 30 requests/minute per user (12 executing, 12 waiting). Rejected requests, by nginx or the
API, answer 429 `TooManyRequests` with `Retry-After`. These are per-instance controls; scaling API replicas multiplies aggregate capacity.

### Input, output and error handling

- DataAnnotations validate the HTTP request models, and the same rules, together with the custom ones
  (`[NotBlank]`, `[HttpUrl]`, `[RichText]`, `[SpanishNationalId]`, `[NotDefaultOrFutureDate]`), are checked
  again on every command before its handler runs, whatever starts it. JSON bodies accept enum values only by
  their declared name, never as integers, and enums bound from the query string reject undefined values.
- JSON bodies refuse any string holding a control character other than a tab or a line break
  (`ControlCharacterRejectingConverter`), and so do rich-text texts and labels and the multipart email body
  (`[PlainText]`); free-text list criteria and `sort` take at most 200 characters and no control character
  (`[SingleLine]`); lists in request bodies are bounded with `MaxLength`. Such input is answered
  `RequestValidationFailed` instead of failing in PostgreSQL, which refuses U+0000. `null` items of those
  lists are skipped.
- A list `sort` parameter keeps only the first occurrence of each known key, so a request cannot grow the
  query EF Core translates recursively.
- Account emails must read as one mailbox: no whitespace, control character or `()<>[]:;,\"`
  (`UserEmailInvalid`).
- The SPA refuses to send a request whose path the browser would rewrite (dot segments, backslashes,
  stripped tabs), because generated paths interpolate their parameters unencoded; its detail routes only
  match GUID ids, since Vue Router decodes `%2F`, `%3F` and `%23` in params, and it treats an emailed
  account link whose user id is not a GUID as incomplete, so a crafted link cannot steer a signed-in
  visitor's request to another endpoint.
- Expected failures use a string `ErrorCode`; all failures return
  `ApiErrorResponse(Title, Status, Code, TraceId)` without stack traces or internal details.
- User-supplied links allow only absolute HTTP(S) URLs without embedded credentials.
- Rich-text JSON (`[RichText]`, `RichTextAllowlist`) may only use the editor's nodes, marks and attributes,
  at most 50 levels deep and 5,000 nodes. Its links may point to absolute HTTP(S) URLs without credentials,
  `mailto:`, `tel:`, root-relative paths (never `//` or `/\`, which browsers read as another host) and
  fragments, and never contain control characters, which browsers drop (`/<tab>/host` is another host);
  embedded images may only reference this application's UUID-based file endpoint; colors are hex or
  `rgb()`/`rgba()`. The editor saves only what the API accepts and the SPA renders through the same
  allowlist. Terms documents are text only: their editor has no image option and drops images from loaded
  or pasted content, and the API refuses a description with an image node (`RequestValidationFailed`).
- User-controlled values inserted into email templates are HTML-encoded; administrator-authored bodies are
  plain text rendered inside the branded template, and their subjects stay on one line. Upload and
  attachment names keep only their last path segment, without control or invisible formatting characters,
  cut to 260 characters; an attachment declared `message/*` or `multipart/*` is sent as
  `application/octet-stream`.

### Transport and proxy trust

TLS ends at the operator's external proxy; its requirements (TLS versions, overwriting forwarded headers,
redirecting plain HTTP, the `Host` header, IPv4 only, no direct access to port `8080`) are in
[DEPLOYMENT.md](DEPLOYMENT.md#tls-and-proxy-boundary). The API accepts one forwarded hop and honours
`X-Forwarded-For`/`X-Forwarded-Proto` only from IPv4 loopback and private ranges (`127.0.0.0/8`, `10.0.0.0/8`,
`172.16.0.0/12`, `192.168.0.0/16`). nginx applies no peer filtering of its own and counts only an exact `https`
`X-Forwarded-Proto` as HTTPS. IPv6 stays disabled on both networks and nginx answers 403 to IPv6 clients,
because Docker would relay IPv6 clients from its bridge gateway, a private address whose `X-Forwarded-For` nginx
trusts.

nginx sends HSTS (`max-age=63072000; includeSubDomains`, no `preload`) only when its normalized scheme is HTTPS,
plus CSP, frame denial, MIME sniffing, referrer, permissions, `Cross-Origin-Opener-Policy: same-origin` and
`Cross-Origin-Resource-Policy: same-origin` headers (no `Cross-Origin-Embedder-Policy`); `includeSubDomains`
requires every subdomain of the public host to work over HTTPS only. Kestrel does not emit a `Server` header
(`AddServerHeader=false`); nginx sends `Server: nginx` without a version (`server_tokens off`).

### Privacy and third parties

- Browsers load nothing from third parties: fonts are bundled from Fontsource and served from the application
  origin; the CSP allows scripts, styles, fonts, images and connections only from `'self'` (plus inline styles
  and `data:`/`blob:` images), sets `base-uri 'none'` and adds `upgrade-insecure-requests` only when nginx's
  normalized scheme is HTTPS. Do not add CDNs, analytics or embeds without a legal basis and consent review.
- The API downloads the public disposable email domain list once a day (see
  [Email abuse controls](#email-abuse-controls)); the request carries no user data, and addresses are only
  checked against the local copy.
- The only cookies are the session, two-factor challenge and CSRF cookies above; the theme choice stays in
  `localStorage` and is never sent to the server, and `sessionStorage` only holds the stale-build reload mark.
  They are all needed to run the site or keep a preference the user picks, and only the persistent session,
  behind its unticked checkbox, outlives the browser session, so the site shows no cookie banner. The
  `/cookies` page
  (`frontend/src/pages/cookie-policy`) lists every cookie and storage key with its purpose and duration;
  change it with them. `Referrer-Policy: same-origin` keeps page URLs out of
  requests to external sites, and `X-DNS-Prefetch-Control: off` stops browsers from pre-resolving link
  destinations. API responses whose body is `application/json` or `application/problem+json` also carry
  `X-Robots-Tag: noindex, nofollow`, so search engines do not index API payloads.
- The `/privacy` page (`frontend/src/pages/privacy-policy`) states, for each processing of personal data,
  the data, purpose, legal basis and retention, plus the processors, transfers and rights; the registration
  form shows its first layer (`PrivacyNotice`). Change them with any new personal field, processor, recipient
  or retention.
- `GET /api/me/personal-data` (report rate limit) gives the signed-in user a copy of their own and their
  dependents' personal data (articles 15 and 20 GDPR), which «Mi cuenta» saves as a JSON file with Spanish
  names and labels: the account with any unconfirmed new email, the news consent history, the dependents, the
  household's signups, the terms decisions about the household with who took them, their signatures and their history, the open sessions and the audit
  entries the user ran or that name a household member as main or related entity, logins before the session
  included (time, action, outcome, kind of actor and client IP, without the IP when an administrator ran it
  and never what else the entry touched). It reads only the caller's household and never holds password or
  code hashes, authenticator secrets, the login challenge or lockout counters. It leaves out ratings, which
  nothing links to an account (article 11 GDPR); blocked copies, which nobody may use (LOPDGDD article 32); the
  email outbox, which only holds a message until it is delivered and may carry one-time codes; and the
  authorship of content, the images in Google Drive and inquiries, which are answered on request. Add a new
  personal field or table to it.
- News consent (articles 6.1.a and 7 GDPR) is changed only by its holder: at
  registration, in «Mi cuenta», or through the unsubscribe link of a news email or its mail client's
  unsubscribe button. `User` appends every change to `news_consent_changes` with its instant and source
  (`Registration`, `Profile`, `UnsubscribeLink`, `OneClickUnsubscribe`) and the blocked copy keeps them. Consents stored before that table existed have no
  recorded change.
- Only adults register, and only a parent or legal guardian, who represents the minor (Civil Code articles
  162 and 225), adds a minor: `POST /api/auth/register` refuses a body without `declaresAdult`
  (`UserAdultDeclarationRequired`) before looking the address up, and every minor, added at registration or
  through `POST /api/users/{id}/children`, needs `declaresGuardianship` (`UserGuardianshipDeclarationRequired`).
  `users.adult_declared_at` and `users.guardianship_declared_at` keep when each was declared, also in the
  blocked copy. The SPA sends the first once the visitor confirms being of age at its age gate and the second
  with every minor, since each form that adds one states that adding it is that declaration. Accounts and
  minors added before they were recorded have none.
- A terms document holds either conditions or a consent (`terms_documents.kind`). Decisions are recorded per
  person signed up, keyed by event, person and document, with who took them (`event_terms_acceptances.user_id`
  and `decided_by_id`): an adult decides for themselves; for a dependent, the guardian decides the conditions
  and, before the age of 14, the consents, and from the age of 14 the dependent decides their own consents
  (article 7 LOPDGDD, article 3.1 LO 1/1982) on the guardian's screen; `TermsConsent.DeciderOf` names the
  decider from the birth date, never from the request. A consent is never required: an event that links one
  as required is refused (`EventTermsConsentRequired`), and `TermsGate` never blocks a signup on a consent.
  An acceptance is never re-asked and holds until its consent is withdrawn; a rejection or a withdrawal is
  revisable and the stored row takes who accepts it now; a required document's rejection is never persisted,
  so it blocks that signup without excluding the person for good. The row is the current state: each change
  of it (the first decision, a later acceptance, a withdrawal) also appends to `terms_decision_changes` whether
  it accepts, who decided, the account that recorded it (the signed-in user, an administrator included), when and the
  signature, so each earlier state can still be proven (article 7.1 GDPR). `AssignActivity` skips this step
  when an administrator signs up someone else; `AssignHousehold` runs it for every member it signs up, each
  with their own decisions. Decisions stored before they were per person name the deciding guardian as the
  person; documents stored before they had a kind are conditions; decisions stored before the history began
  with one change, their state then, recorded by who decided it. The rows and their history cascade away with
  the person or the decider, once the blocked copy of that account holds them, and with the event, once the
  blocked copy of removed participation does.
- A consent is withdrawn (article 7.3 GDPR) through
  `POST /api/events/{eventId}/terms/decisions/{userId}/{termsDocumentId}/withdraw` (`[AllowOnlySelf]`): by the
  person, by their guardian, or by an administrator who records a withdrawal asked for by email. Conditions are
  not withdrawn (`EventTermsWithdrawalNotAllowed`; leaving means cancelling the signup) and a consent not given
  now answers `EventTermsConsentNotGiven`. The withdrawal is decided by whoever `TermsConsent.DeciderOf` names
  and recorded by the caller; the acceptance it ends stays in the history with its signature, since withdrawing
  does not make earlier processing unlawful. «Mi cuenta» lists the household's consents (`GET /api/me/consents`)
  and withdraws a given one with one confirmation, as easily as it was given; the event's decisions tab shows it
  as withdrawn and lets an administrator record one. Once committed, `ConsentWithdrawalNotification` emails the
  legal contact (`LEGAL_EMAIL`; nothing without it) so what the consent covered stops being used and is removed
  from the site and the social profiles; a failed delivery is logged without personal data and never undoes the
  withdrawal.
- A terms document is versioned (`terms_documents.version`), so the text each decision was taken on can always be
  shown (article 7.1 GDPR): every rewrite that changes its name, text, kind or signature keeps the replaced version
  in `terms_document_versions` and numbers the new one, even when nobody decided yet, so a decision recorded while
  the document changes still names a version whose text is kept. Each decision and each change of its history
  stores the version it was taken on (`terms_document_version`; a withdrawal keeps the one withdrawn), and nobody is
  asked again after a rewrite. Every decision sent names the version shown (`TermsDecisionRequest.Version`, which
  the terms state returns); one on a version that is no longer current is refused (`EventTermsDocumentChanged`)
  and the SPA reloads the documents and asks again. Texts are compared as JSON, so saving without changes keeps
  the version. Decisions stored before versions existed name version 1, the text then current.
- A terms document can ask for a handwritten signature (`requires_signature`): accepting it then needs a PNG
  drawn on screen (`TermsConsent` refuses the acceptance without one), which `HandwrittenSignature` refuses above 128 KiB or 2000×1000 pixels and which is stored
  in `event_terms_acceptances.signature`, never in the public file storage. Only administrators read it
  (`GET /api/events/{eventId}/terms/decisions/{userId}/{termsDocumentId}/signature`, answered
  `no-store`).
- Verification, email-change and password-reset links put the user id and a 256-bit code from a
  cryptographic random generator in the URL fragment (`/reset-password#userId=…&code=…`), which browsers
  never send to the server; the page reads and removes it from the address bar, so a reload needs the
  emailed link again.

### Event rating anonymity

`event_ratings` stores the answer under a random v4 `Guid`, with no user id or timestamps: `id`, `event_id`,
`score` (1–5, or null when only answers were written), `most_liked`, `least_liked`, `suggestions`.
`POST /api/events/{eventId}/rating` requires the event to exist and have ended, the caller (or a dependent) to
have confirmed attendance and the rating to carry a score or an answer (`EventRatingEmpty`), but does not
track who already rated an event: the same attendee may submit more than once, and every accepted call
appends one row through the standard repository-plus-`IUnitOfWork` write path. There is no per-user
submission cap besides the general 300-requests/minute-per-authenticated-user budget described above; every
accepted submission, repeats included, counts toward that event's rating count, and the scored ones toward
its average. The SPA can only stop a second submission while the history stays open.

Account deletion keeps past ratings, since their content carries no author reference. Ratings are plain
inserts, so `event_ratings`' physical row order (and `xmin`) reflects write order.

**Known limitations:** PostgreSQL write-ahead log access (`pg_wal`, in volume copies and point-in-time
recovery); dead tuples until the next `VACUUM`; differential observation of two dumps taken before/after a
submission; and, for events with one or two ratings, deducible authorship from count or free-text content (no
minimum-rating threshold exists). Saving a rating is never audited (`SaveEventRatingCommand` is `IUnaudited`)
and the diagnostic log carries no client IP, requested path or user id, so no stored log ties a rating to an
account (see [Logging](#logging)). The audit log does record, with their instants, the other commands of the
same person, such as the login before the rating: someone with database access could still match the
physical order (`xmin`) of `event_ratings` against those instants. The stored rating content still carries no
author reference.

### Logging

The API keeps two logs, both read only by administrators: the diagnostic log and the
[audit log](#audit-log). The diagnostic log follows GDPR data minimization: only failures and warnings an
operator has to diagnose are recorded, so normal use writes nothing to it, and none of its entries outlives
its retention (30 days by default). Never in the diagnostic log: names, emails, phone numbers, anything typed
by the client, the requested path or query, client IP, user agent, cookies, tokens and one-time codes. Logged:
counters, enum/kind values, error/SMTP/HTTP codes, the HTTP
method, the matched endpoint's route template, and exception type/message/stack traces. No event template
carries an id, but an exception message may quote the pseudonymous GUID of a row or stored file it failed on,
such as the path of a file under `/app/files`; it is written as is.

- **API**: every event is declared with `[LoggerMessage]` in one of four `*Log` classes — `SecurityLog` and
  `ApplicationLog` (Application), `InfrastructureLog` and `ApiLog`. `CA1848` is an error (`backend/.editorconfig`), and the
  architecture tests `LoggingConventionTests`/`LoggingCallSiteTests` enforce an allowed-placeholder list and
  that no event is declared below `Warning`. `Logging:LogLevel` in `appsettings.json` is the level policy for
  both the database and the console: `Default` is `Warning`, which also drops the framework's start and stop
  messages; `Microsoft.EntityFrameworkCore` is `None`: every EF Core failure is rethrown and recorded where it
  is caught, so its own entries only repeated it, quoted the SQL of a failing command (which can carry literal
  values) and reported expected unique-key races and retried deadlocks as errors. An environment can override
  any category with `Logging__LogLevel__<Category>`. There is no per-request logging middleware: unhandled
  exceptions are recorded only by `GlobalExceptionHandler`, with the HTTP method and the route template, never
  the requested path. Expected outcomes are not logged: a refused concurrent update (409 `ConcurrentUpdate`), a
  signup that lost a unique-key race (409), a deadlock that a retry resolved, or a rejected CSRF token, which
  the client refreshes and sends again. A use case is logged as slow only above 5 seconds. A failure that
  repeats on every outbox run (`EmailOutboxRunFailed`, `ExhaustedEmailNotRemoved`, `EmailDeliveryAttemptFailed`)
  is logged at most once every 15 minutes. `NormalUseLoggingTests` drives anonymous,
  member and administrator flows and fails if any entry passes the policy. EF Core sensitive-data logging
  stays disabled; `SmtpEmailSender` strips the recipient and server reply out of SMTP exceptions before they
  are logged.
- **Security events**: only three events exist, all `Warning` and free of any user id —
  `PasswordLockoutTriggered` and `TwoFactorLockoutTriggered` (an account or its second factor was locked;
  written once the lock commits) and `SecurityNotificationRateLimited` (a security-change email was dropped
  by the limiter). Logins, logouts, password changes and administrator rights changes are not diagnostic
  events: the audit log records them.
- **API log output**: every event the level policy lets through is stored in the `diagnostic_log_entries`
  table of the application database by a batching Serilog sink (`DatabaseLoggerProvider`): the UTC instant,
  the level, the category, the event name, the rendered message (up to 4,000 characters), the exception with
  its stack trace (up to 32,000), the trace id and the values of the message template. Properties of logging
  scopes, such as the requested path, are dropped. Logging never affects a request: emitting only enqueues
  (at most 10,000 events; beyond that they are dropped), batches of up to 1,000 are written every 2 seconds
  with a binary `COPY` over a pool of three connections of its own (`codigoactivo-logs`, short timeouts, no
  logger), a row the database refuses is dropped alone, and a batch that cannot be written is retried with
  backoff for 2 minutes and then dropped. Development also writes to the console. The fatal startup event
  (`CodigoActivo.Lifecycle The API host terminated unexpectedly`) always goes to the console regardless of the
  level policy, and to the table when the host was built; the process exits non-zero. Failures of the logging
  pipeline itself go to standard error. Nothing about logging is needed for the API to start.
- **nginx**: `access_log off` and `error_log` writes to `/dev/stderr` at `crit`, so no request or error is
  stored; a failed API shows in `docker compose ps`, and the 5xx it returns is logged by the API itself.
- **PostgreSQL**: its logging collector writes one file per weekday, `postgresql-<Mon..Sun>.log` (UTC), into
  the `logs-db` volume at the default `log_min_messages=warning`, which also keeps the few `LOG` lines of each
  start and stop. `log_error_verbosity=terse` drops the `DETAIL` lines that quote row values,
  `log_min_error_statement=panic` keeps the failing SQL out, and checkpoint and autovacuum reports are off.
  The first start of an empty database also leaves the `__EFMigrationsHistory does not exist` error of the
  migrator. Messages written before the collector starts go to the discarded stderr. A known residual: a
  `FATAL` authentication failure names the attempted role; `db` is reachable only from the internal `backend`
  network.
- **Retention and container logs**: `db` and `web` use `logging: driver: none`; `api` keeps a `json-file`
  log of two 1 MB files, which in Production only receives the fatal startup event and failures of the
  logging pipeline. The log tables are partitioned by UTC day (`<table>_pYYYYMMDD`). At startup, right after
  the migrations, and every hour (`LogRetentionWorker`) `LogPartitionMaintainer` creates the partitions of
  today and the next two days, drops the partitions whose whole day is older than the cutoff and deletes the
  older rows of the partition the cutoff falls in. The retention is fixed in code (`LogRetentionOptions`): 30
  days for the diagnostic log and 60 days for the audit log; the cutoff is the retention minus one hour, so no
  entry outlives the retention of its table, and a release that lowers one removes the older entries when the
  API starts. Each statement runs in a short transaction
  with a 5-second lock timeout; a failed run is logged as `LogMaintenanceFailed` (`Error`) and never stops the
  API. Purging stops while the API is stopped, so the effective retention lengthens by however long it is
  down. PostgreSQL rotates its own files at midnight with `log_truncate_on_rotation=on`, emptying the file of
  the new weekday, so `logs-db` holds at most 7 days while `db` runs. A day that starts while `db` is stopped
  is appended to its old file instead, which then keeps entries from a week earlier. See
  [DEPLOYMENT.md](DEPLOYMENT.md#production-topology).
- **What administrators see**: the diagnostic log shows them trace ids, route templates, the GUIDs exception
  messages quote and, in the audit log, client IPs and the names of the accounts and things each command
  named, or that they were removed since. Raising a framework category such as `Microsoft.AspNetCore` to
  `Information` would store requested paths and query strings there too.

### Audit log

`AuditCommandDecorator` records every command the application runs in `audit_log_entries`, through the same
batching pipeline and failure guarantees as the diagnostic log: a failure to record never changes the
command's result. An entry keeps the instant, who ran it (a user with their account id, an anonymous caller or
the system), the caller's IP as the trusted proxy forwarded it (the address the rate limits use, see
[Transport and proxy trust](#transport-and-proxy-trust)), the command, the main entity and the other entities
it named by type and GUID, how it ended (succeeded, rejected with the error code, or failed) and the trace id
that error responses show as their reference. It never stores anything typed by the client: names, emails,
passwords, one-time codes, signatures, secrets and free text stay out, and so does the requested path. Login,
password reset and verification requests record the account the email belongs to, so failed logins are
attributed to their account. Saving an event rating is never recorded, and the scheduled purges are recorded
only when they removed something. The account ids have no foreign key, so an erased account's entries remain
until the audit retention ends; they are not part of its legal copy.

Only administrators read either log: `GET /api/logs/diagnostics`, `/api/logs/diagnostics/{id}` and
`/api/logs/audit` require the administrator role and share the report rate limit.

### Production configuration and secrets

Production refuses to start with weak secrets, a non-public or non-HTTPS `APP_BASE_URL` or unencrypted SMTP;
the full list of startup checks and the secret file rules are in
[DEPLOYMENT.md](DEPLOYMENT.md#first-production-start). Secrets are Compose file secrets, never environment
variables, so they stay out of `docker inspect`, `docker compose config` and every process environment; code
running inside a container can still read the files mounted into it. The root `.env` and `appsettings.json`
hold no credentials, and there is no bootstrap password. The API's database connection never leaves the
internal `backend` network. Restrict `secrets/` to the deployment account and keep it out of images and
unencrypted backups. Rotating the database password must update both the PostgreSQL role and
`secrets/postgres_password`.

### Data Protection and containers

Production persists ASP.NET Data Protection keys in `api-dataprotection`. Every key ring element is stored
as an `urn:codigoactivo:data-protection:aes-gcm:v1` XML element carrying a salt, nonce, authentication tag,
ciphertext and an Ed25519 signature; decryption first verifies that signature against the certificate pinned
in the volume, then derives the AES-256-GCM key from `DATA_PROTECTION_CERTIFICATE_PASSWORD` with Argon2id
(`Argon2idKeyDerivation`: 3 iterations, 64 MiB, 4 lanes, a 16-byte salt — the same parameters and code path
that hash passwords) before decrypting with `System.Security.Cryptography.AesGcm`. Reading the ring ignores,
and logs as `DataProtectionKeysIgnored`, any key whose secret is stored in clear or names another decryptor,
so whoever can write to the volume without the password cannot plant a usable key. The locally generated
Ed25519 certificate's own private key is stored the same way, in a fixed-size binary container (version,
salt, nonce, tag, ciphertext) with the certificate's DER bytes as associated data. BouncyCastle is used only
to generate and parse the Ed25519 certificate and to sign/verify; it performs no encryption. Since the
certificate password derives the key-wrapping key, there is no rotation procedure: changing it means
recreating the volume, which invalidates every session, every stored authenticator secret and every pending
one-time code or link. Outside Production the key ring is unencrypted on disk, but every environment protects
Data Protection payloads themselves — session and two-factor cookies, antiforgery tokens, authenticator
secrets and the email outbox content described below — with AES-256-GCM. Application containers run as
non-root, drop all capabilities, enable `no-new-privileges` and use read-only root filesystems; PostgreSQL
drops every capability its entrypoint does not need and is reachable only on the internal backend network.
The development override removes parts of this boundary and must not be deployed.

### Files and multipart requests

Stored uploads are limited to 10 MiB by default and saved under `/app/files` in the `api-files` volume; nginx
also enforces a `12m` request-body limit. Kestrel refuses any other request body above 2 MiB, which no valid
JSON request reaches; only the multipart upload and email endpoints raise it, to the upload limit plus 64 KiB.
Access to stored files goes through API authorization, not a direct static volume mount. An upload is kept
while content references it (`StoredFileRepository.GetInUseAsync`); one that nothing references 7 days after
it was uploaded, such as an image picked for a form that was never saved, is deleted with its content by
`PurgeUnusedFiles`, which `UnusedFilePurger` runs 30 seconds after startup and then hourly. The purge locks the
files it deletes (`FOR UPDATE`) before reading their references, and saving an event, news item or resource
locks its thumbnail and the images its rich text embeds (`FOR KEY SHARE`, `ContentFiles`) in the transaction
that stores it, so a save either commits first and keeps them or is refused (`*ThumbnailNotFound`,
`RichTextFileNotFound`) instead of storing a reference to a deleted file. A failed run logs
`UnusedFilePurgeFailed`, and content that cannot be removed from the volume logs
`StoredFileContentDeleteFailed`. Administrator email
attachments never reach the `api-files` volume: they are read once
per batch into the Data Protection-protected outbox content described above and removed once every message
referencing them is delivered or discarded, with count, combined bytes and recipient count bounded by
`ManualEmailOptions`.

Every security, abuse and retention value (Argon2id cost, password policy, code and session lifetimes,
lockouts, rate, concurrency and email limits, request sizes, retentions, nginx hardening) is fixed in code;
the deployment configuration holds only addresses, credentials, identity and the demo choice
([DEPLOYMENT.md](DEPLOYMENT.md#environment-variables)), so it cannot weaken any of them.

## Email abuse controls

New accounts must always confirm an emailed OTP before their first login, and holders confirm their own new
email the same way; there is no switch. Mail to an address nobody has verified yet greets nobody, so neither
registration nor an email change relays a name typed by someone else to that mailbox. Verification and
password-reset codes expire after 15 minutes with a 60-second resend cooldown; an email-change link also
lasts 15 minutes, and each new one needs the password again. Login and account-deletion codes share storage
and expire after 10 minutes with the same cooldown. SMTP must be configured everywhere or the API refuses to
start. Activity signup sends no message;
confirming or rejecting one queues an outcome email after commit, always to the guardian address for a
dependent. Delivery errors never roll back registration, recovery, activity decisions or security
changes.

Registration and email changes (`PUT /api/users/{id}`) refuse addresses of disposable (temporary) mailbox
providers with `DisposableEmailNotAllowed` (400), which the SPA presents as a security measure. An address
is refused when its domain (lowercase, punycode, no trailing dot) or any parent domain other than the
top-level one is in `disposable_email_domains`; an unchanged email is not re-checked, so existing accounts
keep working. `DisposableEmailDomainRefresher` downloads the list of the
[disposable-email-domains](https://github.com/disposable-email-domains/disposable-email-domains) project
30 seconds after startup and then every 24 hours, or every hour after a failure. A download replaces the
stored list only when it is at most 2 MiB of UTF-8 text with one valid domain name per line (blank and `#`
lines ignored), holds at least 1,000 domains and would not refuse any domain in
`DisposableEmailDomainList.ProtectedDomains` (Gmail, Outlook/Hotmail, Yahoo, iCloud and Proton, plus
`Organization.WebDomain`). An unreachable or slow source, an error status or a rejected body keeps the last valid
list; until a first list is stored, every domain is accepted.

Changing an account's password (by the user or through recovery), its second factor (authenticator confirmed,
returned to email, or reset by an administrator), its administrator flag or its email or phones queues a
notification to the affected account after commit, from the handler, whoever asked for the change. The notice
names the change and its timestamp and carries no code, secret or link that performs an action; an email
change is reported to the **previous** address once it applies (for a holder's own change, when the new
address is confirmed) and only ever quotes the new one masked, and a phone change only states that the phone
changed, without quoting it. These messages are
ordinary automatic mail, not credential mail, so they spend the shared budget without touching the credential
reserve that login codes rely on.

Every automatic verification, password-reset, second-factor-code, security-change and activity-decision email
passes through `ThrottledEmailSender`: each normalized destination has burst/hourly/daily budgets, the
process has a global budget with a credential-email reserve whose half only second-factor codes may spend,
and second-factor codes have per-destination budgets of their own, so verification and reset mail, which
anyone can request, never stops a login code. Normalization lowercases addresses, strips sub-address tags
and folds dots only for Gmail/Googlemail. Quota is spent on attempt, before the message
reaches the outbox; SMTP failure and a full outbox do not refund it. The limiter is always enabled,
in-memory, resets on restart and is multiplied by the number of API replicas.

Both automatic and administrator-written mail are stored in a PostgreSQL outbox (`email_outbox_messages`)
and delivered in the background by `EmailOutboxProcessor`, so a request that queues email returns once the
row is committed, not once SMTP accepts it: see [DEPLOYMENT.md](DEPLOYMENT.md#email-delivery) for the
delivery schedule, capacity and monitoring. The stored subject, bodies and attachments are protected with
Data Protection (purpose `CodigoActivo.EmailOutbox.v1`); recipient address/name, unsubscribe link and one-click
unsubscribe address, kind and attachment names are stored in clear. Recreating the Data Protection key ring makes pending rows unreadable and they are
discarded on their next delivery attempt.

Only administrators may send through `/api/emails/...`; recipients are resolved on the server from the same
filters as user/attendee lists, never client-supplied addresses. Each recipient gets a separate message,
dependents without their own address are skipped, and so are accounts still pending verification, whose
address may be someone else's (a single one is refused with `EmailRecipientNotVerified`); attachments remain
transient. Signup decision emails skip those addresses too, and administrator rights are refused to such an
account (`UserPendingVerificationCannotBeAdmin`). The verification email of a registration that accepted news
says so. Every send carries a required `purpose`: `Notice` reaches every addressable recipient; `News`
reaches only those with news consent, enforced by the server (`ManualEmailAudience.ReachedBy`; a single
recipient without it is refused with `EmailRecipientWithoutConsent`), and its footer carries the contact address
(`LEGAL_EMAIL`, the same one the site shows) and the recipient's own unsubscribe
link, `/unsubscribe#token=…`. The token is the user id protected with Data Protection (purpose
`CodigoActivo.Unsubscribe.v1`), so it cannot be forged and never expires while its key stays in the ring;
recreating the ring invalidates the links already sent. The stored content is shared by the batch and each
outbox row holds its recipient's link in clear (`unsubscribe_url`), put in place at delivery. The SPA page
posts the token to the anonymous `POST /api/emails/unsubscribe` (credential rate limit), which withdraws the
consent and answers 204 again for a withdrawn consent or an erased account, or `EmailUnsubscribeLinkInvalid`
for an altered token. Each news message also carries the one-click unsubscribe of RFC 8058:
`List-Unsubscribe: <…/api/emails/unsubscribe/one-click?token=…>` with the same token (stored per row in
`one_click_unsubscribe_url`) and `List-Unsubscribe-Post: List-Unsubscribe=One-Click`. The mail client posts
that pair as `application/x-www-form-urlencoded` or `multipart/form-data`, without cookies or an antiforgery
token; the endpoint is anonymous, kept out of the OpenAPI document, shares the credential rate limit, answers
204 without redirecting, and records the withdrawal as `OneClickUnsubscribe`. The token in that URL never
reaches a log: nginx keeps no access log, and the API keeps nothing below `Warning`, the level of ASP.NET's
request lines. This mail bypasses the
automatic-message limiter; the endpoint reports how many messages were queued and how many recipients were
skipped, not what SMTP later did with them. Single-recipient messages allow 30 requests/minute
per administrator (10 executing, 10 waiting); bulk messages allow 5 requests/minute (2 active, no waiting
queue). All requests remain subject to nginx's general API limit, application recipient/attachment limits and
the five-minute nginx upstream timeout.

`GET /api/emails/users/audience` and `GET /api/emails/events/{eventId}/attendees/audience` let an
administrator preview a send before committing to it: given the same filters as `POST /api/emails/users` or
`POST /api/emails/events/{eventId}/attendees`, they return `{ recipients, withoutConsent }` computed by the
same shared selection (`ManualEmailAudience`) the send commands use, so the preview always matches what a
send would reach — distinct addressable recipients deduplicated case-insensitively, which a notice
reaches, and how many of them a news send leaves out for lack of consent. These preview endpoints carry none of the send limits above, only
the general API limits of nginx and the API.

## Demo mode

`DEMO_MODE=true` seeds invented accounts with a random, hashed and discarded 32-byte password, fictitious
addresses, and the second factor still required, so only the bootstrap administrator can sign in. Such
deployments are disposable and must never contain private data. Seeding downloads its sample images from
`picsum.photos`, at most 10 MiB each, in requests that carry no user data. Its log pages mix the real entries
with an invented history, including a password attack, whose callers use the IP ranges reserved for
documentation (`192.0.2.0/24`, `198.51.100.0/24`, `203.0.113.0/24`, `2001:db8::/32`). The mode is locked
on first start; see [DEPLOYMENT.md](DEPLOYMENT.md#demo-mode-and-initial-administrator).

## Operational checklist

- Keep the TLS proxy as the only public ingress and restrict direct port `8080` access.
- Back up and test restoration of the database, uploads, mode state and Data Protection material.
- Alert on authentication abuse, email-budget exhaustion, queue saturation and SMTP delivery failures.
- Review administrator access; smoke-test session revocation, file authorization and account recovery after
  releases.
- Treat the development override, the bootstrap administrator of demo deployments and any copied production
  `secrets/` directory as sensitive operational risks.
