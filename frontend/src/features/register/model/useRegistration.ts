import { reactive, ref } from 'vue'
import { useMutation, useQuery } from '@tanstack/vue-query'

import { scrollToTop } from '@/shared/lib'
import { accountQueryKeys, getRegistrationTypesRequest } from '@/entities/account'

import { registerRequest, verifyRegistrationRequest } from '../api/requests'
import { createEmptyRegistrationForm, type RegistrationForm } from './registration-form'

export type RegistrationStep = 'age-gate' | 'form' | 'success'

export function useRegistration() {
  const step = ref<RegistrationStep>('age-gate')
  const form = reactive<RegistrationForm>(createEmptyRegistrationForm())
  const createdUserId = ref<string | null>(null)
  const verificationCode = ref<string | null>(null)
  const submittedRoleName = ref('')
  const submittedMinorCount = ref(0)
  const isVerified = ref(false)

  const adultRoles = useQuery({
    queryKey: accountQueryKeys.registrationTypes(false),
    queryFn: () => getRegistrationTypesRequest(false),
  })
  const minorRoles = useQuery({
    queryKey: accountQueryKeys.registrationTypes(true),
    queryFn: () => getRegistrationTypesRequest(true),
  })

  const mutation = useMutation({
    mutationFn: (payload: RegistrationForm) => registerRequest(payload),
    onSuccess: (result) => {
      createdUserId.value = result.adultId
      verificationCode.value = result.verificationCode
      submittedRoleName.value =
        (adultRoles.data.value ?? []).find((role) => role.id === form.roleId)?.name ?? ''
      submittedMinorCount.value = result.minorCount || form.minors.length
      step.value = 'success'
      scrollToTop()
    },
  })

  const verifyMutation = useMutation({
    mutationFn: (otp: string) => {
      if (!createdUserId.value) throw new Error('missing user id')
      return verifyRegistrationRequest(createdUserId.value, otp)
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
    mutation.mutate({ ...form })
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
    isVerifying: verifyMutation.isPending,
  }
}
