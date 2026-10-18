/** Longest search term or text filter a list query accepts (`PageQuery.MaxTextLength`). */
export const MAX_SEARCH_LENGTH = 200

/** Largest image the API stores (`FileUploadOptions.DefaultMaxSizeBytes`). */
export const MAX_IMAGE_UPLOAD_BYTES = 10 * 1024 * 1024

/** File picker filter for the only image formats the API stores, detected from their content. */
export const IMAGE_UPLOAD_ACCEPT = 'image/jpeg,image/png,image/gif,image/webp'

/** Minutes an emailed verification code or password reset link stays valid. */
export const ONE_TIME_CODE_LIFETIME_MINUTES = 15

/** Seconds the API makes a person wait before it emails another code to the same account. */
export const RESEND_COOLDOWN_SECONDS = 60
