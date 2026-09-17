import {
  getApiFilesFileId,
  postApiFiles,
  putApiFilesFileId,
} from '@/shared/api/generated/endpoints/files/files'
import type { FileResponse } from '@/shared/api/generated/models'
import { unwrapOrNull } from '@/shared/api'

/**
 * Uploads a thumbnail and resolves to its file id. With `existingId` the stored file content is
 * replaced in place and the same id is returned; otherwise a new file is created.
 */
export async function uploadThumbnailRequest(
  file: File,
  existingId?: string | null,
): Promise<string> {
  if (existingId) {
    await putApiFilesFileId(existingId, { file })
    return existingId
  }
  const response = await postApiFiles({ file })
  return response.data.id ?? ''
}

/** Resolves the display file name (name plus extension); empty when the file no longer exists. */
export async function getThumbnailNameRequest(id: string): Promise<string> {
  const meta = await unwrapOrNull<FileResponse>(getApiFilesFileId(id))
  return `${meta?.name ?? ''}${meta?.extension ?? ''}`
}

/** Uploads a new file and resolves to its id, or `undefined` if the API omitted it. */
export async function uploadFileRequest(file: File): Promise<string | undefined> {
  const response = await postApiFiles({ file })
  return response.data.id
}
