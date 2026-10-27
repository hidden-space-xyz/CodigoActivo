/** Longest search term or text filter a list query accepts (`PageQuery.MaxTextLength`). */
export const MAX_SEARCH_LENGTH = 200

/** Fewest characters a search of the blocked data takes (`SearchBlockedDataCommand.MinQueryLength`). */
export const BLOCKED_DATA_MIN_SEARCH_LENGTH = 3

/** Longest reference of a blocked data consultation (`BlockedDataConsultation.ReferenceMaxLength`). */
export const BLOCKED_DATA_REFERENCE_MAX_LENGTH = 200

/** Largest page a list query returns (`PageQuery.MaxPageSize`). */
export const MAX_PAGE_SIZE = 100

/** Fewest characters the API accepts in a password (`[MinLength(12)]` on the account requests). */
export const PASSWORD_MIN_LENGTH = 12

/** Most characters the API accepts in a password (`[MaxLength(128)]` on every request taking one). */
export const PASSWORD_MAX_LENGTH = 128

/** Longest second-factor code, emailed or from an authenticator (`[MaxLength(16)] Code`). */
export const TWO_FACTOR_CODE_MAX_LENGTH = 16

/** Longest first name or last name of a person (`[MaxLength(120)]` on the account and user requests). */
export const PERSON_NAME_MAX_LENGTH = 120

/** Longest email address, and so the longest login identifier (`[MaxLength(256)]`). */
export const EMAIL_ADDRESS_MAX_LENGTH = 256

/** Longest phone number as typed (`[MaxLength(40)]` on the account and user requests). */
export const PHONE_MAX_LENGTH = 40

/** Fewest digits a phone number takes (`PhoneNumber.MinDigits`). */
export const PHONE_MIN_DIGITS = 7

/** Most digits a phone number takes (`PhoneNumber.MaxDigits`). */
export const PHONE_MAX_DIGITS = 15

/** Longest DNI or NIE as typed (`[MaxLength(12)]` on the account and user requests). */
export const NATIONAL_ID_MAX_LENGTH = 12

/** Most dependents one guardian may hold (`Household.MaxDependents`). */
export const MAX_DEPENDENTS = 20

/** Longest name of an event category or terms document (`[MaxLength(120)] Name`). */
export const CATALOG_NAME_MAX_LENGTH = 120

/** Longest title of an event, news item, resource or activity (`[MaxLength(200)] Title`). */
export const TITLE_MAX_LENGTH = 200

/** Longest subtitle of an event, news item or resource (`[MaxLength(300)] Subtitle`). */
export const SUBTITLE_MAX_LENGTH = 300

/** Longest partner website or resource link (`[MaxLength(500)]` on their requests). */
export const URL_MAX_LENGTH = 500

/** Longest description of an activity (`[MaxLength(4000)] Description` on the activity requests). */
export const ACTIVITY_DESCRIPTION_MAX_LENGTH = 4000

/** Longest location of an activity (`[MaxLength(200)] Location` on the activity requests). */
export const ACTIVITY_LOCATION_MAX_LENGTH = 200

/** Fewest people an activity may ask for in one role (`RoleCapacity.MinDesiredCount`). */
export const ROLE_MIN_DESIRED_COUNT = 1

/** Most people an activity may ask for in one role (`RoleCapacity.MaxDesiredCount`). */
export const ROLE_MAX_DESIRED_COUNT = 10_000

/** Longest partner name (`[MaxLength(200)] Name` on the partner requests). */
export const PARTNER_NAME_MAX_LENGTH = 200

/** Lowest partner tier (`[Range(0, int.MaxValue)] Tier` on the partner requests). */
export const PARTNER_MIN_TIER = 0

/** Highest partner tier, `int.MaxValue` (`[Range(0, int.MaxValue)] Tier` on the partner requests). */
export const PARTNER_MAX_TIER = 2_147_483_647

/** Highest score of an event rating (`EventRating.MaxScore`). */
export const RATING_MAX_SCORE = 5

/** Longest free-text answer of an event rating (`EventRating.MaxAnswerLength`). */
export const RATING_ANSWER_MAX_LENGTH = 2000

/** Longest subject of an email an administrator writes (`ManualEmailText.SubjectMaxLength`). */
export const MANUAL_EMAIL_SUBJECT_MAX_LENGTH = 200

/** Longest body of an email an administrator writes (`ManualEmailText.BodyMaxLength`). */
export const MANUAL_EMAIL_BODY_MAX_LENGTH = 10_000

/** Most files attached to one email an administrator writes (`ManualEmailOptions.MaxAttachments`). */
export const MANUAL_EMAIL_MAX_ATTACHMENTS = 10

/** Largest combined size of those attachments, in bytes (`ManualEmailOptions.MaxAttachmentsBytes`). */
export const MANUAL_EMAIL_MAX_ATTACHMENTS_BYTES = 8 * 1024 * 1024

/** Longest rich-text document, in characters of JSON (`RichTextAllowlist.MaxJsonLength`). */
export const RICH_TEXT_MAX_LENGTH = 262_144

/** Most nodes a rich-text document holds (`RichTextAllowlist.MaxNodes`). */
export const RICH_TEXT_MAX_NODES = 5_000

/** Deepest nesting of nodes in a rich-text document (`RichTextAllowlist.MaxDepth`). */
export const RICH_TEXT_MAX_DEPTH = 50

/** Longest image alternative text or title in rich text (`RichTextAllowlist.MaxLabelLength`). */
export const RICH_TEXT_LABEL_MAX_LENGTH = 300

/** Highest number an ordered list of rich text starts at (`orderedList.start` allowlist). */
export const RICH_TEXT_MAX_LIST_START = 100_000

/** Most columns or rows one rich-text table cell spans (`colspan`/`rowspan` allowlist). */
export const RICH_TEXT_MAX_CELL_SPAN = 100

/** Widest rich-text table column, in pixels (`colwidth` allowlist). */
export const RICH_TEXT_MAX_COLUMN_WIDTH = 2_000

/** Longest language name of a rich-text code block (`CodeLanguage` allowlist pattern). */
export const RICH_TEXT_CODE_LANGUAGE_MAX_LENGTH = 40

/** First year the dashboard analytics cover (`DashboardAnalyticsQuery` days from 2000-01-01). */
export const ANALYTICS_FIRST_YEAR = 2000

/** Last year the dashboard analytics cover (`DashboardAnalyticsQuery` days up to 2099-12-31). */
export const ANALYTICS_LAST_YEAR = 2099

/** Largest image the API stores, in bytes (`FileUploadOptions.MaxSizeBytes`). */
export const MAX_IMAGE_UPLOAD_BYTES = 10 * 1024 * 1024

/** File picker filter for the only image formats the API stores, detected from their content. */
export const IMAGE_UPLOAD_ACCEPT = 'image/jpeg,image/png,image/webp'

/** Minutes an emailed verification code or password reset link stays valid. */
export const ONE_TIME_CODE_LIFETIME_MINUTES = 15

/** Seconds the API makes a person wait before it emails another code to the same account. */
export const RESEND_COOLDOWN_SECONDS = 60
