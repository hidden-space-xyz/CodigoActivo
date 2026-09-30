import type { LearningResource, ResourceInput, ResourceType } from '@/entities/resource'
import type { FormProblem, FormReading } from '@/shared/lib/form'
import { isRichTextBlank } from '@/shared/lib/rich-text'

/** What the resource dialog binds its inputs to. */
export interface ResourceDraft {
  title: string
  subtitle: string
  resourceTypeId: string
  description: string
  url: string
}

/** Field of the resource form that can be refused. */
type ResourceField = 'title' | 'subtitle' | 'resourceTypeId' | 'description' | 'url'

function isHttpUrl(value: string): boolean {
  if (!/^https?:\/\//i.test(value)) return false
  try {
    new URL(value)
    return true
  } catch {
    return false
  }
}

/** A blank draft, or one filled with the resource being edited. */
export function toResourceDraft(resource: LearningResource | null): ResourceDraft {
  return {
    title: resource?.title ?? '',
    subtitle: resource?.subtitle ?? '',
    resourceTypeId: resource?.type.id ?? '',
    description: resource?.description ?? '',
    url: resource?.url ?? '',
  }
}

/**
 * Reads the resource dialog against the known `types`: a title, a subtitle and a known type are
 * required; external types need an http(s) link and send no description, the others need a
 * description and send no link. The thumbnail is resolved separately when saving.
 */
export function readResourceDraft(
  draft: ResourceDraft,
  types: readonly ResourceType[],
): FormReading<ResourceField, Omit<ResourceInput, 'thumbnailId'>> {
  const title = draft.title.trim()
  const subtitle = draft.subtitle.trim()
  const url = draft.url.trim()
  const type = types.find((candidate) => candidate.id === draft.resourceTypeId)
  const problems: Partial<Record<ResourceField, FormProblem>> = {}

  if (!title) problems.title = true
  if (!subtitle) problems.subtitle = true
  if (!draft.resourceTypeId)
    problems.resourceTypeId = 'pages.admin.resources.form.problems.typeRequired'
  else if (!type) problems.resourceTypeId = true
  if (type && !type.isExternal && isRichTextBlank(draft.description)) {
    problems.description = 'pages.admin.resources.form.problems.descriptionRequired'
  }
  if (type?.isExternal && !url) problems.url = 'pages.admin.resources.form.problems.urlRequired'
  else if (type?.isExternal && !isHttpUrl(url))
    problems.url = 'pages.admin.resources.form.problems.urlInvalid'

  if (!type || Object.keys(problems).length > 0) return { problems, value: null }
  return {
    problems,
    value: {
      title,
      subtitle,
      description: type.isExternal ? null : draft.description,
      url: type.isExternal ? url : null,
      resourceTypeId: type.id,
    },
  }
}
