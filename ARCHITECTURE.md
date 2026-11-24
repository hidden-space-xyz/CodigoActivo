# Architecture

`<Codigoactivo/>` is one product composed of two applications:

- `backend/`: an ASP.NET Core Web API on .NET 10.
- `frontend/`: a Vue 3 and TypeScript single-page application (SPA) built with Vite.

They are deployed same-origin. The browser calls relative `/api/...` URLs; Vite proxies them in local
development, nginx proxies them in Docker. `/sitemap.xml` and `/robots.txt` are also forwarded to API
endpoints that generate content from `APP_BASE_URL` and public data. There is no CORS configuration;
cross-origin access is not part of the design.

Runtime and environment details belong in [DEPLOYMENT.md](DEPLOYMENT.md). Authentication and trust boundaries
belong in [SECURITY.md](SECURITY.md).

## Domain language

Documentation and the UI use these terms. The schema, the wire contract and part of the code name some of them
differently: `Assignment*`, `ParentId`, `PrivacyOfficer`, the `assign` and `children` routes, the
`ActivityAssignment*` and `UserChild*` codes, and use cases such as `AssignActivity` and `AddChild`. The
participant role of an activity (`ActivityRole`) is not the participant user type (`UserType`).

| Term                  | Meaning                                                                                                                                          | In code                                        | In the UI                          |
| --------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------- | ---------------------------------- |
| Event                 | A dated gathering with categories, terms documents and activities                                                                                | `Event`                                        | evento                             |
| Activity              | A scheduled session of an event with a desired number of people per role                                                                         | `Activity`                                     | actividad                          |
| Role                  | What a person does in an activity: leader, volunteer or participant                                                                              | `ActivityRole`                                 | rol                                |
| Signup                | A person's place in an activity with a role and a status: requested, confirmed or denied                                                         | `Assignment`                                   | inscripción                        |
| Attendee              | A person with a signup in an event; lists filter by status                                                                                       | `EventAttendee*` (read side)                   | asistente                          |
| Guardian              | An independent account that cares for dependents                                                                                                 | `User`                                         | tutor                              |
| Dependent             | A person created as a minor, who never signs in and is cared for by a guardian                                                                   | `User` with `ParentId`, `UserStatus.Dependent` | menor                              |
| Household             | A guardian and their dependents                                                                                                                  | `Household`                                    | unidad familiar                    |
| Account               | The credentials, second factor and sessions of a person who signs in                                                                             | `Accounts` feature                             | cuenta                             |
| Staff role            | What an account may do in the administration: nothing, as an editor or as an administrator                                                       | `StaffRole`                                    | cargo                              |
| Initial administrator | The administrator created on the first start, the only account that changes staff roles and deletes other accounts                               | `InitialAdministrator`                         | superadministrador                 |
| Privacy officer       | The administrator designated for data protection, the only account that consults the blocked data                                                | `User.IsPrivacyOfficer`                        | responsable de protección de datos |
| Blocked data          | The data history: the previous state of every changed or deleted row of a historized entity, kept encrypted and read only through a consultation | `data_history`, `BlockedData*`                 | datos bloqueados                   |
| Notice                | A manual email about the recipient's account, signups or activities; reaches every verified address                                              | `ManualEmailPurpose.Notice`                    | aviso                              |
| News                  | A manual email about upcoming events and news; reaches only verified addresses with news consent                                                 | `ManualEmailPurpose.News`, `User.NewsConsent`  | novedades                          |
| Inventory             | Categories of belongings with the columns administrators define and the entries that fill them                                                   | `InventoryCategory`, `InventoryEntry`          | inventariado                       |

## Backend

The backend follows Clean Architecture with a DDD domain model and a CQRS split, organized by feature in every
layer. It has one PostgreSQL database, with a write model of aggregates and a read model of its own over the
same tables, and publishes domain events in process after each commit. It has no mediator, message bus,
transactional outbox for events or event sourcing.

### Project dependencies

| Project                       | Responsibility                                                                                                                                                                                                                                                                                                                                                          | Direct project dependencies         |
| ----------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------- |
| `CodigoActivo.Domain`         | Aggregates, typed identifiers, value objects, domain events and policies, repository ports, `Result`, `Error` and `DomainErrorCode`                                                                                                                                                                                                                                     | None                                |
| `CodigoActivo.Application`    | Use cases with their validated messages, response contracts, the read model, the use case decorators and the ports they need                                                                                                                                                                                                                                            | Domain                              |
| `CodigoActivo.Infrastructure` | EF Core write and read contexts, repositories, the unit of work and event publisher, the caching decorator, the encrypted data history, file storage and image re-encoding (NetVips), Argon2id, HMAC-SHA256 one-time code hashing, TOTP (Otp.NET), the encrypted Data Protection key ring, SMTP, email templates, the clock and the log tables with their Serilog sinks | Domain, Application                 |
| `CodigoActivo.Composition`    | The composition root: registrations by feature, strict configuration-to-options mapping checked on start, Data Protection and database initialization                                                                                                                                                                                                                   | Domain, Application, Infrastructure |
| `CodigoActivo.API`            | HTTP controllers and request contracts by feature, the wire `ErrorCode`, middleware, claims and cookies, rate and concurrency limits, output caching, OpenAPI, SEO documents and startup                                                                                                                                                                                | Composition                         |

The `ProjectReference` graph is the source of truth for these boundaries.
`backend/tests/CodigoActivo.ArchitectureTests` enforces the layer and feature dependencies (the API reaches
neither Infrastructure nor EF Core) and further conventions over the compiled assemblies and the sources;
[backend/AGENTS.md](backend/AGENTS.md) lists the conventions to follow when writing code.

### Domain model

- `CodigoActivo.Domain` has one folder per feature (`Users/`, `Events/`, `Activities/`, `EventCategories/`,
  `TermsDocuments/`, `TermsDecisions/`, `News/`, `Resources/`, `Partners/`, `Files/`, `Legal/`, `BlockedData/`,
  `Inventory/`) with its aggregates, typed identifiers, value objects, domain events, policies and repository
  ports; `Common/` holds `Result`, `Error`, `DomainErrorCode`, the base classes (`Entity<TId>`,
  `AggregateRoot<TId>`, `AuditableEntity<TId>`) and the shared value objects.
- Domain declares only the repository ports of its aggregates. Technical ports (`IClock`, `IPasswordHasher`,
  `IOneTimeCodeHasher`, `ITotpService`, `ISecretProtector`, `IFileStorage`, `IAuditTrail`, `IRequestOrigin`,
  the email, persistence and read ports) live in `Application/Abstractions` or in the feature that consumes
  them.
- The aggregates are `User` (a dependent is another `User` referenced by `ParentId`), `UserSession`,
  `BlockedDataConsultation` (with its findings and extracts), `BlockedDataHold` (with the accounts it keeps),
  `Event` (with its categories and terms links), `Activity` (with its role capacities and signups),
  `EventTermsAcceptance`, `EventRating`, `NewsItem`, `Resource`, `Partner`, `TermsDocument`, `LegalPolicy`,
  `EventCategoryType`, `StoredFile` and `InventoryCategory` (with its columns, their options and its entries).
  Their state has private setters: factories create them, returning `Result<T>` when a rule can fail, and
  behavior methods own the invariants and the transitions (`User.Verify`, `User.RecordPasswordFailure`,
  `Activity.RequestAssignment`, `Activity.ChangeAssignmentStatus`, `Event.Feature`). Child entities change only
  through their root. In `User`, `CreateIndependent`, `CreateDependent` and
  `PlanProfileChange`/`ApplyProfileChange` decide which details an independent account or a dependent needs and
  answer each broken rule with one `DomainErrorCode`.
- Aggregates have a typed identifier (`UserId`, `EventId`, `ActivityId`, `PartnerId`, `StoredFileId`...), a
  `readonly record struct` over a `Guid` with no implicit conversion, so an identifier of one aggregate can never
  be passed for another. `EventTermsAcceptance` has none of its own: it is keyed by event, user and document.
- Aggregates reference each other by ID, never through navigations. The EF configuration declares the foreign
  keys and their cascades with `HasOne<T>().WithMany().HasForeignKey(...)`; a foreign key whose cascade would
  change another aggregate's children restricts instead, so those children change through their root.
- Values with rules are value objects: `EmailAddress`, `PhoneNumber`, `SpanishNationalId`, `RichText`,
  `HandwrittenSignature`, `InventoryValues` (stored as `jsonb`) and the security state of `User`, which is
  `OneTimeCode` (verification, password reset and login codes), `EmailChange`, `Authenticator`,
  `LoginChallenge`, `PasswordLockout` and `TwoFactorLockout`. EF Core maps them and the identifiers onto plain
  columns through conventions and converters (`Infrastructure/Database/Conversions`), the `User` ones as
  complex properties; the read model keeps plain `Guid` and primitive columns.
- `DateRange` (the event calendar), `SignupWindow` and `ActivitySchedule` are value objects that `Event` and
  `Activity` build from their own date columns. `DateRange.InstantsIn` gives the UTC bounds of the event days;
  `DateRange.Covers` (a new activity schedule) and `IActivityRepository.AnyOutsideRangeAsync` (a shortened
  event) both compare activities against them.
- Rules that span aggregates are domain policies (`SignupEligibility`, `SignupRoles`, `EarlySignup`,
  `EventTimeline`, `AssignmentDecisions`, `TermsConsent`, `InitialAdministrator`, `Household`);
  `EventTimeline` also decides the stage the read side shows, so it always agrees with signup. Application
  collaborators only load what a policy needs or run the checks that need I/O (`SignupGate`, `TermsGate`,
  `ActivityValidator`, `EventCategoryChecker`, `DisposableEmailChecker`).
- The closed value sets (user status and type, activity role and modality, signup status and resource type) are
  domain enums (`UserStatus`, `UserType`, `ActivityRole`, `ActivityModality`, `AssignmentStatus`,
  `ResourceType`). Their catalog tables stay only for the names, colors and descriptions shown to people; each
  member keeps the stable `Guid` of its row through `CatalogIds` (`Application/Common/Catalogs`), which the EF
  converters use to store the enum in the foreign key column and the API uses to accept and return catalog
  identifiers. A request whose identifier names no member is invalid (`RequestValidationFailed`), and the API
  maps the identifier to the enum before building the command, so commands never carry catalog identifiers.
  The exceptions are `SendEmailToUsers` and `SendEmailToEventAttendees`, which carry the filters of the list
  they send from (`UserListQuery`, `EventAttendeeListQuery`) to read the same audience. The other domain enums
  are stored as strings.
- Rich text references a stored file only by its content URL `/api/files/{id}/content`, which saved documents
  keep and the frontend builds and checks; `RichTextFileReferences` in Domain owns that format, so it cannot
  change without migrating saved documents.
- An upload is never stored as sent: `IImageProcessor` (libvips through NetVips) re-encodes it as two WebP
  variants (`FileVariant`), the content that pages and rich text show and the thumbnail at
  `/api/files/{id}/thumbnail` that lists, cards and logos show.

### Domain events

- Aggregates record domain events for the changes other parts react to (`PartnerUpdated`, `NewsItemDeleted`,
  `AssignmentStatusChanged`, `PasswordChanged`, `ContactDetailsReplaced`, `AccountErased`...). `UnitOfWork`
  collects them when it saves and publishes them only after the commit, or after the explicit transaction the
  save ran in commits; a failed commit publishes nothing and keeps them with the changes they describe.
- Each `ICommittedEventsHandler` sees the whole commit once (`CacheInvalidationOnCommit` evicts the tags
  `CacheTagsByEvent` maps; `ReleasedFilesCleanup` removes the files that events implementing `IReleasesFiles`
  stopped referencing, when nothing else references them) and then every `IDomainEventListener<T>` of each
  event runs (`AssignmentDecisionNotification`, `AccountSecurityNotifications`,
  `SessionsEndOnPasswordReplaced`, `AccountLockoutLog`).
- These handlers and listeners are the effects of a commit. They run in the request after the commit and to
  their end even when the client disconnects; a failing effect is logged and neither stops the others nor fails
  the committed request. A crash between the commit and an effect loses that effect, because there is no
  transactional outbox for events (emails themselves go through the email outbox).
- Emails that must never be lost are state instead of events: the aggregate marks them pending in the same
  commit, and a scheduled command sends them and clears the mark.

### Persistence

- Infrastructure is organized by feature too: each feature folder (`Users/`, `Events/`, `Activities/`, `News/`,
  `Files/`...) holds its EF configurations, repositories and background services; `Database/` keeps the
  contexts, conventions, catalogs, shared configurations and repository base, the unit of work, the query
  executor, seeders and migrations; `Communication/` holds the email delivery shared by every feature,
  `DataHistory/` the encrypted data history (recorder, reader and purge), and `Caching/`, `Messaging/`,
  `Security/`, `Time/`, `Diagnostics/` and `Logs/` (the partitioned log tables, their own connection pool and the
  Serilog sinks that write them) the other technical services.
- EF Core uses Npgsql and snake-case names. Identifiers are generated by the application, never by the
  database.
- Repositories exist only for aggregate roots (`IRepository<T>` plus methods that name their intention), never
  expose `IQueryable` or predicates, and share the scoped `CodigoActivoDbContext`. `AddAsync` and `Remove` only
  stage changes; the few methods that write immediately, without waiting for the unit of work, say so by their
  verb (`Save…`, `End…`, `Purge…`).
- `IUnitOfWork.SaveChangesAsync` commits staged changes once and turns a PostgreSQL unique violation into
  `UniqueConstraintViolationException`, naming the entity whose table rejected the commit. A save that finds a
  row it updates or deletes changed or removed meanwhile, for any entity, throws `ConcurrentUpdateException`
  instead. Races between staff or between a scheduled job and a request are rare enough that only the database
  guards them. The API answers both exceptions with 409 `ConcurrentUpdate` unless a handler answers them itself.
- `IUnitOfWork.ExecuteInTransactionAsync` runs work in one explicit transaction. When the work throws, the
  transaction rolls back and the domain events raised in it are discarded; a deadlock that PostgreSQL resolves
  by aborting the transaction surfaces as `ConcurrentUpdateException`.
- Users carry a concurrency token mapped onto the `xmin` system column (no column of their own), so a save or
  deletion of a user that another transaction wrote since it was read is refused instead of mixing both writes.
  The credential, code and challenge flows of an account, which an attacker can run in parallel, go through
  `AccountLock`: they decide on the account as loaded and, only when they will act, lock and reload it and check
  again before they write. They never hash a password with the row locked. Concurrent requests for one account
  therefore run one after another, and the token only backs them up.
- `PasswordAttemptGuard` counts a wrong password in an explicit transaction with the account row locked
  (`FOR UPDATE`), so parallel attempts lose no increment and only one of them locks the account.
  `SavePasswordStateAsync` writes only the lockout and login challenge columns (a lock closes the challenge),
  so it hands just the `PasswordLocked` event to the unit of work, which publishes it when that transaction
  commits; the account's other changes and events wait for their own commit. It advances the account's token
  only when no other transaction wrote the row since it was loaded. Second-factor checks lock and reload the
  account the same way (`IUserRepository.LockAsync`), token included, before `SecondFactorGuard` counts the
  code.
- A person holds at most one signup per activity (primary key on `user_id`, `activity_id`); the signup commands
  answer a concurrent duplicate with `ActivityAssignmentAlreadyExists` (409).
- At most one event and one news item are featured (unique index on `featured`, filtered to featured rows).
  PostgreSQL checks it row by row and EF Core cannot order moving the flag between rows within one save, so
  `SetEventFeatured` and `SetNewsItemFeatured` save the unfeaturing of the current item before featuring the
  chosen one, in one transaction; a concurrent featuring that loses the race fails instead of leaving two items
  featured.
- Event category type, inventory category and terms document names are unique ignoring letter case: the unique
  index and the name check both compare with the nondeterministic ICU collation `case_insensitive`.
- Users are deleted only through `AccountEraser`, used by `DeleteUser`, `DeleteOwnAccount`, the account purges
  and `EmailClaims`, which replaces an unverified account whose email a registration or a confirmed email change
  takes. It refuses the initial administrator and then, in one `ExecuteInTransactionAsync` transaction:
  - locks the account (`FOR UPDATE`) and loads its household and what belongs to it, signups, terms decisions
    and ratings included (`IAccountErasureStore`);
  - hands the content credited to the household over to the initial administrator;
  - discards the email still waiting about its members, whatever address it waits for
    (`EmailTopics.AboutPersonSuffix`);
  - removes the account; any other change staged in the unit of work commits with it.
- `InitialAdministratorGuard`, a `SaveChangesInterceptor` that `CodigoActivoDbContext` adds to itself, refuses
  any deletion of the initial administrator.
- Deleting an event, an activity or a rating, or canceling a signup, commits through `ParticipationEraser`,
  which loads what the removal takes away (`IParticipationErasureStore`) and removes it in one transaction.
- Historized entities change only through the change tracker: `DataHistoryRecorder`, the other interceptor
  `CodigoActivoDbContext` adds to itself, keeps the previous state of every historized row a save changes or
  deletes, encrypted, in `data_history`. Their dependents therefore use `ClientCascade` (the database refuses to
  delete a row whose historized dependents were not loaded) and are loaded before their principal is deleted.
  `DataHistoryConventionTests` forbids `ExecuteUpdate`/`ExecuteDelete` on historized types, except the password
  state `UserRepository` writes, which the history excludes, and limits raw SQL to a reviewed list of types that
  only lock, read or write tables without history. What the history holds, how long it is kept and who reads
  it are in [SECURITY.md](SECURITY.md#data-history).
- Scheduled commands apply the retention periods that [SECURITY.md](SECURITY.md) states (`PurgeDataHistory`,
  `PurgeUnverifiedAccounts`, `PurgeAdultDependents`, `PurgeUnusedFiles`, `RemoveExpiredSessions` and
  `DiscardExpiredAccountChanges`), erasing accounts through `AccountEraser` and uploads through
  `IOrphanFileCleaner`, and send the emails the aggregates left pending (`SendComingOfAgeNotices`,
  `SendConsentWithdrawalNotices` and `SendBlockedDataConsultationNotices`). `ScheduledJobsWorker` only runs
  them on the schedule each feature registers (`ScheduledJob`): first 30 seconds after startup, then every
  hour, or every minute for the two notices to the legal contact.

### Commands and queries

Each use case is a message and a sealed handler in `CodigoActivo.Application/<Feature>/Commands|Queries/`.
`UseCaseRegistration` (Composition) registers every `ICommandHandler<,>`, `IQueryHandler<,>`,
`IDomainEventListener<>` and `ICommittedEventsHandler` it finds by the contract it implements, and wraps the
handlers in decorators: a command runs through `LoggingCommandDecorator` (warns about slow use cases),
`AuditCommandDecorator` (records it in the audit log), `ValidationCommandDecorator` and
`UnitOfWorkCommandDecorator` (commits once the handler succeeds); a query through `LoggingQueryDecorator`,
`AuditQueryDecorator` (records an `IAuditedQuery`), `ValidationQueryDecorator` and `CachingQueryDecorator`.
Controllers inject the handler contract (`ICommandHandler<TCommand, TResult>`) with `[FromServices]`, the only
way an action receives a service (implicit service binding is disabled). `Accounts` groups its supporting
types by concern: `Passwords/`, `SecondFactor/`, `Sessions/`, `EmailAddresses/`, `Notifications/` and
`Erasure/`; `AccountLock`, `AccountLockoutLog`, `SecurityLog` and `PersonalDataReader` sit at the `Accounts`
root. `Activities` keeps the signup collaborators in `Signups/` and `ParticipationEraser` in `Erasure/`.

Commands and queries are application messages, not HTTP contracts. They hold typed identifiers and domain
values, and their fields carry the DataAnnotations of their rules (`[property: Required, MaxLength(200)]`).
`MessageValidator` checks them, nested messages included, before any command handler runs and before any query
handler whose result can report the failure (`Result`); the criteria of list queries, which cannot report it,
are checked by MVC when the request binds them. The current day reaches the date rules through the validation
context, never through a service locator.

The HTTP request records live in the API (`API/<Feature>/Contracts/`) with only the standard attributes the
OpenAPI document describes, plus the catalog membership check (`IValidatableObject` through
`CatalogIdentifiers`), and map themselves to the command (`request.ToCommand(id)`). Response records and list
criteria stay in `Application/<Feature>/Contracts/` as the read contracts of the queries; the list criteria
share the binding primitives of `Application/Common/Querying` (`PageQuery`, `SortMap`, `TextSearch`), and day
filters turn a local day into UTC instants with `LocalDayRange` (Domain). Mapping is handwritten. `Gender`,
`TwoFactorMethod`, `StaffRole`, `TermsDocumentKind`, `LegalPolicyKind` and `InventoryColumnType` are contract
enums that mirror the domain ones (`ContractEnums`); they live with the read rows that store them, so the read
model depends on no feature.

Queries read the database through `IReadStore`: no-tracking `IQueryable` sources of read rows
(`Application/Abstractions/Querying/ReadModel`) that `CodigoActivoReadDbContext` maps onto the same tables;
that context is excluded from migrations. Queries project to response shapes in the database, materialize
through `IQueryExecutor`, and never depend on repositories or domain entities beyond static domain rules such
as `User.GivesOwnConsent`. There are two exceptions. The dashboard port `IDashboardCountsReader`
(`Application/Reports`, plain SQL in `Infrastructure/Reports`) counts whole tables in one round trip and
creation times per chart bucket with `width_bucket`. The log port `ILogEntryReader` (`Application/Logs`,
`Infrastructure/Logs/LogEntryReader`) reads the partitioned log tables through read rows of its own in the read
context, filters them on their trigram, containment and B-tree indexes and looks up the names of the accounts
and things an audit page mentions, one query per kind. A query that may be [cached](#caching) implements
`ICachedQuery` (duration, tags and key) and `CachingQueryDecorator` keeps its result in `HybridCache`. Queries
never mutate, commit or invalidate caches.

Commands load aggregates through repositories, call the domain and stage the changes; the decorator commits them
and the committed domain events evict caches and run the effects. A handler saves on its own only when it must
react to the outcome of the save (a unique violation answered as a conflict, file content to compensate) or
when several saves form one transaction. `ICommand<TResult>` requires `TResult` to be a `Result`, so the
decorators can refuse and commit every command alike. Commands return `Result`, or `Result<TId>` with the typed
ID of what they create; they return data only when it exists nowhere else after the operation, such as the
authenticator secret of `BeginAuthenticatorSetup`, the queued and skipped recipient counts of `SendEmail*`
(`EmailDispatch`) or the number of rows a scheduled purge removed. The controller then runs the query that
builds the HTTP response. Commands never call query handlers; they and the listeners may read the read store
only for lookups that change nothing, each one reviewed in `HandlerConventionTests`: email audiences, the names
a notification shows, the guardianship `ActingUserPolicy` checks, and the accounts a blocked data consultation
searches, exports or holds, with their personal data copy.

### Auditing

`AuditCommandDecorator` hands the commands worth keeping to `AuditWriter`, which builds the `AuditRecord` for
`IAuditTrail` (`Application/Abstractions/Auditing`) with:

- the actor: `ICurrentUser`, the system for an `IScheduledCommand`, otherwise an anonymous caller;
- the caller address: `IRequestOrigin`, implemented by `HttpRequestOrigin` in the API;
- the `AuditAction`, named after the command;
- the main entity: the one the handler named through `IAuditSubject` (as login does with the account it found
  by email), else the typed ID a `Result<TId>` created, else the first typed ID of the command;
- the other typed IDs, as related entities;
- the outcome.

`AuditQueryDecorator` does the same for a query marked `IAuditedQuery`, `AccountLockoutLog` for the locks of an
account, and the access denied event of the session cookie for a refused account. `MessageAudit` reads only
the typed IDs, never another value; `IUnaudited` keeps a command out, `[Unaudited]` a property, and
`IScheduledCommand`, `IPreparatoryCommand`, `IAccountLookupCommand` and `IRoutineCommand` limit the outcomes
recorded. `SerilogAuditTrail` (Infrastructure) queues the record to `audit_log_entries`. What an entry holds,
what stays out and why are in [SECURITY.md](SECURITY.md#audit-log).

### Authorization in use cases

Use cases decide who may run them on whose behalf; who may do what is in
[SECURITY.md](SECURITY.md#authentication-and-authorization). `ICurrentUser` (implemented by `HttpCurrentUser`
in the API) tells who is signed in; commands and queries take the author, the acting user and whether that
user is an administrator from it, never from their input. `GetAccountQuery` is the one query that reads an
account by an id it is given: the controller answers a completed two-factor login with it, before the session
exists.

`ActingUserPolicy` lets the signed-in user act for themselves or one of their dependents, and an administrator
for anyone; it answers anything else with `ActingForAnotherUserForbidden` (403 `AccessDenied` on the wire). It
reads the guardianship from the read side, so commands and queries share it. Updating a user, adding a
dependent and signing a person up are narrower: they accept only the person or their guardian, administrators
included (`EnsureMayManagePersonalDataAsync`). Signing up a household takes only the signed-in user and their
own dependents (`Household.Includes`) and answers anyone else with `ActivityHouseholdMemberNotAllowed`.

The `[AllowOnlySelf]`, `[AllowOnlyStaff]` and `[AllowOnlySuperAdministrator]` attributes refuse early at the
HTTP edge. `[AllowOnlySelf]` asks the same policy; `[AllowOnlyStaff]` asks the role claims the session carries
for the account's `StaffRole` (an administrator's claims include the editor's) and its data protection
designation; `[AllowOnlySuperAdministrator]` admits only the initial administrator and, with
`PrivacyOfficerAllowed`, the privacy officer.

### Terms, legal policies and inventory

An event can link several terms documents (`event_terms_documents`, each `is_required`/`display_order`); a
document holds conditions or a consent (`kind`) and may ask for a handwritten signature (`requires_signature`).
The signup wire contract carries a `TermsDecisions` list of `{TermsDocumentId, Accepted, Signature, Version}`
for the person in `AssignRequest` and one per member in each `AssignHouseholdRequest` assignment; the signature
is a base64 PNG drawn in the SPA with `signature_pad`. `TermsGate` applies `TermsConsent` person by person and
records each decision as an `EventTermsAcceptance` about that person; the acceptance keeps every change of the
decision as a `TermsDecisionChange`, and a rewritten document keeps the replaced version as a
`TermsDocumentVersion`. `GET /api/events/{eventId}/terms` returns every linked document and, for the caller and
each dependent, the decisions taken and whether they decide their own consents. Administrators list the
decisions of an event under `/api/events/{eventId}/terms/decisions`. A withdrawal (`WithdrawTermsConsent`)
leaves its email to the legal contact pending (`WithdrawalNoticePending`, kept out of the data history) for
`SendConsentWithdrawalNotices`. Who decides, what each kind requires, withdrawals, versions and signatures are
in [SECURITY.md](SECURITY.md#terms-documents-and-decisions).

The privacy and cookie policies are `LegalPolicy` aggregates, at most one per `LegalPolicyKind` (unique `kind`);
administrators write their text-only rich text in «Políticas» (`/admin/legal`), and demo mode seeds a short
generic example of each. `PublishLegalPolicy` (`PUT /api/legal-policies/{kind}`) creates the policy on its
first publication; later ones keep the version they replace as a `LegalPolicyVersion` with its publication
date, and publishing the same content changes nothing. Anyone reads the version in force from
`GET /api/legal-policies/{kind}` (404 `LegalPolicyNotFound` until it is published); the response is output
cached under the `legal-policies` tag, which `LegalPolicyPublished` evicts. Administrators list every version
under `/api/legal-policies/{kind}/versions` and read one under `/api/legal-policies/{kind}/versions/{version}`.

The inventory («Inventariado», `/admin/inventory`) is a set of `InventoryCategory` aggregates. Administrators
define the columns of each category, each with a name, whether it is required and a type: text, long text,
number (at most two decimals and `InventoryColumn.NumberMaxValue`, which a double holds exactly), date, yes or
no, a list of options with their colors, or quantity. A category has at most one quantity column; its total
adds that column up, or counts the entries when there is none. An entry keeps the quantity in its own column
and the other values in `InventoryValues`, keyed by column id and written in the canonical form of their type.
`Redefine` changes the columns in one step and refuses what would break the stored entries: a new type for a
column with values, a removed option an entry chose, or a required column some entry leaves empty.
`GET /api/inventory/categories/{id}/entries` returns every entry of a category, at most
`InventoryCategory.MaxEntries`, and the SPA filters, sorts and pages them through its server table source.

### Results, validation and HTTP errors

Expected failures are values, not exceptions: command and route-addressed query handlers return `Result` or
`Result<T>` with an `Error` whose code is a `DomainErrorCode` (rules of the model) or an `ApplicationErrorCode`
(use case outcomes such as `UserNotFound`); neither knows HTTP. A query addressed to a resource by route, lists
of its children included, answers that resource's `NotFound` when it does not exist; a filter such as
`?eventId=` only narrows a list. The one expected failure that travels as an exception is a commit the database
refuses for a concurrent write (see [Persistence](#persistence)), which `GlobalExceptionHandler` answers 409
`ConcurrentUpdate` without logging it.

The API owns the wire `ErrorCode` (`API/Errors`): `WireErrorCodes` translates each code to the wire member of
the same name or to an explicit rename (malformed input becomes `RequestValidationFailed`, acting for another
user `AccessDenied`), and `WireErrorCodesTests` fails when a code has no translation or a wire code is reached
by none. `ErrorCode` is serialized as a string and is part of the frontend contract; the API registers the
string enum converter for MVC and for the responses it writes directly, since the Domain carries no
serialization attributes.

Controllers translate failures through `ApiControllerBase`. All client-visible failures use
`ApiErrorResponse(Title, Status, Code, TraceId)`, including model validation, authorization, CSRF, rate-limit
rejections (`TooManyRequests` with `Retry-After`) and unhandled exceptions. HTTP mappings: 400 validation, 401
unauthenticated, 403 forbidden, 404 not found, 409 conflict, 429 too many requests, 500 unexpected. A path
under `/api` that matches no endpoint answers 404 `EndpointNotFound` to anyone. Every create answers 201 with
the new resource, plus a `Location` header when an endpoint reads that resource alone.

### Configuration and startup

Each kind of value has one home. Deployment configuration (addresses, credentials, SMTP, the demo choice and
the association's legal identity and contact details, which stay out of the public repository) consists of the
flat uppercase variables of `.env` and `secrets/`. The composition root (`AddCodigoActivo`) reads them once into
options registered with `AddValidatedOptions` and `ValidateOnStart`: a missing optional setting takes its
default, and a missing required setting or any setting that is present but unusable stops the application at
start with a message naming the key. Security, abuse and retention values are fixed in their `*Options`
classes, registered with `AddFixedOptions` so no setting reaches them (tests replace the instance). The
association's public identity is `Organization` (Domain) and `shared/config/organization.ts` (frontend).
User-facing text is in the frontend i18n resources and, for the backend, in
`Application/Common/Localization/AppStrings.resx`; logs and seeded content are not localization resources.
Data Protection is registered in the composition root once, with the keys protected at rest in every
environment.

Startup (`ApplicationInitializer`) validates the configuration, verifies that `DATA_PROTECTION_KEY` decrypts
the key ring and refuses a demo mode other than the locked one before it writes anything. It then applies
migrations, prepares the log partitions, seeds catalogs and, when the user table is empty, creates the initial
administrator (`InitialAdministrator.Id`, the first privacy officer); it refuses a database that has users but
not that account. Only then does it lock the selected demo mode and, when enabled, add demo data, a replayed
data history and an invented log history. [DEPLOYMENT.md](DEPLOYMENT.md#first-production-start) describes the
same sequence for operators.

### Email boundaries

Application decides which email is sent, to whom and with which data, through typed models and the composer
ports `IAccountEmailComposer`, `ISignupEmailComposer`, `IManualEmailComposer`, `IConsentWithdrawalEmailComposer`
and `IBlockedDataEmailComposer`; Infrastructure renders the HTML, the branding and the links to the SPA
(`Infrastructure/Communication/Templates`).

Automatic messages (`IEmailSender`/`ThrottledEmailSender`, which applies the
[email guard](SECURITY.md#email-abuse-controls) to codes only) and manual email (`ManualEmailDispatcher`) both
store their batch in the PostgreSQL outbox through `IEmailOutbox`, in a unit of work of its own that commits
before the call returns. Application names what a message is about with `EmailMessage.Topic` (`EmailTopics`),
so a new message replaces the one waiting on the same topic and address, and says with `ValidFor` how long a
code stays valid. The `EmailOutboxDeliveryWorker` hosted service claims due rows and delivers them through one
`IEmailTransport` session per run (`SmtpEmailSender`), paced by `EmailDeliveryPacer`. The fault that
`EmailDeliveryException` names decides whether a message is dropped or retried or the whole delivery pauses,
and a message expires after its `ValidFor` or the lifetime of its kind. The emails to the legal contact are
sent by scheduled commands (`SendConsentWithdrawalNotices`, `SendBlockedDataConsultationNotices`) that ask
`IPendingEmails` whether their previous email is still waiting. Operational values are in
[DEPLOYMENT.md](DEPLOYMENT.md#email-delivery).

The send commands (`SendEmailToUsers`, `SendEmailToEventAttendees`) and the audience-preview queries in
`Application/Emails/Queries` share recipient selection through `ManualEmailAudience`, so a preview always
matches what the corresponding send would reach; the send's `ManualEmailPurpose` decides whether recipients
without news consent are left out. A news batch keeps one shared content whose footer leaves a slot for the
unsubscribe link: `ManualEmailComposer` gives each `EmailRecipient` its own link (a token from
`IUnsubscribeTokens`), the outbox row stores it, and `EmailLayout.LinkUnsubscribe` puts it in place at
delivery. The same token forms the recipient's one-click unsubscribe address (RFC 8058), which travels on the
outbox row to `EmailMessage.OneClickUnsubscribeUrl` and which `SmtpEmailSender` sends in the
`List-Unsubscribe` and `List-Unsubscribe-Post` headers. The SPA page and the mail client both end in
`UnsubscribeCommand`, which records which of them withdrew the consent. The token, the endpoints and their
limits are in [SECURITY.md](SECURITY.md#unsubscribing).

Registration and email changes ask `DisposableEmailChecker` whether an address belongs to a disposable
mailbox provider; it looks the domain and its parent domains (`EmailDomains`) up in
`disposable_email_domains` through `IDisposableEmailDomainLookup`. The `DisposableEmailDomainRefreshWorker`
hosted service keeps that table filled from an external list, replacing it only with a download that
`DisposableEmailDomainList` validated ([DEPLOYMENT.md](DEPLOYMENT.md#disposable-email-domains)).

## Frontend

The frontend follows Feature-Sliced Design 2.1, pages first. Imports flow downward; slices at the same layer do
not import one another (the only exception is the `@x` cross-import `entities/user/@x/account.ts`); each slice
exposes its public API through `index.ts`; and `@` maps to `src`. Code starts in the page that uses it and
moves down to a feature or entity only when a second slice needs it. Steiger (`npm run lint:fsd`) enforces the
structure, including that a widget, feature or entity slice is used by at least two slices or by `app`.
`eslint.config.js` enforces most coding rules and `frontend/tests/architecture/` what neither tool sees: the
slice and segment layout, the roles of `api` segment files, where query and mutation options are built, lazy
page routing, the runtime dependencies (no store library), the ownership of `shared/config` values, theme
variables and global classes, admin table columns, admin dialogs that hold a form, the error messages, the test
layout and the reviewed `@x` list. [frontend/AGENTS.md](frontend/AGENTS.md) lists the conventions to follow
when writing code.

| Layer       | Responsibility                                                                                |
| ----------- | --------------------------------------------------------------------------------------------- |
| `app/`      | Bootstrap, route table and guards, layouts and global styles                                  |
| `pages/`    | One slice per route: its views, view models, helpers and the API access only that page uses   |
| `widgets/`  | Page chrome composed by the app layouts (site header and footer, admin shell)                 |
| `features/` | User actions reused by several pages (logout, emailing users or attendees)                    |
| `entities/` | Business models with their API adapters, query and mutation options, rules and UI             |
| `shared/`   | Generated client, HTTP client, UI primitives, utilities, typed routes, config, i18n, branding |

Segments mean the same in every slice: `ui` components, `model` state and pure rules, `api` server access,
`lib` pure helpers and `config` static content.

### API and server state

`frontend/swagger.json` is the committed API contract. Orval generates
`src/shared/api/generated/{endpoints,models}` using `src/shared/api/http-client.ts` as its request mutator.
Generated files are disposable and must never be edited manually. The HTTP client sends cookies, obtains the
CSRF token when needed and turns failed responses into `ApiError`. `ErrorCode` values map to Spanish messages
under `errors.*` in `src/shared/i18n/locales/es.json`, the only locale.

The generated client is an anti-corruption boundary: besides `shared/api` itself, only `api` segments import
it. There `requests.ts` calls the endpoints, `mapper.ts` alone handles the wire DTOs and turns them into the
slice's own `model/types.ts`, and `queries.ts` and `mutations.ts` hold the options factories. Dates stay ISO
strings; views format them. Entities expose query keys (`xKeys`), `queryOptions` factories (`xQueries`), paged
sources for `useServerTable` and `usePagedList`, and `mutationOptions` factories (`xMutations`) that list the
keys they refresh in `meta.invalidates`; the query client invalidates them after a successful mutation, and a
page adds keys of another slice with `alsoInvalidates`. Reports read by a single page (event statistics,
attendees, ratings and terms decisions, badges, roster, dashboard analytics and the diagnostic and audit logs)
live in that page's `api` segment, as do the blocked data consultations and holds.

### Views, forms, routing and session

- Forms use `useForm` (`shared/lib/form`) around a pure `read(draft)` that returns the problem of each field
  (a translation key in a `form.problems` group, or `true` for a red border only) and, without problems, the
  value to send; the short credential forms (login, password recovery and reset) do without it. Admin
  create/edit dialogs follow the `useCrudDialog` state machine and save or delete through `useCrudActions`; an
  admin dialog holding a `<form>` (`shared/ui/form-dialog` for the usual cancel and save) closes only through
  its buttons, never on an outside click or Escape, so no draft is lost.
- Person field rules mirror the backend `User` rules in one place, `entities/user`: `usePersonForm` (and the
  `parseIndependentPerson`/`parseDependentPerson` functions behind it) validates and normalizes an independent
  account or a dependent and decides when a change needs the caller's password; the registration, profile and
  dependents forms only bind fields and show its messages.
- Admin tables render through `shared/ui/data-table`: a card with a toolbar (an optional search box bound to
  a column filter, filter pills, the result count and page actions), a chip per active filter except the
  search box together with «Limpiar filtros», the table, and a pager that stays hidden while every row fits on
  the smallest page. Header and toolbar filter components register their chips with the table they sit in;
  page-owned filters go in `extraFilters`. Row actions use `shared/ui/row-actions`: inline icon buttons plus a
  labeled menu.
- Route names and params are typed in `shared/routes`. Route guards resolve authentication and the staff role
  or data protection designation each admin page needs before entering it; detail pages receive their path
  params as props.
- The session is the `GET /api/auth/me` query in the TanStack Query cache, read through `useSession`; ending
  the session clears every other cached query. Pinia is not used. A query or mutation the API refuses because
  the session is gone (`AuthenticationRequired`, `CurrentUserNotFound`) ends it too, and protected pages then
  return to login (`app/router/session-expiry.ts`).
- Login is two pages: the password form navigates to `/login/verify`, which reads the pending challenge from
  `GET /api/auth/login/two-factor` (held in a cookie, so it survives a reload) and only stores the user in the
  session once the second factor is accepted.
- Light and dark themes use `--ca-*` CSS variables; Element Plus tokens map onto them.

## Caching

- Every anonymous GET/HEAD declares a named output-cache policy or an explicit `no-store`; authenticated or
  user-specific responses are never output cached.
- `HybridCache` is limited to immutable catalogs and non-personal dashboard aggregates; only
  `CachingQueryDecorator` reads and writes it, and only `HttpCacheInvalidator` evicts its tags.
- `HybridCache` entries live 12 hours (catalogs) or one minute (dashboard aggregates), and output-cache entries
  one minute. Both in-memory caches are capped at 64 MiB and skip payloads larger than 1 MiB.
- Committed domain events invalidate the tags `CacheTagsByEvent` maps them to, evicting both application and
  HTTP output entries; commands never invalidate caches themselves.
- Both caches are process-local and invalidated in process, which assumes one API replica.
- nginx does not cache API responses. It adds `Cache-Control: no-store` to every proxied response that sets
  none, so browsers and intermediaries never store API data. File content and thumbnails keep the API's own
  `public, no-cache`: browsers and intermediaries may store them but revalidate every use against their
  `ETag`. The API answers 304 while the file exists, so a deleted file, such as an image whose consent was
  withdrawn, stops being shown everywhere. `robots.txt` keeps crawlers out of `/api/`, files included.
- Hashed files under `/assets/` are `immutable` for a year, the stable-named icons last a day, and
  `index.html` with every SPA route is `no-cache`, so a deployment is picked up on the next navigation.
  Errors and any response without an explicit policy are `no-store`. A tab left open across a deployment
  reloads once when a lazy chunk of the previous build is gone (`stale-build-reload.ts`).
- The SPA holds no data cache: TanStack Query refetches on every mount, focus and reconnect and drops data
  as soon as no view observes it, so freshness is decided only by the API. The one exception is the session
  query (`GET /api/auth/me`), kept until a mutation invalidates it or the session ends. The event page also
  refetches the event and its activities two seconds after its signup next opens or closes.

## Changing the API contract

An API change is complete only when both applications agree:

1. Change backend endpoints, DTOs and any `ErrorCode` values.
2. Run the backend in Development and replace `frontend/swagger.json` with the document from
   `http://localhost:5150/swagger/v1/swagger.json`. `SwaggerDocTests` fails while the two documents differ
   in anything but the order of their properties.
3. From `frontend/`, run `npm run api:generate`.
4. Add translations for new error codes in `src/shared/i18n/locales/es.json`.
5. Run `dotnet test --solution CodigoActivo.slnx` from `backend/` and `npm run check` from `frontend/`.

`npm run api:check` generates the client in a temporary location and fails if committed output differs from
`swagger.json`.
