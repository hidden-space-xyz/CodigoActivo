<script setup lang="ts">
import { formatDateTime } from '@/shared/lib/date'
import { ActionButton } from '@/shared/ui/action-button'
import { AdminPageHeader } from '@/shared/ui/admin-page-header'
import { DataTable } from '@/shared/ui/data-table'

import { authorityLabelKey } from '../lib/labels'
import { useBlockedData } from '../model/use-blocked-data'
import ConsultationPanel from './ConsultationPanel.vue'
import OpenConsultationDialog from './OpenConsultationDialog.vue'

const {
  table,
  isOfficer,
  usesAuthenticator,
  email,
  availableEntities,
  dialogVisible,
  codeStep,
  openError,
  sendingCode,
  opening,
  consultation,
  query,
  candidates,
  searched,
  searching,
  accountId,
  range,
  entities,
  extracting,
  startConsultation,
  continueConsultation,
  resendCode,
  openConsultation,
  search,
  downloadExtract,
  closeConsultation,
} = useBlockedData()
</script>

<template>
  <div>
    <AdminPageHeader
      :title="$t('pages.admin.blockedData.header.title')"
      :subtitle="$t('pages.admin.blockedData.header.subtitle')"
    >
      <template v-if="isOfficer" #actions>
        <ActionButton
          :label="$t('pages.admin.blockedData.open.action')"
          type="primary"
          icon="lock"
          :disabled="consultation !== null"
          @click="startConsultation"
        />
      </template>
    </AdminPageHeader>

    <ConsultationPanel
      v-if="consultation"
      v-model:query="query"
      v-model:account-id="accountId"
      v-model:range="range"
      v-model:entities="entities"
      :consultation="consultation"
      :candidates="candidates"
      :searched="searched"
      :searching="searching"
      :extracting="extracting"
      :available-entities="availableEntities"
      @search="search"
      @download="downloadExtract"
      @close="closeConsultation"
    />

    <DataTable
      :table="table"
      :empty-text="$t('pages.admin.blockedData.empty.none')"
      :error-text="$t('pages.admin.blockedData.empty.error')"
    >
      <el-table-column
        prop="openedAt"
        :label="$t('pages.admin.blockedData.columns.openedAt')"
        width="180"
        sortable="custom"
      >
        <template #default="{ row }">{{ formatDateTime(row.openedAt) }}</template>
      </el-table-column>
      <el-table-column :label="$t('pages.admin.blockedData.columns.officer')" min-width="180">
        <template #default="{ row }">{{
          row.officerName ?? $t('pages.admin.blockedData.columns.erasedOfficer')
        }}</template>
      </el-table-column>
      <el-table-column :label="$t('pages.admin.blockedData.columns.authority')" min-width="200">
        <template #default="{ row }">{{ $t(authorityLabelKey(row.authority)) }}</template>
      </el-table-column>
      <el-table-column
        prop="reference"
        :label="$t('pages.admin.blockedData.columns.reference')"
        min-width="220"
        sortable="custom"
      />
      <el-table-column
        prop="foundAccounts"
        :label="$t('pages.admin.blockedData.columns.foundAccounts')"
        width="140"
        align="right"
      />
      <el-table-column
        prop="extracts"
        :label="$t('pages.admin.blockedData.columns.extracts')"
        width="120"
        align="right"
      />
    </DataTable>

    <OpenConsultationDialog
      v-model:visible="dialogVisible"
      :code-step="codeStep"
      :uses-authenticator="usesAuthenticator"
      :email="email"
      :sending-code="sendingCode"
      :saving="opening"
      :error="openError"
      @continue="continueConsultation"
      @resend="resendCode"
      @submit="openConsultation"
    />
  </div>
</template>
