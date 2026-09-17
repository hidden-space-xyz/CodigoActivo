import {
  getApiActivitiesAssignmentStatusTypes,
  getApiActivitiesModalityTypes,
  getApiActivitiesRoleType,
} from '@/shared/api/generated/endpoints/activities/activities'
import {
  deleteApiEventsCategoryTypeEventCategoryTypeId,
  deleteApiEventsTermsDocumentTermsDocumentId,
  getApiEventsCategoryType,
  getApiEventsTermsDocument,
  postApiEventsCategoryType,
  postApiEventsTermsDocument,
  putApiEventsCategoryTypeEventCategoryTypeId,
  putApiEventsTermsDocumentTermsDocumentId,
} from '@/shared/api/generated/endpoints/events/events'
import { getApiResourcesTypes } from '@/shared/api/generated/endpoints/resources/resources'
import {
  getApiUsersStatusTypes,
  getApiUsersTypes,
} from '@/shared/api/generated/endpoints/users/users'
import type {
  CreateEventCategoryTypeRequest,
  CreateTermsDocumentRequest,
  EventCategoryTypeResponse,
  GetApiEventsCategoryTypeParams,
  GetApiEventsTermsDocumentParams,
  TermsDocumentResponse,
  UpdateEventCategoryTypeRequest,
  UpdateTermsDocumentRequest,
} from '@/shared/api/generated/models'
import { toPage } from '@/shared/api'

/** Lists the user types catalog; a missing body becomes `[]`. */
export function getUserTypesRequest() {
  return getApiUsersTypes().then((r) => r.data ?? [])
}

/** Lists the account status catalog; a missing body becomes `[]`. */
export function getUserStatusTypesRequest() {
  return getApiUsersStatusTypes().then((r) => r.data ?? [])
}

/** Lists the participation roles available for activities. */
export function getActivityRoleTypesRequest() {
  return getApiActivitiesRoleType().then((r) => r.data ?? [])
}

/** Lists the statuses an activity signup can have (requested, confirmed, denied...). */
export function getAssignmentStatusTypesRequest() {
  return getApiActivitiesAssignmentStatusTypes().then((r) => r.data ?? [])
}

/** Lists the modalities an activity can be offered in. */
export function getActivityModalityTypesRequest() {
  return getApiActivitiesModalityTypes().then((r) => r.data ?? [])
}

/** Lists the types used to classify linked resources. */
export function getResourceTypesRequest() {
  return getApiResourcesTypes().then((r) => r.data ?? [])
}

/** Loads up to 100 event categories as a plain array for selectors (no paging metadata). */
export function getEventCategoryTypesRequest() {
  return getApiEventsCategoryType({ pageSize: 100 }).then((r) => r.data.items ?? [])
}

/** Fetches one page of event categories for the admin catalog table. */
export function getEventCategoryTypesPageRequest(
  params: GetApiEventsCategoryTypeParams,
): Promise<{ items: EventCategoryTypeResponse[]; total: number }> {
  return getApiEventsCategoryType(params).then(toPage)
}

/** Creates an event category and resolves the created `EventCategoryTypeResponse`. */
export function createEventCategoryTypeRequest(body: CreateEventCategoryTypeRequest) {
  return postApiEventsCategoryType(body).then((r) => r.data)
}

/** Saves an event category's name and color. */
export function updateEventCategoryTypeRequest(id: string, body: UpdateEventCategoryTypeRequest) {
  return putApiEventsCategoryTypeEventCategoryTypeId(id, body)
}

/** Deletes an event category from the admin catalog. */
export function deleteEventCategoryTypeRequest(id: string) {
  return deleteApiEventsCategoryTypeEventCategoryTypeId(id)
}

/** Loads up to 100 terms documents as a plain array for the event form selector. */
export function getTermsDocumentsRequest() {
  return getApiEventsTermsDocument({ pageSize: 100 }).then((r) => r.data.items ?? [])
}

/** Fetches one page of terms documents for the admin catalog table. */
export function getTermsDocumentsPageRequest(
  params: GetApiEventsTermsDocumentParams,
): Promise<{ items: TermsDocumentResponse[]; total: number }> {
  return getApiEventsTermsDocument(params).then(toPage)
}

/** Creates a terms document and resolves the created `TermsDocumentResponse`. */
export function createTermsDocumentRequest(body: CreateTermsDocumentRequest) {
  return postApiEventsTermsDocument(body).then((r) => r.data)
}

/** Saves a terms document's name and description. */
export function updateTermsDocumentRequest(id: string, body: UpdateTermsDocumentRequest) {
  return putApiEventsTermsDocumentTermsDocumentId(id, body)
}

/** Deletes a terms document from the admin catalog. */
export function deleteTermsDocumentRequest(id: string) {
  return deleteApiEventsTermsDocumentTermsDocumentId(id)
}
