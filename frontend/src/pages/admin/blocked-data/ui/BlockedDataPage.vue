<script setup lang="ts">
import { useI18n } from 'vue-i18n'

import { formatDateTime } from '@/shared/lib/date'
import { ActionButton } from '@/shared/ui/action-button'
import { AdminPageHeader } from '@/shared/ui/admin-page-header'
import { DataTable } from '@/shared/ui/data-table'
import { RowActions, type RowAction } from '@/shared/ui/row-actions'

import { authorityLabelKey } from '../lib/labels'
import type { Hold } from '../model/types'
import { useBlockedData } from '../model/use-blocked-data'
import ConsultationPanel from './ConsultationPanel.vue'
import OpenConsultationDialog from './OpenConsultationDialog.vue'

const { t } = useI18n()

const {
  table,
  holdTable,
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
  placing,
  lifting,
  startConsultation,
  continueConsultation,
  resendCode,
  openConsultation,
  search,
  downloadExtract,
  placeHold,
  canLift,
  liftHold,
  closeConsultation,
} = useBlockedData()

function holdActions(hold: Hold): RowAction[] {
  return [
    {
      key: 'lift',
      label: t('pages.admin.blockedData.holds.lift.action'),
      icon: 'undo',
      inline: true,
      disabled: lifting.value || !canLift(hold),
      run: () => {
        liftHold(hold)
      },
    },
  ]
}
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
          data-testid="blocked-data-open"
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
      :placing="placing"
      :available-entities="availableEntities"
      @search="search"
      @download="downloadExtract"
      @hold="placeHold"
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

    <section
      class="holds"
      data-testid="blocked-data-holds"
      aria-labelledby="blocked-data-holds-title"
    >
      <h2 id="blocked-data-holds-title" class="holds__title">
        {{ $t('pages.admin.blockedData.holds.title') }}
      </h2>
      <p class="holds__intro">{{ $t('pages.admin.blockedData.holds.intro') }}</p>
      <p v-if="isOfficer && !consultation" class="holds__intro">
        {{ $t('pages.admin.blockedData.holds.liftHint') }}
      </p>
      <DataTable
        :table="holdTable"
        :empty-text="$t('pages.admin.blockedData.holds.empty.none')"
        :error-text="$t('pages.admin.blockedData.holds.empty.error')"
      >
        <el-table-column
          prop="placedAt"
          :label="$t('pages.admin.blockedData.holds.columns.placedAt')"
          width="180"
          sortable="custom"
        >
          <template #default="{ row }">{{ formatDateTime(row.placedAt) }}</template>
        </el-table-column>
        <el-table-column
          :label="$t('pages.admin.blockedData.holds.columns.placedBy')"
          min-width="180"
        >
          <template #default="{ row }">{{
            row.placedByName ?? $t('pages.admin.blockedData.columns.erasedOfficer')
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
          prop="accounts"
          :label="$t('pages.admin.blockedData.holds.columns.accounts')"
          width="120"
          align="right"
        />
        <el-table-column :label="$t('pages.admin.blockedData.holds.columns.state')" min-width="220">
          <template #default="{ row }">
            <template v-if="!row.liftedAt">{{
              $t('pages.admin.blockedData.holds.active')
            }}</template>
            <template v-else-if="row.liftedByName">{{
              $t('pages.admin.blockedData.holds.lifted', {
                date: formatDateTime(row.liftedAt),
                name: row.liftedByName,
              })
            }}</template>
            <template v-else>{{
              $t('pages.admin.blockedData.holds.liftedByErased', {
                date: formatDateTime(row.liftedAt),
              })
            }}</template>
          </template>
        </el-table-column>
        <el-table-column
          v-if="isOfficer"
          :label="$t('common.actions')"
          width="100"
          align="right"
          fixed="right"
        >
          <template #default="{ row }">
            <RowActions :actions="holdActions(row)" />
          </template>
        </el-table-column>
      </DataTable>
    </section>

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

<style scoped>
.holds {
  margin-top: 32px;
}

.holds__title {
  margin: 0;
  font-size: 1.1rem;
}

.holds__intro {
  margin: 4px 0 12px;
  color: var(--ca-text-muted);
}
</style>
