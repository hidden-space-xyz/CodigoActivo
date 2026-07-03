import {
  deleteApiFilesFileId,
  getApiFilesFileId,
  getApiFilesFileIdContent,
  postApiFiles,
  putApiFilesFileId,
} from '@/shared/api/generated/endpoints/files/files'
import type { FileResponse } from '@/shared/api/generated/models'
import { unwrapOrNull } from '@/shared/api'

export async function deleteThumbnail(id?: string | null): Promise<void> {
  if (!id) return
  try {
    await deleteApiFilesFileId(id)
  } catch {
    void 0
  }
}

export async function uploadThumbnail(file: File, existingId?: string | null): Promise<string> {
  if (existingId) {
    await putApiFilesFileId(existingId, { file })
    return existingId
  }
  const response = await postApiFiles({ file })
  return response.data.id ?? ''
}

interface ThumbnailPreview {
  url: string
  name: string
}

export async function loadThumbnailPreview(id: string): Promise<ThumbnailPreview> {
  const [meta, content] = await Promise.all([
    unwrapOrNull<FileResponse>(getApiFilesFileId(id)),
    getApiFilesFileIdContent(id).then((r) => r.data as unknown as Blob),
  ])
  return {
    url: URL.createObjectURL(content),
    name: `${meta?.name ?? ''}${meta?.extension ?? ''}`,
  }
}
