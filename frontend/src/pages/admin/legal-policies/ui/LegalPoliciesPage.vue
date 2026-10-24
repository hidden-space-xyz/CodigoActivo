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
      <el-tab-pane :label="$t('pages.admin.legalPolicies.tabs.Privacy')" name="Privacy">
        <LegalPolicyEditor
          kind="Privacy"
          :label="$t('pages.admin.legalPolicies.tabs.Privacy')"
          :hint="$t('pages.admin.legalPolicies.privacyHint')"
        />
      </el-tab-pane>
      <el-tab-pane :label="$t('pages.admin.legalPolicies.tabs.Cookies')" name="Cookies">
        <LegalPolicyEditor kind="Cookies" :label="$t('pages.admin.legalPolicies.tabs.Cookies')" />
      </el-tab-pane>
    </el-tabs>
  </div>
</template>
