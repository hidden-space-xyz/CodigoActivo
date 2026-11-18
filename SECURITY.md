# Security Policy

## Reporting a vulnerability

Report security issues privately through **Security → Advisories → Report a vulnerability** in the GitHub
repository. Do not open a public issue. Include the affected version or commit, impact, reproduction steps
and any suggested mitigation. The project supports the latest deployed version from `master`.

## Security model

The application assumes a same-origin deployment behind a trusted TLS reverse proxy. The browser reaches
nginx, which serves the SPA and forwards `/api` to the private API container. Cross-origin API use and JWT
authentication are not supported.

Production refuses to start with weak secrets, a non-public or non-HTTPS `APP_BASE_URL` or unencrypted SMTP
([startup checks](DEPLOYMENT.md#first-production-start)). Credentials are Compose file
[secrets](DEPLOYMENT.md#secrets), never environment variables, the root `.env` or `appsettings.json`; code
running inside a container can still read the files mounted into it. The containers run with the isolation
that [Production topology](DEPLOYMENT.md#production-topology) lists; the development overlay removes part of
it and must not be deployed. Every security, abuse and retention value is
[fixed in code](DEPLOYMENT.md#fixed-settings), so no deployment setting can weaken it.

### Authentication and authorization

#### Sessions

- Authentication uses an ASP.NET Core session cookie: `HttpOnly`, `SameSite=Lax`, non-sliding, expiring after
  30 days. It is a browser-session cookie unless the user ticks «Mantener la sesión iniciada en este
  dispositivo» on the password step (`keepSignedIn`); ticking it makes the cookie persistent, the challenge
  cookie carries that choice to the second factor, and a claims refresh keeps it. The session, challenge and
  CSRF cookies are `Secure` in every environment except Development and use a `__Host-` name in Production.
- The authentication ticket in that cookie is not self-sufficient: completing the second factor also writes a
  `user_sessions` row, whose id travels in the ticket's `sid` claim, and deletes that user's already-expired
  rows; `RemoveExpiredSessions` deletes every other expired row hourly. The row carries the expiry that decides
  access: refreshing the claims re-issues the cookie with a later expiry, so a cookie can outlive its row, and
  the ticket is rejected as soon as the row is missing or expired. Deleting an account removes its rows by
  cascade.
- Every authenticated request revalidates account status, password fingerprint, staff role, data protection
  designation and the `sid` row in one query, which rejects the ticket when the account is not active, has
  another password or has no live row, and refreshes its claims when the staff role or the designation changed.
  Changing or resetting the password invalidates the sessions through the fingerprint, and the change then
  deletes that user's session rows; a [password lock](#passwords-and-credential-endpoints) leaves them working.
  The SPA reacts to a lost session as [ARCHITECTURE.md](ARCHITECTURE.md#views-forms-routing-and-session)
  describes, and also drops its cached session and data right after the user changes their password.
- `POST /api/auth/logout` deletes the row of the presented session before clearing the cookies, so a copy of
  that cookie stops working immediately instead of lasting until its expiry. It is idempotent: it asks for no
  valid session, only the CSRF token, and always answers 204 after clearing the session and challenge
  cookies, including when the row is already gone or when deleting it or ending a pending login challenge fails
  (the failure is logged).

#### Authorization

- Every endpoint requires an authenticated session unless it is marked `[AllowAnonymous]` (fallback
  authorization policy).
- Authorization rests on the staff role of the account (`users.staff_role`, `StaffRole`): none, editor or
  administrator, each including the rights of the one before, carried by the session as role claims (`editor`,
  `administrator`, and `privacy-officer` for the account designated for data protection). Catalog values such
  as `UserType` are not authorization roles.
- `[AllowOnlyStaff(StaffRole.Editor)]` protects the content endpoints: events and activities with their
  catalogs and terms documents, news, partners, resources and files. Deleting an event or an activity erases
  its signups, terms decisions and ratings, so the use case refuses an editor while the event or activity has
  any of them (`ParticipationDeletionRequiresAdministrator`).
- `[AllowOnlyStaff(StaffRole.Administrator)]` protects everything about people beyond the caller's own
  household: other users, signups, terms decisions, ratings, reports, every manual email, the diagnostic log,
  the legal policies and the inventory. The [audit log](#audit-log) is read only by the initial administrator
  and the account designated for data protection (the privacy officer).
- The initial administrator is the superadministrator: only it changes staff roles and designates the privacy
  officer (those endpoints admit administrators, but their use cases refuse anyone else with
  `SuperAdministratorOnly`, answered as `AccessDenied`), and only it deletes accounts other than a guardian's
  own dependents ([Account deletion](#account-deletion)).
- `[AllowOnlySelf]` accepts the target user, that user's guardian or an administrator, except on
  `PUT /api/users/{id}`, `POST /api/users/{id}/children` and
  `PATCH /api/activities/{activityId}/{userId}/assign` (`AdministratorsAllowed = false`): personal data,
  dependents, signups and the consents given with them change only through their holder or guardian, never an
  administrator, who can only record a [withdrawn consent](#terms-documents-and-decisions).
- Activity leadership grants one read, decided on every request from the session user's own signups:
  `GET /api/events/{eventId}/leader-roster` returns only the event's activities that have not ended and in
  which the caller holds a confirmed leader signup, each with its confirmed attendees by role. Anyone else,
  administrators and guardians of a leading dependent included, gets an empty list; the client sends no
  activity id. Each attendee carries only names and signup time, plus, for independent accounts, their own
  email and primary phone and, for dependents, their age on the activity's day (computed on the server) and
  the guardian's names, email and primary phone; never ids, DNI/NIE, birth dates or secondary phones. The SPA
  keys the cached list by user id.
- Only members may hold a leader signup; this is checked on signup and when an administrator changes the role
  of a signup. A person holds at most one signup per activity, enforced by the primary key (`user_id`,
  `activity_id`), so concurrent signups cannot leave a spare leader row and an administrator's change of status
  or role always hits the row they see.

#### Registration and account enumeration

- The email is the only login identifier and the only unique personal value. The DNI/NIE, the phone and
  the optional secondary phone are not unique, so no route checks them against other accounts and neither
  registration nor profile updates reveal whether someone else uses them; the secondary phone only has to
  differ from the same account's phone (`SecondaryPhoneSameAsPrimary`). `POST /api/auth/login` resolves the
  account by email only.
- No anonymous route tells whether an email has an account. `POST /api/auth/register` answers 204 to every
  valid request: a new address gets the verification link; an address whose account was verified gets a
  notice instead, saying that it already has an account and that nothing changed, and nothing is created;
  an address held by an account nobody verified gets the link of a new account that replaces the old one,
  which is erased keeping its data history, so registering someone else's email reserves nothing. The notice
  counts against the same budget as the verification mail it replaces. `POST /api/auth/resend-verification`
  (by email) and `forgot-password` also answer 204 alike.
- Before looking the address up, registration refuses invalid details and a disposable domain
  (`DisposableEmailNotAllowed`), which depend only on the request, and hashes the password, so every other
  outcome costs the same Argon2 work.
- An account still unverified 24 hours after registering is erased with its dependents, keeping their data
  history, by `PurgeUnverifiedAccounts` (hourly).
- Only someone who knows an account's password learns its state: after a correct password, the password step
  answers an unverified account with `UserAccountPendingVerification` and a locked second factor with
  `TwoFactorLocked`.

#### Staff roles and the data protection designation

- Giving a staff role (`PATCH /api/users/{id}/staff-role`) or designating the privacy officer
  (`POST /api/users/{id}/privacy-officer`) requires the initial administrator to re-enter their password (a
  stolen session cookie alone cannot promote another account); a wrong password returns
  `UserCurrentPasswordIncorrect` and changes nothing. Removing an account's role needs no password (the SPA
  asks for confirmation), but the body must state `staffRole`: a body without it is refused instead of being
  read as a revocation.
- Public registration never grants a staff role; the only account created with one is the
  [initial administrator](DEPLOYMENT.md#initial-administrator), whose random password is discarded, so there
  is no bootstrap password. That account can be neither demoted (`UserCannotRemoveInitialAdmin`) nor
  [deleted](#account-deletion), so the application always keeps an administrator; the SPA offers no role
  change for it.
- A dependent, who never signs in, never holds a role or the designation
  (`UserDependentCannotHoldStaffRole`), nor does an account whose email is unverified
  (`UserPendingVerificationCannotHoldStaffRole`); the SPA offers the actions only for independent accounts.
  Editors and administrators are members: a role is refused to any other user type and, while an account holds
  one, so is changing its type (`UserStaffRoleRequiresMember`). Only an administrator is designated for data
  protection, and the designated account stays an administrator until another account is designated
  (`UserPrivacyOfficerMustBeAdministrator`).
- Nobody changes their own role or type: on its own id, the initial administrator, the only account that
  changes roles, gets `UserSelfStaffRoleChangeForbidden` from `PATCH /api/users/{id}/staff-role` before any
  password is checked, and `PATCH /api/users/{id}/change-type` returns `UserSelfTypeChangeForbidden` to any
  caller; on the caller's row the SPA offers no role change and disables the type change.

#### Account changes and dependents

- `PUT /api/users/{id}` asks the caller (the user or their guardian) for their own password whenever the
  update would replace the account's email, phone or secondary phone. A missing or wrong password returns
  `UserCurrentPasswordIncorrect` and changes nothing; edits that leave all three untouched, a new DNI/NIE
  included, need none.
- Replacing the email also needs `code`, a current code of the caller's second factor (emailed by
  `POST /api/me/confirmation-code` or read from the authenticator), checked like a login code. A missing code
  (not counted as a failure) or a wrong one returns `TwoFactorCodeInvalid` and changes nothing, so a session
  and the password alone cannot move the login codes and reset links to another mailbox. Only holders replace
  an email: a dependent has none and administrators are refused.
- Holders who change their own email keep the current one until they open the link emailed to the new address
  (`PATCH /api/auth/{id}/confirm-email`, no session needed). The rest of the edit applies at once, nothing
  changes when the mail cannot be queued (`OtpResendCooldownActive`, `EmailSendFailed`), and a newer request
  replaces the pending one. Changing or resetting the password drops the pending address, so a change asked
  for with a stolen password does not survive the owner's reset, and confirming the new address voids the
  password reset and login codes already emailed to the previous one. `DiscardExpiredAccountChanges` (hourly)
  drops a pending address whose link expired, keeping its data history, and any authenticator enrollment
  nobody confirmed in time.
- When the new address belongs to a verified account, that account's holder gets the same notice as
  registration instead of the link, so the answer never tells whether the address is in use. An account nobody
  verified that holds the address is erased, keeping its data history, when the link is confirmed; one
  verified in the meantime keeps it (`UserEmailAlreadyInUse`).
- The stored account, never the request, decides what else applies. An account that is not already a
  dependent is refused a guardian (`UserParentNotAllowedForAdult`) and any birth date
  (`UserBirthDateNotAllowedForAdult`), must supply a DNI/NIE (`UserNationalIdRequired`) and may set
  `newsConsent`, so no request can demote an account into somebody's dependent. A dependent only accepts its
  own guardian, repeated or omitted (`UserParentReassignmentForbidden` otherwise), and is never reassigned; it
  must supply a birth date (`UserChildBirthDateRequired`), which has to be a minor's only when it differs from
  the stored value (`UserChildBirthDateNotMinor`), and never stores a DNI/NIE, news consent or password.
- Dependents are created by a registration (its `minors` list, at most 20, `RequestValidationFailed` beyond) or
  through `POST /api/users/{id}/children` (at most 20 per guardian, `UserChildLimitReached` beyond), and must
  be minors at creation; a household signup list holds at most 21 people. A dependent leaves the guardian when
  the guardian deletes them or when they come of age, since the guardian no longer represents them then:
  `PurgeAdultDependents` erases a dependent, keeping its data history, on the day they turn 18 in
  `APP_TIMEZONE` (1 March for a 29 February birth in a common year), and `SendComingOfAgeNotices` emails the
  guardian 30 days before, recording it in `users.coming_of_age_notice_sent_at`. A changed birth date clears
  that record, so the corrected day gets its own notice; a notice the email limits refuse is retried on the
  next run, and a guardian whose email nobody verified gets none. Both commands run hourly, one after the
  other.

### Two-factor authentication

Every login has two steps; there is no opt-out. `POST /api/auth/login` verifies the password and issues a
short-lived challenge cookie (`__Host-CodigoActivo.TwoFactor` in Production, 10 minutes, non-sliding) instead
of a session; it is bound to the password fingerprint and account status, so a password change invalidates it.
The session cookie is only issued by `POST /api/auth/login/two-factor`, which accepts the second factor,
records the login timestamp and stores the session in one transaction.

The challenge ticket is not self-sufficient either: the password step stores a random challenge id and the
time it opened on the account and puts the id in the ticket, so a copied challenge cookie is refused as soon as
that id changes or is cleared. A newer password step rotates the id; an accepted second factor, a
second-factor lockout, a password lockout, a password change or reset, a change of second factor or its reset
by an administrator, and signing out all clear it, so each challenge is single-use. The account itself also
refuses the challenge once its lifetime has passed, whatever the cookie says. Answering, reading or resending a
challenge needs the open challenge the cookie names on an account that may still sign in; the use cases check
both themselves, while the password fingerprint is only checked through the cookie. Reading or resending the
current challenge keeps working across reloads.

The account flows serialize on the account row only when they will act (`AccountLock`, see
[ARCHITECTURE.md](ARCHITECTURE.md#persistence)), so parallel requests each answer as they would alone: a
repeated request finds the cooldown and emails nothing, and a correct password whose account was reset or
locked since it was checked is answered like a wrong one (not counted) and opens no challenge.

Each user chooses one second factor from their account:

- **Email (default)**: a 6-digit code, emailed on the password step, stored as a keyed hash, expiring
  with the challenge, single use. The password step reuses a code sent within the last minute instead of
  resending; `POST /api/auth/login/two-factor/resend` enforces the same 60-second cooldown. These codes pass
  the [email guard](#email-abuse-controls) under the second-factor budgets.
- **Authenticator application**: RFC 6238 TOTP (HMAC-SHA1, 6 digits, 30-second steps, one step of clock
  drift) via Otp.NET; the project implements no cryptographic algorithm itself. Enrollment starts with the
  user's password and a current emailed code (`POST /api/me/confirmation-code`), stores the shared secret
  encrypted with ASP.NET Data Protection, and only activates once the first code is confirmed; an unconfirmed
  enrollment expires after 15 minutes. Each accepted time step is remembered so a code cannot be replayed.
  Enrollment URIs and shared keys are only returned to the authenticated owner over HTTPS and never logged.
  Returning to email requires the password and a current code. An account that already uses an authenticator
  cannot enroll another one (`AuthenticatorAlreadyEnabled`, before the password is checked) until it returns
  to email, so a session and the password alone cannot change the second factor.

Wrong codes are counted per account across logins, authenticator enrollment and removal, email changes, account
deletion and blocked data consultations (`SecondFactorGuard`): after 5 failures the second factor locks for
15 minutes (`TwoFactorLocked`) and, since only someone who knew the password could try those codes, the owner
is emailed a security alert telling them to change the password. An accepted login code, or the code that
returns the account to email, resets the counter; the other routes leave it as it is. These checks run with
the account row locked and reloaded, so parallel attempts are counted one by one and an emailed code or a time
step is accepted only once. Confirming a new authenticator enrollment is not counted: it needs the session and
the secret just issued to that session after its password and emailed code.

An administrator can reset a user's second factor to email (`POST /api/users/{id}/two-factor/reset`) after
re-entering their own password, which also lifts a second-factor lockout; the user is only notified when the
reset removed an authenticator. Nobody resets their own: on the caller's id it returns
`UserSelfTwoFactorResetRequiresVerification` before any password is checked, and the SPA offers no reset on
the caller's row. Losing the Data Protection keys makes stored authenticator secrets unreadable; affected users
then need the same administrator reset.

### Passwords and credential endpoints

Passwords require 12–128 characters and are hashed with Argon2id, never stored or logged in plaintext. Login
performs fallback Argon2 work for unknown identifiers to reduce timing differences. A wrong password for an
existing account also writes its failure count, so it is not timed exactly like an unknown address.

`PATCH /api/users/{id}/password` changes only the caller's own password and asks for the current one;
guardians and administrators get `AccessDenied` (403) for any other account, so nobody else can spend that
account's password attempts.

Emailed one-time codes and link tokens (login and confirmation codes, verification, password reset and
email-change links) are random, short-lived and stored only as HMAC-SHA256 under a server-side key
(`HmacOneTimeCodeHasher`), so a copy of the database alone cannot test guesses against them. That key is never
stored: it is derived from `DATA_PROTECTION_KEY` (see [Data Protection](#data-protection)).

Five consecutive wrong passwords for the same account, counted together by `PasswordAttemptGuard` across the
login password step and every route that asks for a password again, lock the account for 15 minutes
(`PasswordLockoutOptions`); one correct password before the limit clears the count, and once the lock expires
the count starts again. During the lock the login answers every password, right or wrong, with the same
`InvalidCredentials` after the same Argon2 work, the routes that ask for a password again refuse every
password too, and nothing is counted. Once the lock commits, the `PasswordLocked` listeners record it and email
its owner, even if the client has gone. The lock closes the open login challenge but leaves the account's
sessions working, so guessing at someone's password cannot sign them out. A completed password reset lifts it
at once (`forgot-password` keeps working), while an administrator's second-factor reset does not. The accepted
trade-off is that anyone who knows an account's email can keep it locked, 15 minutes at a time.

### Rate and concurrency limits

Credential routes (both login steps and code resend, registration, verification, email-change confirmation,
password reset and change, authenticator enrollment and removal, staff role grants and data protection
designations, second-factor resets, user updates, the confirmation code and self-service account deletion, the
two steps that open a blocked data consultation and both unsubscribe endpoints) have layered controls:

- nginx caps `login`, `register`, `forgot-password`, `resend-verification`,
  `/api/auth/{id}/(verify|reset-password|confirm-email)` and
  `/api/users/{id}/(password|staff-role|privacy-officer)` at 10 requests/second per client IP, burst 100, and
  the three that email anyone's address (`register`, `forgot-password`, `resend-verification`) also at one
  request/minute per client IP, burst 30, so one client cannot drain the shared budget of that mail. The
  remaining credential routes (two-factor login/resend/authenticator/email, `users/{id}`,
  `users/{id}/two-factor/reset`, `me/confirmation-code`, `me/deletion`, `blocked-data/consultations` and
  its `/code`, `emails/unsubscribe*`) rely only on nginx's general 200 requests/second limit and the API
  controls below.
- The API enforces 120 requests/minute per client IP in every environment on every credential route, about 60
  logins a minute behind one address, counted before the request waits for the Argon2 pool, so a client over
  it never holds a place in that queue.
- The credential routes that run Argon2 (`[PasswordHashing]`: the login password step, registration, password
  reset and change, and every route that asks for the password again) share a process-wide concurrency limit of
  4 executing requests and a FIFO queue of 64, which bounds the memory-hard Argon2 work; a queued request holds
  no Argon2 memory. These routes stay out of the process-wide pool below, so a request holding one of those 4
  permits never also waits behind the rest of the traffic. The other credential routes never wait behind them
  (`ApiConventionTests` checks the split).

Normal API traffic uses different limits, sized for about 100 people browsing at once behind one address (a
classroom, a venue's Wi-Fi, a carrier's CGNAT address), since an active person's SPA sends about 100
requests/minute, images included; the credential and mail limits above stay stricter on purpose. nginx allows
200 requests/second per IP, burst 1,000, of which the first 500 pass at once and the rest are paced at that
rate. The API applies a sliding one-minute budget of 600 requests per authenticated user, about twice what an
administrator paging through tables of thumbnails sends, or 10,000 per IP for anonymous traffic.

Report routes add a 60-request/minute budget per user (8 executing, 40 waiting); multipart file writes allow 30
requests/minute per user (4 executing, since only two uploads are decoded at a time, and 20 waiting); the
email send endpoints have [their own](#manual-email). Each request counts at most once against each budget,
whether it executes, waits or is refused, and before it waits for a concurrency permit
(`SinglePassChainedRateLimiter`). Process-wide, 64 requests execute and 2,048 wait in FIFO order, enough for
the refetch that 1,000 open event pages send together when signups open; a request waiting for a route's own
limit holds none of those 64.

Requests rejected by nginx or the API answer 429 `TooManyRequests` with `Retry-After`: 10 seconds when an API
budget refuses them (the soonest a sliding window returns a permit, `RetryAfterRateLimiter`) and 1 second
otherwise. These are per-instance controls; scaling API replicas multiplies aggregate capacity.

### CSRF

`CsrfValidationMiddleware` validates every unsafe HTTP method with ASP.NET antiforgery. The client obtains a
token from `GET /api/auth/csrf`, which also sets the `HttpOnly`, `SameSite=Lax` CSRF cookie
(`__Host-CodigoActivo.Csrf` in Production) and returns the token in its body; the client sends it back in
`X-CSRF-TOKEN`, which the SPA's HTTP client does automatically. A missing or invalid token answers 400
`InvalidCsrfToken`; the rejection is not logged, since the client refreshes the token and sends the request
again. The only exception is `POST /api/emails/unsubscribe/one-click` (`[RequireAntiforgeryToken(false)]`),
which mail clients call without cookies; it uses no session or other ambient credential, only the unforgeable
token in its own URL, so a forged cross-site request can do nothing that the token's holder could not already
do.

### Account deletion

Erasing one's own account requires the current password and the second factor on `POST /api/me/deletion`
(email code via `POST /api/me/confirmation-code`, or the authenticator code). `POST /api/me/confirmation-code`
checks the password first and emails the login code: same stored code, lifetime and cooldown, so a code sent
for any of its purposes (`AccountDeletion`, `EmailChange`, `AuthenticatorSetup`, which only change what the
email names) or for a login is accepted by the others, and each still needs the password.

Deletion signs the caller out and removes the user, their dependents and their signups, terms decisions and
[event ratings](#event-ratings). It is refused only to the initial administrator
(`UserDeleteInitialAdminForbidden`, also for a deletion code), before the password is checked; the SPA hides
the action from the initial administrator, which it recognizes from the session.

`DELETE /api/users/{id}` refuses the initial administrator and the caller's own id
(`UserSelfDeleteRequiresVerification`); the SPA disables that action on both rows. Only the initial
administrator deletes other accounts, administrators included, without re-entering a password, and a guardian
deletes their own dependents; any other administrator gets `AccessDenied`.

Every deletion credits the initial administrator instead of the household as author, last editor or uploader
of events, activities, news, partners, resources and files, and as the account that recorded a terms decision
about someone else, in the same transaction; the [data history](#data-history) keeps the original credit.
[ARCHITECTURE.md](ARCHITECTURE.md#persistence) describes `AccountEraser`, `ParticipationEraser` and
`InitialAdministratorGuard`. The SPA only tells users that they and their dependents lose access and cannot
recover the account.

### Input, output and error handling

- DataAnnotations validate the HTTP request models, and the same rules, together with the custom ones
  (`[HttpUrl]`, `[RichText]`, `[SpanishNationalId]`, `[NotDefaultOrFutureDate]`), are checked
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
  (`UserEmailInvalid`, answered as `RequestValidationFailed`).
- The SPA refuses to send a request whose path the browser would rewrite (dot segments, backslashes,
  stripped tabs), because generated paths interpolate their parameters unencoded. Its detail routes only
  match GUID ids, since Vue Router decodes `%2F`, `%3F` and `%23` in params, and it treats an emailed
  account link whose user id is not a GUID as incomplete, so a crafted link cannot steer a signed-in
  visitor's request to another endpoint.
- Expected failures use a string `ErrorCode`; all API failures return
  `ApiErrorResponse(Title, Status, Code, TraceId)` without stack traces or internal details (the 429 that
  nginx itself answers has no `TraceId`).
- User-supplied links allow only absolute HTTP(S) URLs without embedded credentials.
- Rich-text JSON (`[RichText]`, `RichTextAllowlist`) may only use the editor's nodes, marks and attributes,
  at most 50 levels deep and 5,000 nodes. Its links may point to absolute HTTP(S) URLs without credentials,
  `mailto:`, `tel:`, root-relative paths (never `//` or `/\`, which browsers read as another host) and
  fragments, and never contain control characters, which browsers drop (`/<tab>/host` is another host);
  embedded images may only reference this application's UUID-based file endpoint; colors are hex or
  `rgb()`/`rgba()`. The editor saves only what the API accepts and the SPA renders through the same
  allowlist.
- User-controlled values inserted into email templates are HTML-encoded; manual email bodies are plain text
  rendered inside the branded template, and their subjects stay on one line.
- Upload and attachment names keep only their last path segment, without control or invisible formatting
  characters, cut to 260 characters; an attachment declared `message/*` or `multipart/*` is sent as
  `application/octet-stream`.

### Files and multipart requests

Uploads are limited to 10 MiB; nginx also enforces a `12m` request-body limit and receives each body
completely before forwarding it, so a slow sender never holds an API request slot. Kestrel refuses any
other request body above 2 MiB, which no valid JSON request reaches; only the multipart upload and email
endpoints raise it, to the upload limit plus 64 KiB.

Only JPEG, PNG and WebP are accepted, detected from their first bytes. No upload is stored as sent:
`VipsImageProcessor` reads the dimensions from the header and refuses more than 50 megapixels before decoding
(`FileUploadDimensionsTooLarge`), keeps only the libvips JPEG, PNG and WebP loaders enabled, decodes at most
two uploads at a time, applies the EXIF orientation, converts to sRGB, drops every metadata block (EXIF with
any GPS position, XMP, IPTC, color profile) and stores two WebP variants: the content, at most 2048 × 2048
pixels, and a thumbnail at most 800 pixels wide, lossless when the image has an alpha channel. An upload no
loader decodes is refused (`FileUploadInvalidImage`). The name it was sent with is discarded too, since it may
name the people the image shows: the file is known only by its id, which the administration forms show, and
the rich text editor inserts images with an empty `alt`.

The variants are saved under `/app/files` in the `api-files` volume, each written to a temporary entry,
flushed to disk and only then moved to its name, which never replaces stored content. Files are served only by
the API, never from a static mount of the volume. Only editors and administrators upload or delete them, and
anyone who knows a file's id may read it (`GET /api/files/{id}/content` and `/thumbnail`). No route returns a
file's metadata; only the upload response does.

An upload is kept while content references it (`StoredFileRepository.GetInUseAsync`). Saving an event,
activity, news item, resource or partner checks that its thumbnail and the images its rich text embeds still
exist (`ContentFiles`, `*ThumbnailNotFound`, `RichTextFileNotFound`), and the foreign key of the thumbnail
keeps a purge from deleting it once it is referenced. An upload that nothing references 7 days after it was
uploaded, such as an image picked for a form that was never saved, is deleted with its content by
`PurgeUnusedFiles` (hourly). The same run then removes every entry of the volume that no file row owns once it
is an hour old: a write cut short, an upload whose row was never committed or content whose deletion failed.
A failed run logs `ScheduledJobFailed`, and content that cannot be removed from the volume logs
`StoredFileContentDeleteFailed`.

Manual email attachments never reach the `api-files` volume: they are read once per batch into the outbox
content, which Data Protection protects, and removed once every message referencing them is delivered or
discarded.

### Email abuse controls

#### Verification and codes

New accounts must always open an emailed verification link before their first login, and holders confirm
their own new email the same way; there is no switch. Mail to an address nobody has verified yet greets nobody,
so neither registration nor an email change relays a name typed by someone else to that mailbox, and opening the
verification link (`PATCH /api/auth/{id}/verify`) answers 204 without any of the account's data. The
verification email of a registration that accepted news says so.

Verification and password-reset codes expire after 15 minutes with a 60-second resend cooldown; an
email-change link also lasts 15 minutes, and each new one needs the password again. Login and confirmation
codes share storage and expire after 10 minutes with the same cooldown. SMTP must be
[configured](DEPLOYMENT.md#environment-variables) everywhere or the API refuses to start.

Activity signup sends no message; confirming or denying one queues an outcome email after commit, always to the
guardian address for a dependent. Delivery errors never roll back registration, password reset, activity
decisions or security changes.

Verification, email-change and password-reset links put the user id and a 256-bit code from a cryptographic
random generator in the URL fragment (`/reset-password#userId=…&code=…`), which browsers never send to the
server; the page reads and removes it from the address bar, so a reload needs the emailed link again.

#### Disposable domains

Registration and email changes (`PUT /api/users/{id}`) refuse addresses of disposable (temporary) mailbox
providers with `DisposableEmailNotAllowed` (400), which the SPA presents as a security measure. An address
is refused when its domain (lowercase, punycode, no trailing dot) or any parent domain other than the
top-level one is in `disposable_email_domains`; an unchanged email is not re-checked, so existing accounts
keep working. The list comes from the disposable-email-domains project on the schedule in
[DEPLOYMENT.md](DEPLOYMENT.md#disposable-email-domains), which also says what is accepted while no download
succeeds; addresses are only checked against the local copy. A download
replaces the stored list only when it is at most 2 MiB of UTF-8 text with one valid domain name per line
(blank and `#` lines ignored), holds at least 1,000 domains and would not refuse any domain in
`DisposableEmailDomainList.ProtectedDomains` (Gmail, Outlook/Hotmail, Yahoo, iCloud and Proton, plus
`Organization.WebDomain`).

#### Security alerts

`AccountSecurityNotifications` queues an alert to the affected account after commit, whoever caused the
change, when its password changes (by the user or through a reset), its second factor changes (authenticator
confirmed, returned to email, or removed by an administrator's reset), its staff role, its data protection
designation or its email or phones change, or its password or second factor is locked after wrong attempts.
The alert names the change and its timestamp and carries no code, secret or link that performs an action. An
email change is reported to the previous address once it applies (for a holder's own change, when the new
address is confirmed) and only ever quotes the new one masked; a phone change only states that the phone
changed, without quoting it. The email guard never refuses these alerts, a new alert about the same kind of
change replaces the one still waiting, and they leave at the pace of their recipient.

#### Email guard

Every code (second-factor, verification, email change, email-in-use notice and password reset) passes the email
guard in `ThrottledEmailSender` before it is stored, and a refused code is answered at once. Normalization
lowercases addresses, strips sub-address tags and, only for Gmail/Googlemail, folds dots. Each normalized
destination has three sets of burst/hourly/daily budgets, one for second-factor codes, one for password reset
links and one for the rest, so registrations naming an address never stop its login codes or reset links. The
mail anyone can ask for (everything but the second-factor codes) also shares one budget that never exceeds
half of `SMTP_MAX_PER_MINUTE` over an hour, so a flood of it never stops a login code.

Every other automatic email and every manual email is never refused by the guard: the outbox keeps one waiting
message per address and topic, and the delivery worker paces each recipient with the same burst/hourly/daily
budgets, so a loop of actions or a stolen administrator account delays a mailbox's mail instead of flooding
it. Codes leave first, and the daily limit of the provider keeps its last 10% for second-factor codes. Quota is
spent on attempt, before the message reaches the outbox; SMTP failure does not refund it. The budgets can be
neither disabled nor configured (`EmailGuardOptions`); they are in-memory and reset on restart.

| Limit                                                                                             | Value  |
| ------------------------------------------------------------------------------------------------- | ------ |
| Immediate messages allowed per normalized address and budget                                      | 20     |
| Sustained hourly limit per address and budget                                                     | 10     |
| Daily limit per address and budget                                                                | 50     |
| Immediate budget of mail anyone can ask for, at most (900 with the default `SMTP_MAX_PER_MINUTE`) | 1,000  |
| Sustained hourly budget of mail anyone can ask for, at most (900 with the default)                | 1,000  |
| Maximum address budgets held in memory                                                            | 50,000 |
| Idle-budget cleanup interval                                                                      | 5 min  |
| Minimum interval between repeated guard alerts or outbox failures                                 | 15 min |

#### Outbox

Both automatic and manual email wait in a PostgreSQL outbox until they are
[delivered](DEPLOYMENT.md#email-delivery). The stored subject, bodies and attachments are protected with
Data Protection (purpose `CodigoActivo.EmailOutbox.v1`); the recipient's address and name, the unsubscribe
link, the one-click unsubscribe address, the kind and the attachment names are stored in clear. Recreating the
Data Protection key ring makes pending rows unreadable: each one is removed and logged on its first delivery
attempt.

#### Manual email

Only administrators may send through `/api/emails/...`; recipients are resolved on the server from the same
filters as user/attendee lists, never client-supplied addresses. Each recipient gets a separate message.
Dependents without their own address are skipped, and so are accounts still pending verification, whose
address may be someone else's (a send to a single such account is refused with `EmailRecipientNotVerified`);
signup decision emails skip those addresses too. Every send carries a required `purpose`: `Notice` reaches
every addressable recipient; `News` reaches only those with news consent, enforced by the server
(`ManualEmailAudience.ReachedBy`; a send to a single recipient without it is refused with
`EmailRecipientWithoutConsent`).

`ManualEmailOptions` caps each send at 500 recipients and 10 attachments of 8 MiB in all, and attachments
remain transient. Single-recipient messages allow 30 requests/minute per administrator (10 executing, 10
waiting); bulk messages allow 5 requests/minute (2 executing, no waiting queue). All requests remain subject to
nginx's general API limit and the five-minute nginx upstream timeout. The email guard never refuses this mail;
a send reports how many messages were queued and how many recipients were skipped, not what SMTP later did
with them.

`GET /api/emails/users/audience` and `GET /api/emails/events/{eventId}/attendees/audience` let an
administrator preview a send before committing to it. Given the same filters as `POST /api/emails/users` or
`POST /api/emails/events/{eventId}/attendees`, they return `{ recipients, withoutConsent }`: the distinct
addressable recipients, deduplicated case-insensitively, that a notice reaches, and how many of them a news
send leaves out for lack of consent. Both come from the same shared selection (`ManualEmailAudience`) the send
commands use, so the preview always matches what a send would reach. These preview endpoints carry none of the
send limits above, only the general API limits of nginx and the API.

#### Unsubscribing

The footer of a news email carries the contact address (`LEGAL_EMAIL`, the same one the site shows) and the
recipient's own unsubscribe link, `/unsubscribe#token=…`. The token is the user id protected with Data
Protection (purpose `CodigoActivo.Unsubscribe.v1`), so it cannot be forged and never expires while its key
stays in the ring; recreating the ring invalidates the links already sent. The stored content is shared by the
batch and each outbox row holds its recipient's link in clear (`unsubscribe_url`), put in place at delivery.
The SPA page posts the token to the anonymous `POST /api/emails/unsubscribe` (credential rate limit), which
withdraws the consent, answers 204 also for a consent already withdrawn or an erased account, and answers
`EmailUnsubscribeLinkInvalid` for an altered token.

Each news message also carries the one-click unsubscribe of RFC 8058:
`List-Unsubscribe: <…/api/emails/unsubscribe/one-click?token=…>` with the same token (stored per row in
`one_click_unsubscribe_url`) and `List-Unsubscribe-Post: List-Unsubscribe=One-Click`. The mail client posts
that pair as `application/x-www-form-urlencoded` or `multipart/form-data`, without cookies or an antiforgery
token ([CSRF](#csrf) explains why that is safe); the endpoint is anonymous, kept out of the OpenAPI document,
shares the credential rate limit, answers 204 without redirecting, and records the withdrawal as
`OneClickUnsubscribe`. The token in that URL never reaches a [log](#logging): nginx keeps no access log, and
the API keeps nothing below `Warning`, so ASP.NET's `Information` request lines are never stored.

### Transport and proxy trust

TLS ends at an external proxy; its requirements (overwriting forwarded headers, redirecting plain HTTP, the
`Host` header, IPv4 only) are in [DEPLOYMENT.md](DEPLOYMENT.md#tls-and-proxy-boundary). The API accepts one
forwarded hop and honors `X-Forwarded-For`/`X-Forwarded-Proto` only from IPv4 loopback and private ranges
(`127.0.0.0/8`, `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`). nginx takes the client address from
`X-Forwarded-For` only from peers in those same ranges, but reads `X-Forwarded-Proto` from any peer and counts
only `https`, compared case-insensitively, as HTTPS. IPv6 stays disabled on both networks and nginx answers 403
to IPv6 clients, because Docker would relay IPv6 clients from its bridge gateway, a private address whose
`X-Forwarded-For` nginx trusts.

nginx sends HSTS (`max-age=63072000; includeSubDomains`, no `preload`) only when its normalized scheme is
HTTPS; `includeSubDomains` requires every subdomain of the public host to work over HTTPS only. It also sends
CSP, frame denial, MIME sniffing, referrer, permissions, `Cross-Origin-Opener-Policy: same-origin` and
`Cross-Origin-Resource-Policy: same-origin` headers (no `Cross-Origin-Embedder-Policy`).
`Referrer-Policy: same-origin` keeps page URLs out of requests to external sites, and
`X-DNS-Prefetch-Control: off` stops browsers from pre-resolving link destinations. API responses whose body is
`application/json` or `application/problem+json` also carry `X-Robots-Tag: noindex, nofollow`, so search
engines do not index API payloads. Kestrel does not emit a `Server` header (`AddServerHeader=false`); nginx
sends `Server: nginx` without a version (`server_tokens off`).

### Data Protection

The API persists ASP.NET Data Protection keys in `api-dataprotection` and protects them the same way in every
environment. The secret of every key in the ring is stored as an
`urn:codigoactivo:data-protection:aes-gcm:v1` XML element carrying a random nonce, the authentication tag and
the ciphertext of `System.Security.Cryptography.AesGcm`. The AES-256-GCM key that encrypts it is derived with
HKDF-SHA256 from `DATA_PROTECTION_KEY` (`DataProtectionRootKey`), 32 random bytes that never reach the volume
and need no stretching; the one-time code key comes from the same root key under another purpose label.

Reading the ring ignores, and logs as `DataProtectionKeysIgnored`, any key whose secret is stored in clear or
names another decryptor, so whoever can write to the volume without `DATA_PROTECTION_KEY` cannot plant a usable
key. A start fails when the ring holds an element that `DATA_PROTECTION_KEY` does not decrypt, so a wrong key
never replaces the ring. There is no rotation procedure: changing `DATA_PROTECTION_KEY` means recreating the
volume, which invalidates every session, every stored authenticator secret and every pending one-time code or
link, and leaves the data history unreadable.

Everything Data Protection protects (session and two-factor cookies, antiforgery tokens, authenticator
secrets, unsubscribe tokens, the email outbox content and the data history) is encrypted with AES-256-GCM.

### Privacy and third parties

Personal data processing follows the GDPR and the LOPDGDD, images of people follow LO 1/1982, and the
representation of minors and the event conditions follow the Spanish Civil Code. Policies, documentation and
code cite only these laws.

- Browsers load nothing from third parties: fonts are bundled from Fontsource and served from the application
  origin; the CSP allows scripts, styles, fonts, images and connections only from `'self'` (plus inline styles
  and `data:`/`blob:` images), sets `base-uri 'none'` and adds `upgrade-insecure-requests` only when nginx's
  normalized scheme is HTTPS. Do not add CDNs, analytics or embeds without a legal basis and consent review.
  The API itself downloads the public [disposable email domain list](#disposable-domains), a request that
  carries no user data.
- The only cookies are the session, two-factor challenge and CSRF cookies; the theme choice stays in
  `localStorage` and is never sent to the server, and `sessionStorage` only holds the stale-build reload mark.
  They are all needed to run the site or keep a preference the user picks, and of the cookies only the
  persistent session, which exists only when the user ticks its checkbox, outlives the browser session, so the
  site shows no cookie banner. The `/cookies` page shows the cookie policy the administrators publish, which
  must list every cookie and storage key with its purpose and duration; ask them to publish a new version
  whenever one of these changes.
- The `/privacy` page shows the data controller from the `LEGAL_*` settings and then the privacy policy the
  administrators publish, which must state, for each processing of personal data, the data, purpose, legal
  basis and retention, plus the processors, transfers and rights; the registration form shows the first layer
  of that information (`PrivacyNotice`). Change the notice and ask for a new version of the policy with any
  new personal field, processor, recipient or retention. Both policies keep every published version
  (`legal_policy_versions`), so the text in force on any date can be shown.
- `GET /api/me/personal-data` (report rate limit) gives the signed-in user a copy of their own and their
  dependents' personal data (articles 15 and 20 GDPR), which «Mi cuenta» saves as a JSON file with Spanish
  names and labels. It names and links the privacy policy version in force, which gives the information of
  article 15.1, and reads only the caller's household. It holds:
  - the account with any unconfirmed new email, the news consent history and the dependents;
  - the household's signups and the terms decisions about the household with every change, who took it and
    the signature kept for it, withdrawn acceptances included;
  - the user's event ratings and the open sessions;
  - the audit entries the user ran or that name a household member as main or related entity, logins before
    the session included (time, action, outcome, kind of actor and client IP, without the IP when an
    administrator ran it and never what else the entry touched).

  It never holds password or code hashes, authenticator secrets, the login challenge or lockout counters. It
  leaves out the data history, which nobody may use (LOPDGDD article 32); the email outbox, which only holds a
  message until it is delivered and may carry one-time codes; and the authorship of content, which is answered
  on request.
- News consent (articles 6.1.a and 7 GDPR) is changed only by its holder: at registration, in «Mi cuenta», or
  through the unsubscribe link of a news email or its mail client's unsubscribe button. `User` appends every
  change to `news_consent_changes` with its instant and source (`Registration`, `Profile`, `UnsubscribeLink`,
  `OneClickUnsubscribe`), and the data history keeps those changes once the account is erased.
- Only adults register, and only a parent or legal guardian, who represents the minor (Civil Code articles
  162 and 225), adds a minor: `POST /api/auth/register` refuses a body without `declaresAdult`
  (`UserAdultDeclarationRequired`) before looking the address up, and every minor, added at registration or
  through `POST /api/users/{id}/children`, needs `declaresGuardianship` (`UserGuardianshipDeclarationRequired`).
  `users.adult_declared_at` and `users.guardianship_declared_at` keep when each was declared, also in the
  data history. The SPA sends `declaresAdult` once the visitor confirms being of age at its age gate and
  `declaresGuardianship` with every minor, since each form that adds one states that adding it is that
  declaration. The initial administrator, who never registers, has neither.

### Terms documents and decisions

- A terms document holds either conditions or a consent (`terms_documents.kind`). Decisions are recorded per
  person signed up, keyed by event, person and document, with who took them (`event_terms_acceptances.user_id`
  and `decided_by_id`): an adult decides for themselves; for a dependent, the guardian decides the conditions
  and, before the age of 14, the consents, and from the age of 14 the dependent decides their own consents
  on the guardian's screen. Article 3.1 LO 1/1982 leaves the consent of a minor to their maturity without
  setting an age; the association applies to every consent, images included, the age of 14 that article 7
  LOPDGDD sets for data processing. `TermsConsent.DeciderOf` names the decider from the birth date, never
  from the request.
- A consent is never required: an event that links one as required is refused (`EventTermsConsentRequired`),
  and `TermsGate` never blocks a signup on a consent. An acceptance is never re-asked and holds until its
  consent is withdrawn; a rejection or a withdrawal is revisable and the stored row takes who accepts it now;
  a required document's rejection is never persisted, so it blocks that signup without excluding the person
  for good. `AssignActivity` and `AssignHousehold` run `TermsGate` for every person they sign up, each with
  their own decisions, since only the person or their guardian signs them up.
- The row is the current state: each change of it (the first decision, a later acceptance, a withdrawal) also
  appends an entry to `terms_decision_changes` with whether it accepts, who decided, the account that recorded
  it (the signed-in user, an administrator included), when and the signature, so each earlier state can still
  be proven (article 7.1 GDPR). The rows and their history are deleted with the person, the decider or the
  event, each leaving its data history.
- A consent is withdrawn (article 7.3 GDPR) through
  `POST /api/events/{eventId}/terms/decisions/{userId}/{termsDocumentId}/withdraw` (`[AllowOnlySelf]`): by the
  person, by their guardian, or by an administrator who records a withdrawal asked for by email. Conditions are
  not withdrawn (`EventTermsWithdrawalNotAllowed`; leaving means canceling the signup) and a consent that is not
  currently given answers `EventTermsConsentNotGiven`. The withdrawal is decided by whoever
  `TermsConsent.DeciderOf` names and recorded by the caller; the acceptance it ends stays in the history with
  its signature, since withdrawing does not make earlier processing unlawful. «Mi cuenta» lists the household's
  consents (`GET /api/me/consents`) and withdraws a given one with one confirmation, as easily as it was given;
  the event's decisions tab shows it as withdrawn and lets an administrator record one.
- The withdrawal is committed with its notice pending, and within a minute `SendConsentWithdrawalNotices`
  emails the legal contact (`LEGAL_EMAIL`; without it nothing is sent and the notice keeps waiting) so what the
  consent covered stops being used and is removed from the site and the social profiles. Several withdrawals go
  in one email, a withdrawal accepted again before the notice needs none, and a failure only delays the notice:
  it neither undoes the withdrawal nor loses the notice.
- Erasing an account (by its holder, a guardian or the initial administrator, or on coming of age) or an event
  deletes its terms decisions, which then stay only in the [data history](#data-history), blocked: nobody may
  consult them to tell whether a published image or name is still covered. So the erasure, in its own
  transaction and before deleting them, also emails the legal contact every given consent it deletes about an
  event that has already started (`ConsentWithdrawalNotifier.NotifyErasedAsync`, at most 50 per email, of the
  same kind as the withdrawal notice), so what they covered stops being used as well. Consents about an event
  still to come are left out, since nothing of it can have been published.
- A terms document is versioned (`terms_documents.version`), so the text each decision was taken on can always
  be shown (article 7.1 GDPR): every rewrite that changes its name, text, kind or signature requirement keeps
  the replaced version in `terms_document_versions` and numbers the new one, even when nobody decided yet, so a
  decision recorded while the document changes still names a version whose text is kept. Its kind cannot change
  while an event links it or anyone decided on it (`TermsDocumentKindInUse`), since what a decision requires
  and whether it can be withdrawn follow the current kind. Each decision and each change of its history stores
  the version it was taken on (`terms_document_version`; a withdrawal keeps the one withdrawn), and nobody is
  asked again after a rewrite. Every decision sent names the version shown (`TermsDecisionRequest.Version`,
  which the terms state returns); one on a version that is no longer current is refused
  (`EventTermsDocumentChanged`) and the SPA reloads the documents and asks again. Texts are compared as JSON,
  so saving without changes keeps the version.
- A terms document can ask for a handwritten signature (`requires_signature`): accepting it then needs a PNG
  drawn on screen (`TermsConsent` refuses the acceptance without one). Every consent a dependent accepts from
  the age of 14 needs one too, whatever the document asks, so the acceptance taken on the guardian's screen
  carries the minor's own signature. The signature is a PNG of at most 128 KiB and 2000 × 1000 pixels
  (`HandwrittenSignature` refuses a larger one), stored in `event_terms_acceptances.signature`, never in the
  public file storage. Only administrators read it
  (`GET /api/events/{eventId}/terms/decisions/{userId}/{termsDocumentId}/signature`, answered `no-store`).
- Terms documents are text only: their editor has no image option and drops images from loaded or pasted
  content, and the API refuses a description with an image node (`RequestValidationFailed`).

### Event ratings

`event_ratings` keeps one rating per account and event (unique `event_id`, `user_id`), without timestamps, in
these columns: `id` (random v4), `event_id`, `user_id`, `score` (1–5, or null when only answers were written),
`most_liked`, `least_liked` and `suggestions`. The account that wrote it is its author, also when it rates for
a dependent. `PUT /api/events/{eventId}/rating` requires the event to exist and have ended, the caller or a
dependent to have a confirmed signup in one of its activities (`EventRatingAttendanceRequired`) and the rating
to carry a score or an answer (`EventRatingEmpty`); the first call stores the rating and later ones replace
it, and a concurrent first save answers `ConcurrentUpdate`. `DELETE /api/events/{eventId}/rating` removes the
caller's own rating at any time (`EventRatingNotFound` without one), keeping its data history. The event
history (`GET /api/me/event-history`) returns the caller's rating of each event so the form reopens with it,
and the personal data copy includes them.

Administrators read ratings (`GET /api/events/{eventId}/ratings` and the event statistics) without their
authors: no response projects `user_id`. Saving or deleting a rating is never audited (`IUnaudited`), since
an audit entry would tie the account to the event or the rating id the list returns. Anyone with database
access can still read the author.

### Data history

The blocked data (LOPDGDD article 32) is the data history: every save adds one `data_history` entry per row of a
historized entity it changes or deletes, with the row as it was before, in the same transaction
([ARCHITECTURE.md](ARCHITECTURE.md#persistence) describes the recorder). A change never commits without its
history and the history never keeps a change that did not commit. Additions record nothing, since the row
itself is the record, and neither does a change of excluded properties alone.

- **What**: each configuration declares `HasHistory()` or `HasNoHistory()` (`DataHistoryModelTests`).
  Historized: users, signups, terms decisions and their history, news consent changes, ratings, events,
  activities, terms documents and their versions, news, partners, resources and file metadata (never the file).
  `ExcludedFromHistory()` keeps out the password hash, every code (an email change keeps only the address asked
  for), authenticator secrets, the login challenge, lockouts, the last login and the row version.
- **Coverage**: historized rows change only through the change tracker, the only writes the recorder sees, so
  none escapes the history; the model and `DataHistoryConventionTests` enforce it.
- **Entry**: `id` (UUID v7), `entity`, `occurred_at`, `subject_ids` and `payload`. `subject_ids` holds every
  `UserId` of the row, as GUIDs: the account it is, its guardian, the person a signup or decision is for,
  whoever decided, authored or last updated it; never the signed-in account that changed it nor the one that
  recorded a decision (`NotHistorySubject()`), so an extract about an administrator does not carry every row
  they changed about others. `payload` holds the operation, the actor, the command (`ICommandScope`, set by the
  audit decorator) and the previous values, encrypted with the Data Protection key ring under
  `CodigoActivo.DataHistory.v1` and the entry id, so a payload moved to another row does not decrypt. A copy of
  the database without `DATA_PROTECTION_KEY` and the key ring reveals nothing.
- **Retention** (`DataHistoryRetentionOptions`): 5 years, the term of the personal actions on the event
  conditions (Civil Code article 1964.2), which also outlasts image and honor claims (LO 1/1982 article 9.5);
  3 years for news consent changes and ratings, when the very serious infringements prescribe (LOPDGDD article
  72). A claim interrupts those terms (Civil Code article 1973), so the purge keeps every entry about an
  account an active [hold](#blocked-data-consultations) names, whatever its age, until the hold is lifted.
  `PurgeDataHistory` runs hourly; it also removes the consultations kept 5 years and the holds lifted 5 years
  ago. Nothing else deletes entries, and the recorder refuses to change one.

### Blocked data consultations

Only the privacy officer reads the history: the active administrator designated for data protection
(`users.is_privacy_officer`, `PrivacyOfficer`), whichever second factor it uses. Exactly one account is
designated at any time: the initial administrator from the first start, then whichever account it designates
(`POST /api/users/{id}/privacy-officer`, open only to the initial administrator), which takes the designation
from the previous one in one transaction; a unique filtered index refuses a second. The initial administrator
takes it back by designating itself, and erasing the designated account returns it to the initial
administrator in the erasure's transaction. `GET /api/blocked-data/access` tells the signed-in administrator
whether they are the privacy officer; every administrator reaches the lists of consultations and holds, and
only the officer opens a consultation.

- `POST /api/blocked-data/consultations` (credential rate limit) opens a consultation for 15 minutes. It takes
  who asks for the data and the reference of the request, both typed by the officer, plus the current password
  and a code of the second factor, counted like a login code and spent by the opening. An emailed code comes
  from `POST /api/blocked-data/consultations/code`, which checks the password first and shares the stored login
  code, lifetime and cooldown, like the account deletion code. The consultation is opened with its notice
  pending, and `SendBlockedDataConsultationNotices` emails `LEGAL_EMAIL` within a minute, so an access nobody
  expected is noticed.
- `POST .../{id}/searches` takes an identifier, email, DNI/NIE, phone or part of the name (at least three
  characters) in the body, never in the URL. It finds existing accounts and erased ones (decrypting only past
  states of accounts), adds their dependents, and returns each with only its id, name and guardian, whether it
  exists or when it was erased, and how many entries of each kind concern it, counted without decrypting. The
  consultation records the accounts found, never the text.
- `POST .../{id}/extracts` decrypts the entries about an account the consultation found and its dependents,
  narrowed to days and kinds of data; adds the current personal data of an existing account, of those kinds
  alone (the days only narrow the history) and without its sessions or audit entries; and records the extract
  (account, filters, moment and number of entries, enough to find the same entries again). An entry that no
  longer decrypts is returned without content and logged (`DataHistoryEntryUnreadable`).
- `POST .../{id}/holds` places a hold for an open claim on an account the consultation found: it names that
  account and its dependents, existing and erased, without a foreign key, and copies the requester and the
  reference of the consultation. `POST .../{id}/holds/{holdId}/lift`, from any open consultation, lifts it once
  the claim is closed (a second lift answers `BlockedDataHoldLifted`); lifting lets the next purge destroy what
  the claim needed, so it takes the same password, second factor and notice as reading.
  `GET /api/blocked-data/holds` lists the holds with how many accounts each keeps, never which. Erasing an
  account is never refused for a hold: the erasure only adds to the history the hold keeps.
- `GET /api/blocked-data/consultations` lists who consulted, when, for what request, how many accounts were
  found and how many extracts taken, without any of the data. A consultation keeps its officer without a
  foreign key, so it stays listed after that account is erased.

Only the consultation handlers, their `BlockedDataMinors` helper and the access query, which reads only the
names of the historized entities, use `IDataHistoryReader` (`DataHistoryConventionTests`).

### Logging

The API keeps two logs: the diagnostic log, read by administrators, and the [audit log](#audit-log). The
diagnostic log follows GDPR data minimization: only failures and warnings an operator has to diagnose are
recorded, so normal use writes nothing to it, and none of its entries outlives its retention (30 days). It
never holds names, emails, phone numbers, anything typed by the client, the requested path or query, client
IP, user agent, cookies, tokens or one-time codes. It holds counters, enum/kind values, error/SMTP/HTTP codes,
the HTTP method, the matched endpoint's route template, and exception type/message/stack traces. No event
template carries an id, but an exception message may quote the pseudonymous GUID of a row or stored file it
failed on, such as the path of a file under `/app/files`; the message is written as is.

- **Events**: every event is declared in one of four `*Log` classes (`SecurityLog` and `ApplicationLog` in
  Application, `InfrastructureLog` and `ApiLog`) with `[LoggerMessage]`, an allowed-placeholder list and a
  level of `Warning` or above; [backend/AGENTS.md](backend/AGENTS.md) has the conventions, which `CA1848` and
  the architecture tests `LoggingConventionTests`/`LoggingCallSiteTests` enforce. There is no per-request
  logging middleware: unhandled exceptions are recorded only by `GlobalExceptionHandler`, with the HTTP method
  and the route template, never the requested path. Expected outcomes are not logged: a concurrent update,
  unique-key race or deadlock refused with 409, or a rejected CSRF token. A use case is logged as slow only
  above 5 seconds; repeated email delivery failures are throttled as
  [DEPLOYMENT.md](DEPLOYMENT.md#email-delivery) describes. `NormalUseLoggingTests` drives anonymous, member and
  administrator flows and fails if any entry passes the policy. EF Core sensitive-data logging stays disabled;
  `SmtpEmailSender` strips the recipient and server reply out of SMTP exceptions before they are logged.
- **Levels**: `Logging:LogLevel` in `appsettings.json` is the level policy for both the database and the
  console. `Default` is `Warning`, which also drops the framework's start and stop messages.
  `Microsoft.EntityFrameworkCore` is `None`: every EF Core failure is rethrown and recorded where it is caught,
  and its own entries would only repeat it, quote the SQL of a failing command (which can carry literal values)
  and report expected unique-key races and deadlocks as errors. `Logging__LogLevel__<Category>` overrides a
  category, but Compose forwards none; raising a framework category such as `Microsoft.AspNetCore` to
  `Information` would store requested paths and query strings in the diagnostic log.
- **Security events**: only two events exist, both `Warning` and free of any user id:
  `PasswordLockoutTriggered` and `TwoFactorLockoutTriggered` (an account or its second factor was locked;
  written once the lock commits). Frequent ones mean someone keeps guessing passwords or codes; no manual
  unlock is needed, and the audit log names the accounts (`PasswordLocked`, `TwoFactorLocked`). Logins,
  logouts, password changes and staff role changes are not diagnostic events: the audit log records them.
- **Storage**: every event the level policy lets through is stored in the `diagnostic_log_entries` table of the
  application database by a batching Serilog sink (`DatabaseLoggerProvider`): the UTC instant, the level, the
  category, the event name, the rendered message (up to 4,000 characters), the exception with its stack trace
  (up to 32,000), the trace id and the values of the message template. Properties of logging scopes, such as
  the requested path, are dropped, and the API generates every trace id: a client-supplied `traceparent` is
  ignored. Logging never affects a request: emitting only enqueues (at most 10,000 events; beyond that they are
  dropped). Batches of up to 1,000 are written every 2 seconds with a binary `COPY` over a pool of three
  connections of its own (`codigoactivo-logs`, short timeouts, no logger). A row the database refuses is
  dropped alone, and a batch that cannot be written is retried with backoff for 2 minutes and then dropped.
- **Console**: Development also writes to the console. The fatal startup event
  (`CodigoActivo.Lifecycle The API host terminated unexpectedly`) always goes to the console regardless of the
  level policy, and to the table when the host was built; the process exits non-zero. Failures of the logging
  pipeline itself go to standard error. Nothing about logging is needed for the API to start.
- **Container logs**: `db` and `web` use `logging: driver: none`; `api` keeps a `json-file` log of two 1 MB
  files, which in Production only receives the fatal startup event and failures of the logging pipeline.
- **nginx**: `access_log off` and `error_log` writes to `/dev/stderr` at `crit`, so no request or error is
  stored; a failed API shows in `docker compose ps`, and the 5xx it returns is logged by the API itself.
- **PostgreSQL**: its logging collector writes one file per weekday, `postgresql-<Mon..Sun>.log` (UTC), into
  the `logs-db` volume at the default `log_min_messages=warning`, which also keeps the few `LOG` lines of each
  start and stop. `log_error_verbosity=terse` drops the `DETAIL` lines that quote row values,
  `log_min_error_statement=panic` keeps the failing SQL out, and checkpoint and autovacuum reports are off.
  The first start of an empty database also leaves the `__EFMigrationsHistory does not exist` error of the
  migrator. Messages written before the collector starts go to the discarded stderr. A known residual: a
  `FATAL` authentication failure names the attempted role; `db` is reachable only from the internal `backend`
  network. PostgreSQL rotates its files at midnight with `log_truncate_on_rotation=on`, emptying the file of
  the new weekday, so `logs-db` holds at most 7 days while `db` runs. A day that starts while `db` is stopped
  is appended to its old file instead, which then keeps entries from a week earlier.
- **Retention**: fixed in code (`LogRetentionOptions`), 30 days for the diagnostic log and 60 days for the
  audit log. The log tables are partitioned by UTC day (`<table>_pYYYYMMDD`). At startup, right after the
  migrations, and then every hour (`LogRetentionWorker`), `LogPartitionMaintainer` creates the partitions of
  today and the next two days, drops the partitions whose whole day is older than the cutoff and deletes the
  older rows of the partition the cutoff falls in. The cutoff is the retention minus one hour, so no entry
  outlives the retention of its table, and a release that lowers a retention removes the older entries when the
  API starts. Each statement runs in a short transaction with a 5-second lock timeout; a failed run is logged as
  `LogMaintenanceFailed` (`Error`) and never stops the API. Purging stops while the API is stopped, so the
  effective retention lengthens by however long it is down.
- **What the readers see**: the diagnostic log shows administrators trace ids, route templates and the GUIDs
  that exception messages quote; the audit log shows its readers client IPs and the names of the accounts and
  things each command named, or that they were removed since; a consultation of the blocked data is shown only
  by its kind.

### Audit log

The audit log keeps what protects the site or resolves an incident in `audit_log_entries`, through the same
batching pipeline and failure guarantees as the diagnostic log: a failure to record never changes a result.
Every entry, written by `AuditWriter`, holds the instant, who caused it (a user with their account id, an
anonymous caller or the system), the caller's IP as the trusted proxy forwarded it (the address the rate
limits use, see [Transport and proxy trust](#transport-and-proxy-trust)), the action, the main entity and the
other entities the command named, by type and GUID, how it ended (succeeded, rejected with the error code, or
failed) and the trace id that error responses show as their reference. It never stores anything typed by the
client: names, emails, passwords, one-time codes, signatures, secrets and free text stay out, and so does the
requested path. Login and password reset requests record the account the email belongs to, so failed logins
are attributed to their account.

- **Recorded**: every command, as `AuditCommandDecorator` sees it, unless a rule below limits it; the reads that
  hand over personal data on request (`IAuditedQuery`, `AuditQueryDecorator`), once they succeeded: the copy of
  an account, a signature, the roster and the badges of an event; and what the application detects by itself:
  an account or a second factor locked by wrong attempts (`PasswordLocked`, `TwoFactorLocked`) and a signed-in
  account an authorization attribute refused (`AccessDenied`, without the endpoint, since no path is stored).
- **Never recorded** (`IUnaudited`): saving or deleting an event rating, and what says nothing about security or
  an incident: abandoning a pending login, resending a login code or a verification link, featuring an event
  or a news item and the housekeeping of expired sessions, expired account changes and unused uploads.
- **Recorded in part**: scheduled purges and notices when they changed something (`IScheduledCommand`); a
  password reset request when its email has an account (`IAccountLookupCommand`); the steps that only prepare
  a change another command records when refused (`IPreparatoryCommand`: starting an authenticator
  enrollment, asking for the code that confirms a change of the own account or opens a consultation); and the
  everyday commands on content and signups (`IRoutineCommand`), refused only when the caller lacked permission,
  since a closed activity or a lost concurrent edit changes nothing. A command that proves a credential is
  never routine (`AuditConventionTests`).
- **Not recorded on purpose**: the everyday reads of people (the user and attendee lists, the terms decisions)
  and of the audit log itself, where every page and filter would add an entry.

An extract of the blocked data or a hold on it never names its account (`[Unaudited]`), so only the
privacy officer knows whose blocked data was handed over or held: neither the other administrators nor the
person concerned read it in the audit log or the personal data copy (LOPDGDD article 32). The account ids have
no foreign key, so an erased account's entries remain until the audit retention ends; they are not part of the
data history.

`GET /api/logs/diagnostics` and `/api/logs/diagnostics/{id}` require the administrator role, and
`GET /api/logs/audit` the initial administrator or the privacy officer
(`[AllowOnlySuperAdministrator(PrivacyOfficerAllowed = true)]`), so the other administrators do not watch
who did what to whom; the three share the report rate limit.

### Demo mode

[Demo deployments](DEPLOYMENT.md#demo-mode) are disposable and must never contain private data. Their invented
accounts have a random, hashed and discarded 32-byte password and fictitious addresses, so only the initial
administrator can sign in; the second factor stays required. Seeding generates its sample images locally, so
it sends no request outside the deployment, and the callers of the invented log history use the IP ranges
reserved for documentation (`192.0.2.0/24`, `198.51.100.0/24`, `203.0.113.0/24`, `2001:db8::/32`).
