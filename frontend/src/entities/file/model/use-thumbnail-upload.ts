import type { MaybeRefOrGetter } from 'vue'
import { computed, ref, toValue } from 'vue'
import { useI18n } from 'vue-i18n'

import { uploadFileRequest } from '../api/requests'
import { getErrorMessage } from '@/shared/lib/feedback'

/**
 * Hours an upload of the same pick is reused. The API deletes an upload that no content uses after
 * a week, so a form left open longer uploads the pick again instead of sending a deleted id.
 */
const UPLOAD_REUSE_HOURS = 24

/**
 * Holds the thumbnail picked in a form and uploads it only on save. `resolveThumbnailId` returns
 * the existing id (`null` without one) when nothing was picked, uploads the picked image as a new
 * file otherwise (the same pick is uploaded once and reused for a day, so saving again after a
 * refused save reuses it), and returns `null` on failure with a translated message in
 * `uploadError`. `missingThumbnail` is true while there is neither a pick nor an existing id, and
 * `reset` forgets the pick and the upload.
 */
export function useThumbnailUpload(existingId: MaybeRefOrGetter<string | null | undefined>) {
  const { t } = useI18n()
  const pickedFile = ref<File | null>(null)
  const uploading = ref(false)
  const uploadError = ref('')
  let uploaded: { readonly file: File; readonly id: string; readonly at: number } | null = null

  const missingThumbnail = computed(() => !pickedFile.value && !toValue(existingId))

  function reset(): void {
    pickedFile.value = null
    uploadError.value = ''
    uploaded = null
  }

  async function resolveThumbnailId(): Promise<string | null> {
    uploadError.value = ''
    const file = pickedFile.value
    if (!file) return toValue(existingId) ?? null
    if (uploaded?.file === file && Date.now() - uploaded.at < UPLOAD_REUSE_HOURS * 3_600_000) {
      return uploaded.id
    }
    uploading.value = true
    try {
      const id = await uploadFileRequest(file)
      uploaded = { file, id, at: Date.now() }
      return id
    } catch (error) {
      uploadError.value = getErrorMessage(error, t('common.imageUploadFailed'))
      return null
    } finally {
      uploading.value = false
    }
  }

  return { pickedFile, uploading, uploadError, missingThumbnail, reset, resolveThumbnailId }
}
