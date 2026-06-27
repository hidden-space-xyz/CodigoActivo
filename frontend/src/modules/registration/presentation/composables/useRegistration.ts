import { computed, reactive, ref } from 'vue'
import { useMutation } from '@tanstack/vue-query'

import { isMinor } from '@/modules/registration/domain/services/age'
import {
  createEmptyRegistrationForm,
  type RegistrationForm,
} from '@/modules/registration/domain/value-objects/registration-form'
import type { RegistrationRole } from '@/modules/registration/domain/value-objects/registration-role'
import {
  patchApiAuthUserIdVerify,
  postApiAuthRegister,
} from '@/shared/api/generated/endpoints/auth/auth'
import type { CreateUserRequest } from '@/shared/api/generated/models'
import { scrollToTop } from '@/shared/utils/scroll'

export type RegistrationStep = 'role' | 'form' | 'success'

const DEFAULT_ROLE: RegistrationRole = 'participant'

function toCreateUserRequest(form: RegistrationForm): CreateUserRequest {
  const body: CreateUserRequest = {
    firstName: form.firstName.trim(),
    lastName: form.lastName.trim(),
    email: form.email.trim() ? form.email.trim() : null,
    phone: form.phone.trim() ? form.phone.trim() : null,
    password: form.password ? form.password : null,
  }
  if (form.dateOfBirth) body.birthDate = form.dateOfBirth
  return body
}

export function useRegistration() {
  const step = ref<RegistrationStep>('role')
  const form = reactive<RegistrationForm>(createEmptyRegistrationForm(DEFAULT_ROLE))
  const submittedRole = ref<RegistrationRole | null>(null)
  const createdUserId = ref<string | null>(null)
  const verificationCode = ref<string | null>(null)
  const isVerified = ref(false)

  const isUserMinor = computed(() => isMinor(form.dateOfBirth))

  const mutation = useMutation({
    mutationFn: (payload: CreateUserRequest) => postApiAuthRegister(payload).then((r) => r.data),
    onSuccess: (data) => {
      submittedRole.value = form.role
      createdUserId.value = data.user?.id ?? null
      verificationCode.value = data.verificationCode ?? null
      step.value = 'success'
      scrollToTop()
    },
  })

  const verifyMutation = useMutation({
    mutationFn: (otp: string) => {
      if (!createdUserId.value) throw new Error('missing user id')
      return patchApiAuthUserIdVerify(createdUserId.value, { otp }).then((r) => r.data)
    },
    onSuccess: () => {
      isVerified.value = true
    },
  })

  function selectRole(role: RegistrationRole): void {
    form.role = role
    step.value = 'form'
    scrollToTop()
  }

  function goBackToRoles(): void {
    step.value = 'role'
    scrollToTop()
  }

  function submit(): void {
    mutation.mutate(toCreateUserRequest(form))
  }

  function verify(otp: string): void {
    verifyMutation.mutate(otp)
  }

  function reset(): void {
    mutation.reset()
    verifyMutation.reset()
    Object.assign(form, createEmptyRegistrationForm(DEFAULT_ROLE))
    submittedRole.value = null
    createdUserId.value = null
    verificationCode.value = null
    isVerified.value = false
    step.value = 'role'
    scrollToTop()
  }

  return {
    step,
    form,
    isUserMinor,
    submittedRole,
    verificationCode,
    isVerified,
    selectRole,
    goBackToRoles,
    submit,
    verify,
    reset,
    isSubmitting: mutation.isPending,
    isError: mutation.isError,
    isVerifying: verifyMutation.isPending,
    verifyError: verifyMutation.isError,
  }
}
