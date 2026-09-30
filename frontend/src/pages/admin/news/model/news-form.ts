import type { NewsItem, NewsItemInput } from '@/entities/news-item'
import type { FormProblem, FormReading } from '@/shared/lib/form'
import { EMPTY_DOC_JSON } from '@/shared/lib/rich-text'

/** What the news dialog binds its inputs to; `description` is a rich-text document. */
export interface NewsDraft {
  title: string
  subtitle: string
  description: string
}

/** Field of the news form that can be refused. */
type NewsField = 'title' | 'subtitle'

/** A blank draft, or one filled with the news item being edited. */
export function toNewsDraft(newsItem: NewsItem | null): NewsDraft {
  return {
    title: newsItem?.title ?? '',
    subtitle: newsItem?.subtitle ?? '',
    description: newsItem?.description ?? '',
  }
}

/**
 * Reads the news dialog: a title and a subtitle are required and an empty description is saved as
 * an empty rich-text document. The thumbnail is resolved separately when saving.
 */
export function readNewsDraft(
  draft: NewsDraft,
): FormReading<NewsField, Omit<NewsItemInput, 'thumbnailId'>> {
  const title = draft.title.trim()
  const subtitle = draft.subtitle.trim()
  const problems: Partial<Record<NewsField, FormProblem>> = {}
  if (!title) problems.title = true
  if (!subtitle) problems.subtitle = true
  if (!title || !subtitle) return { problems, value: null }

  return {
    problems,
    value: {
      title,
      subtitle,
      description: draft.description.trim() ? draft.description : EMPTY_DOC_JSON,
    },
  }
}
