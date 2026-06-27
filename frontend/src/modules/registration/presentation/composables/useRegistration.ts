import { reactive, ref } from 'vue'
import { useMutation, useQuery } from '@tanstack/vue-query'

import {
  createEmptyRegistrationForm,
  type RegistrationForm,
} from '@/modules/registration/domain/value-objects/registration-form'
import {
  patchApiAuthUserIdVerify,
  postApiAuthRegister,
} from '@/shared/api/generated/endpoints/auth/auth'
import { getApiUsersRegistrationTypes } from '@/shared/api/generated/endpoints/users/users'
import type { RegisterRequest } from '@/shared/api/generated/models'
import { scrollToTop } from '@/shared/utils/scroll'

export type RegistrationStep = 'age-gate' | 'form' | 'success'

function toRegisterRequest(form: RegistrationForm): RegisterRequest {
  return {
    firstName: form.firstName.trim(),
    lastName: form.lastName.trim(),
    email: form.email.trim(),
    phone: form.phone.trim(),
    password: form.password,
    birthDate: form.dateOfBirth,
    roleId: form.roleId,
    minors: form.minors.map((minor) => ({
      firstName: minor.firstName.trim(),
      lastName: minor.lastName.trim(),
      birthDate: minor.dateOfBirth,
      roleId: minor.roleId,
    })),
  }
}

export function useRegistration() {
  const step = ref<RegistrationStep>('age-gate')
  const form = reactive<RegistrationForm>(createEmptyRegistrationForm())
  const createdUserId = ref<string | null>(null)
  const verificationCode = ref<string | null>(null)
  const submittedRoleName = ref('')
  const submittedMinorCount = ref(0)
  const isVerified = ref(false)

  // Selectable roles come from the backend, already filtered by age + visibility.
  const adultRoles = useQuery({
    queryKey: ['registration-types', 'adult'],
    queryFn: ({ signal }) =>
      getApiUsersRegistrationTypes({ minor: false }, { signal }).then((r) => r.data ?? []),
  })
  const minorRoles = useQuery({
    queryKey: ['registration-types', 'minor'],
    queryFn: ({ signal }) =>
      getApiUsersRegistrationTypes({ minor: true }, { signal }).then((r) => r.data ?? []),
  })

  const mutation = useMutation({
    mutationFn: (payload: RegisterRequest) => postApiAuthRegister(payload).then((r) => r.data),
    onSuccess: (data) => {
      createdUserId.value = data.adult?.id ?? null
      verificationCode.value = data.verificationCode ?? null
      submittedRoleName.value =
        (adultRoles.data.value ?? []).find((role) => role.id === form.roleId)?.name ?? ''
      submittedMinorCount.value = data.minors?.length ?? form.minors.length
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

  function confirmAdult(): void {
    step.value = 'form'
    scrollToTop()
  }

  function backToGate(): void {
    step.value = 'age-gate'
    scrollToTop()
  }

  function submit(): void {
    mutation.mutate(toRegisterRequest(form))
  }

  function verify(otp: string): void {
    verifyMutation.mutate(otp)
  }

  function reset(): void {
    mutation.reset()
    verifyMutation.reset()
    Object.assign(form, createEmptyRegistrationForm())
    createdUserId.value = null
    verificationCode.value = null
    submittedRoleName.value = ''
    submittedMinorCount.value = 0
    isVerified.value = false
    step.value = 'age-gate'
    scrollToTop()
  }

  return {
    step,
    form,
    adultRoles,
    minorRoles,
    verificationCode,
    submittedRoleName,
    submittedMinorCount,
    isVerified,
    confirmAdult,
    backToGate,
    submit,
    verify,
    reset,
    isSubmitting: mutation.isPending,
    isError: mutation.isError,
    isVerifying: verifyMutation.isPending,
    verifyError: verifyMutation.isError,
  }
}
