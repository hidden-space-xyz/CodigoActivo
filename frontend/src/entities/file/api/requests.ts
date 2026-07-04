import {
  deleteApiFilesFileId,
  getApiFilesFileId,
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

export async function getThumbnailNameRequest(id: string): Promise<string> {
  const meta = await unwrapOrNull<FileResponse>(getApiFilesFileId(id))
  return `${meta?.name ?? ''}${meta?.extension ?? ''}`
}
