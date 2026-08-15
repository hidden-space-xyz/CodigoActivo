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

export function getUserTypesRequest() {
  return getApiUsersTypes().then((r) => r.data ?? [])
}

export function getUserStatusTypesRequest() {
  return getApiUsersStatusTypes().then((r) => r.data ?? [])
}

export function getActivityRoleTypesRequest() {
  return getApiActivitiesRoleType().then((r) => r.data ?? [])
}

export function getAssignmentStatusTypesRequest() {
  return getApiActivitiesAssignmentStatusTypes().then((r) => r.data ?? [])
}

export function getActivityModalityTypesRequest() {
  return getApiActivitiesModalityTypes().then((r) => r.data ?? [])
}

export function getResourceTypesRequest() {
  return getApiResourcesTypes().then((r) => r.data ?? [])
}

export function getEventCategoryTypesRequest() {
  return getApiEventsCategoryType({ pageSize: 100 }).then((r) => r.data.items ?? [])
}

export function getEventCategoryTypesPageRequest(
  params: GetApiEventsCategoryTypeParams,
): Promise<{ items: EventCategoryTypeResponse[]; total: number }> {
  return getApiEventsCategoryType(params).then(toPage)
}

export function createEventCategoryTypeRequest(body: CreateEventCategoryTypeRequest) {
  return postApiEventsCategoryType(body).then((r) => r.data)
}

export function updateEventCategoryTypeRequest(id: string, body: UpdateEventCategoryTypeRequest) {
  return putApiEventsCategoryTypeEventCategoryTypeId(id, body)
}

export function deleteEventCategoryTypeRequest(id: string) {
  return deleteApiEventsCategoryTypeEventCategoryTypeId(id)
}

export function getTermsDocumentsRequest() {
  return getApiEventsTermsDocument({ pageSize: 100 }).then((r) => r.data.items ?? [])
}

export function getTermsDocumentsPageRequest(
  params: GetApiEventsTermsDocumentParams,
): Promise<{ items: TermsDocumentResponse[]; total: number }> {
  return getApiEventsTermsDocument(params).then(toPage)
}

export function createTermsDocumentRequest(body: CreateTermsDocumentRequest) {
  return postApiEventsTermsDocument(body).then((r) => r.data)
}

export function updateTermsDocumentRequest(id: string, body: UpdateTermsDocumentRequest) {
  return putApiEventsTermsDocumentTermsDocumentId(id, body)
}

export function deleteTermsDocumentRequest(id: string) {
  return deleteApiEventsTermsDocumentTermsDocumentId(id)
}
