import type { MaybeRefOrGetter } from 'vue'
import { computed, ref, toValue } from 'vue'

import { getErrorMessage } from '@/shared/lib'

import { uploadThumbnail } from '../api/requests'

/**
 * The picked-file/upload state machine shared by every thumbnail-bearing form dialog:
 * validate presence, upload the picked file (or keep the existing id), surface upload errors.
 */
export function useThumbnailUpload(existingId: MaybeRefOrGetter<string | null | undefined>) {
  const pickedFile = ref<File | null>(null)
  const uploading = ref(false)
  const uploadError = ref('')

  const missingThumbnail = computed(() => !pickedFile.value && !toValue(existingId))

  function reset(): void {
    pickedFile.value = null
    uploadError.value = ''
  }

  /**
   * Returns the thumbnail id to persist, or null when the upload failed —
   * `uploadError` then carries the message and the caller should abort the save.
   */
  async function resolveThumbnailId(): Promise<string | null> {
    uploadError.value = ''
    if (!pickedFile.value) return toValue(existingId) ?? null
    uploading.value = true
    try {
      return await uploadThumbnail(pickedFile.value, toValue(existingId))
    } catch (error) {
      uploadError.value = getErrorMessage(error, 'No se pudo subir la imagen.')
      return null
    } finally {
      uploading.value = false
    }
  }

  return { pickedFile, uploading, uploadError, missingThumbnail, reset, resolveThumbnailId }
}
