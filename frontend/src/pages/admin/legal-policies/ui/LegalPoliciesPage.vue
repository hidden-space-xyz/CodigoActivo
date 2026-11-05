<script setup lang="ts">
import { ref } from 'vue'

import type { LegalPolicyKind } from '@/entities/legal-policy'
import { AdminPageHeader } from '@/shared/ui/admin-page-header'

import LegalPolicyEditor from './LegalPolicyEditor.vue'

const tab = ref<LegalPolicyKind>('Privacy')
</script>

<template>
  <div>
    <AdminPageHeader
      :title="$t('pages.admin.legalPolicies.header.title')"
      :subtitle="$t('pages.admin.legalPolicies.header.subtitle')"
    />

    <el-tabs v-model="tab">
      <el-tab-pane name="Privacy" data-testid="legal-policies-privacy">
        <template #label>
          <span data-testid="legal-policies-privacy-tab">{{
            $t('pages.admin.legalPolicies.tabs.Privacy')
          }}</span>
        </template>
        <LegalPolicyEditor
          kind="Privacy"
          :label="$t('pages.admin.legalPolicies.tabs.Privacy')"
          :hint="$t('pages.admin.legalPolicies.privacyHint')"
        />
      </el-tab-pane>
      <el-tab-pane name="Cookies" data-testid="legal-policies-cookies">
        <template #label>
          <span data-testid="legal-policies-cookies-tab">{{
            $t('pages.admin.legalPolicies.tabs.Cookies')
          }}</span>
        </template>
        <LegalPolicyEditor kind="Cookies" :label="$t('pages.admin.legalPolicies.tabs.Cookies')" />
      </el-tab-pane>
    </el-tabs>
  </div>
</template>
