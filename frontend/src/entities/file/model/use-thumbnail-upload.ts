import type { MaybeRefOrGetter } from 'vue'
import { computed, ref, toValue } from 'vue'
import { useI18n } from 'vue-i18n'

import { getErrorMessage } from '@/shared/lib'

import { uploadThumbnail } from '../api/requests'

export function useThumbnailUpload(existingId: MaybeRefOrGetter<string | null | undefined>) {
  const { t } = useI18n()
  const pickedFile = ref<File | null>(null)
  const uploading = ref(false)
  const uploadError = ref('')

  const missingThumbnail = computed(() => !pickedFile.value && !toValue(existingId))

  function reset(): void {
    pickedFile.value = null
    uploadError.value = ''
  }

  async function resolveThumbnailId(): Promise<string | null> {
    uploadError.value = ''
    if (!pickedFile.value) return toValue(existingId) ?? null
    uploading.value = true
    try {
      return await uploadThumbnail(pickedFile.value, toValue(existingId))
    } catch (error) {
      uploadError.value = getErrorMessage(error, t('entities.file.thumbnail.uploadFailed'))
      return null
    } finally {
      uploading.value = false
    }
  }

  return { pickedFile, uploading, uploadError, missingThumbnail, reset, resolveThumbnailId }
}
