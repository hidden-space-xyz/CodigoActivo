import { postApiFiles } from '@/shared/api/generated/endpoints/files/files'

/**
 * Uploads a new file and resolves to its id. A new file never reuses the id of the one it replaces,
 * so its content URL changes and no open page keeps showing the previous image; the replaced file
 * is removed once the saved content stops referencing it.
 */
export async function uploadFileRequest(file: File): Promise<string> {
  const response = await postApiFiles({ file })
  return response.data.id
}
