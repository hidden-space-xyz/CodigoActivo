import type { MaybeRefOrGetter } from 'vue'
import { computed, ref, toValue } from 'vue'
import { useI18n } from 'vue-i18n'

import { uploadThumbnailRequest } from '../api/requests'
import { getErrorMessage } from '@/shared/lib/feedback'

/**
 * Holds the thumbnail picked in a form and uploads it only on save. `resolveThumbnailId` returns
 * the existing id when nothing was picked, uploads the picked image as a new file otherwise (the
 * same pick is uploaded once, so saving again after a refused save reuses it), and returns `null`
 * on failure with a translated message in `uploadError`.
 */
export function useThumbnailUpload(existingId: MaybeRefOrGetter<string | null | undefined>) {
  const { t } = useI18n()
  const pickedFile = ref<File | null>(null)
  const uploading = ref(false)
  const uploadError = ref('')
  let uploaded: { readonly file: File; readonly id: string } | null = null

  const missingThumbnail = computed(() => !pickedFile.value && !toValue(existingId))

  function reset(): void {
    pickedFile.value = null
    uploadError.value = ''
  }

  async function resolveThumbnailId(): Promise<string | null> {
    uploadError.value = ''
    const file = pickedFile.value
    if (!file) return toValue(existingId) ?? null
    if (uploaded?.file === file) return uploaded.id
    uploading.value = true
    try {
      const id = await uploadThumbnailRequest(file)
      uploaded = { file, id }
      return id
    } catch (error) {
      uploadError.value = getErrorMessage(error, t('entities.file.thumbnail.uploadFailed'))
      return null
    } finally {
      uploading.value = false
    }
  }

  return { pickedFile, uploading, uploadError, missingThumbnail, reset, resolveThumbnailId }
}
