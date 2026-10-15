import { getApiFilesFileId, postApiFiles } from '@/shared/api/generated/endpoints/files/files'
import type { FileResponse } from '@/shared/api/generated/models'
import { unwrapOrNull } from '@/shared/api'

/**
 * Uploads a thumbnail as a new file and resolves to its id. A new image never reuses the id of the
 * one it replaces, so its content URL changes and no open page keeps showing the previous image;
 * the replaced file is removed once the saved content stops referencing it.
 */
export async function uploadThumbnailRequest(file: File): Promise<string> {
  const response = await postApiFiles({ file })
  return response.data.id ?? ''
}

/**
 * Resolves the display file name, the original name as uploaded (it already carries its extension);
 * empty when the file no longer exists.
 */
export async function getThumbnailNameRequest(id: string): Promise<string> {
  const meta = await unwrapOrNull<FileResponse>(getApiFilesFileId(id))
  return meta?.name ?? ''
}

/** Uploads a new file and resolves to its id, or `undefined` if the API omitted it. */
export async function uploadFileRequest(file: File): Promise<string | undefined> {
  const response = await postApiFiles({ file })
  return response.data.id
}
