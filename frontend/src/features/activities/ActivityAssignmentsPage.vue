<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute } from 'vue-router'
import Button from '@/shared/ui/components/AppButton.vue'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Dialog from 'primevue/dialog'
import Select from 'primevue/select'
import Tag from 'primevue/tag'

import { useAssignments } from '@/features/activities/useAssignments'
import { useActivityAssignments } from '@/features/activities/useActivityReports'
import { useAssignmentStatusTypesList } from '@/features/catalogs/catalogs'
import type { ActivityAssignmentRowResponse } from '@/shared/api/generated/models'
import { getErrorMessage } from '@/shared/utils/api-error'
import { groupByParent } from '@/shared/utils/group-by-parent'
import AdminPageHeader from '@/shared/ui/admin/AdminPageHeader.vue'
import DataState from '@/shared/ui/admin/DataState.vue'
import { useCrudFeedback } from '@/shared/ui/admin/use-crud-feedback'

const route = useRoute()
const eventId = computed(() => String(route.params.eventId))
const activityId = computed(() => String(route.params.activityId))

const feedback = useCrudFeedback()
const report = useActivityAssignments(activityId)
const assignments = useAssignments(eventId.value)
const statusTypes = useAssignmentStatusTypesList()

const summaryCards = computed(() => {
  const data = report.data.value
  const cards = [{ label: 'Apuntados', value: data?.totalSignups ?? 0 }]
  for (const role of data?.roleTypeBreakdown ?? []) {
    cards.push({ label: role.roleTypeName ?? '—', value: role.approvedAssignments ?? 0 })
  }
  return cards
})

// Group the sign-ups by parentId so each parent is shown with its children, even
// when the parent did not sign up to this activity (those rows arrive with signedUp=false).
const grouped = computed(() =>
  groupByParent(
    report.data.value?.rows ?? [],
    (row) => row.userId,
    (row) => row.parentId,
  ),
)

function userDepth(row: ActivityAssignmentRowResponse): number {
  return row.userId ? (grouped.value.depthById.get(row.userId) ?? 0) : 0
}

function isChild(row: ActivityAssignmentRowResponse): boolean {
  return userDepth(row) > 0
}

function childCount(row: ActivityAssignmentRowResponse): number {
  return row.userId ? (grouped.value.childrenByParent.get(row.userId)?.length ?? 0) : 0
}

function rowClass(row: ActivityAssignmentRowResponse): string {
  return isChild(row) ? 'assignment-row--child' : ''
}

function statusSeverity(name?: string | null): string {
  switch (name) {
    case 'Confirmada':
      return 'success'
    case 'Rechazada':
      return 'danger'
    case 'Solicitada':
      return 'warn'
    default:
      return 'info'
  }
}

const statusDialogVisible = ref(false)
const statusTarget = ref<ActivityAssignmentRowResponse | null>(null)
const selectedStatusId = ref<string | null>(null)

function openChangeStatus(row: ActivityAssignmentRowResponse): void {
  statusTarget.value = row
  selectedStatusId.value = row.statusId ?? null
  statusDialogVisible.value = true
}

function submitChangeStatus(): void {
  const target = statusTarget.value
  if (!target?.userId || !selectedStatusId.value) return
  assignments.changeStatus.mutate(
    {
      activityId: activityId.value,
      userId: target.userId,
      body: { assignmentStatusId: selectedStatusId.value },
    },
    {
      onSuccess: () => {
        feedback.success('Estado actualizado.')
        statusDialogVisible.value = false
        void report.refetch()
      },
      onError: (error) => feedback.error(getErrorMessage(error)),
    },
  )
}

// Role options are the activity's allowed role types, which the summary breakdown already lists.
const roleOptions = computed(() => report.data.value?.roleTypeBreakdown ?? [])

const roleDialogVisible = ref(false)
const roleTarget = ref<ActivityAssignmentRowResponse | null>(null)
const selectedRoleId = ref<string | null>(null)

function openChangeRole(row: ActivityAssignmentRowResponse): void {
  roleTarget.value = row
  selectedRoleId.value = row.roleTypeId ?? null
  roleDialogVisible.value = true
}

function submitChangeRole(): void {
  const target = roleTarget.value
  if (!target?.userId || !selectedRoleId.value) return
  assignments.changeRole.mutate(
    {
      activityId: activityId.value,
      userId: target.userId,
      body: { activityRoleTypeId: selectedRoleId.value },
    },
    {
      onSuccess: () => {
        feedback.success('Rol actualizado.')
        roleDialogVisible.value = false
        void report.refetch()
      },
      onError: (error) => feedback.error(getErrorMessage(error)),
    },
  )
}
</script>

<template>
  <div>
    <RouterLink :to="{ name: 'admin-event-detail', params: { eventId } }" class="back">
      ← Volver al evento
    </RouterLink>

    <AdminPageHeader
      :title="report.data.value?.title ?? 'Actividad'"
      subtitle="Usuarios apuntados a esta actividad"
    />

    <div class="summary">
      <div v-for="card in summaryCards" :key="card.label" class="summary__card">
        <div class="summary__value">{{ card.value }}</div>
        <div class="summary__label">{{ card.label }}</div>
      </div>
    </div>

    <section class="block">
      <h2 class="block__title">Asignaciones</h2>
      <DataState
        :loading="report.isLoading.value"
        :error="report.isError.value"
        :empty="(report.data.value?.rows?.length ?? 0) === 0"
        empty-text="Todavía no hay usuarios apuntados a esta actividad."
      >
        <DataTable
          :value="grouped.rows"
          :row-class="rowClass"
          data-key="userId"
          striped-rows
          paginator
          :rows="10"
        >
          <Column header="Nombre">
            <template #body="{ data }">
              <div
                class="assignment-name"
                :class="{ 'assignment-name--child': isChild(data) }"
                :style="{ paddingLeft: userDepth(data) * 22 + 'px' }"
              >
                <i v-if="isChild(data)" class="pi pi-angle-right assignment-name__child-icon" />
                <span>{{ data.firstName || '—' }}</span>
                <Tag
                  v-if="childCount(data) > 0"
                  :value="String(childCount(data))"
                  icon="pi pi-users"
                  severity="secondary"
                  class="assignment-name__count"
                />
              </div>
            </template>
          </Column>
          <Column header="Apellidos">
            <template #body="{ data }">{{ data.lastName || '—' }}</template>
          </Column>
          <Column header="Correo">
            <template #body="{ data }">{{ data.email || '—' }}</template>
          </Column>
          <Column header="Teléfono">
            <template #body="{ data }">{{ data.phone || '—' }}</template>
          </Column>
          <Column header="Rol">
            <template #body="{ data }">
              <Tag
                v-if="data.signedUp && data.roleTypeName"
                :value="data.roleTypeName"
                severity="secondary"
              />
              <span v-else>—</span>
            </template>
          </Column>
          <Column header="Estado">
            <template #body="{ data }">
              <Tag
                v-if="data.signedUp"
                :value="data.statusName || '—'"
                :severity="statusSeverity(data.statusName)"
              />
              <Tag v-else value="No apuntado" severity="secondary" />
            </template>
          </Column>
          <Column header="Acciones" style="width: 130px">
            <template #body="{ data }">
              <div class="row-actions">
                <Button
                  v-if="data.signedUp"
                  icon="pi pi-tag"
                  text
                  rounded
                  aria-label="Cambiar rol"
                  @click="openChangeRole(data)"
                />
                <Button
                  v-if="data.signedUp"
                  icon="pi pi-sync"
                  text
                  rounded
                  aria-label="Cambiar estado"
                  @click="openChangeStatus(data)"
                />
              </div>
            </template>
          </Column>
        </DataTable>
      </DataState>
    </section>

    <Dialog
      v-model:visible="statusDialogVisible"
      modal
      header="Cambiar estado"
      :style="{ width: '400px' }"
    >
      <div class="form__field">
        <label>Estado</label>
        <Select
          v-model="selectedStatusId"
          :options="statusTypes.data.value ?? []"
          option-label="name"
          option-value="id"
          placeholder="Selecciona un estado"
          fluid
        />
      </div>
      <template #footer>
        <Button
          label="Cancelar"
          text
          severity="secondary"
          :disabled="assignments.changeStatus.isPending.value"
          @click="statusDialogVisible = false"
        />
        <Button
          label="Aplicar"
          :loading="assignments.changeStatus.isPending.value"
          :disabled="!selectedStatusId"
          @click="submitChangeStatus"
        />
      </template>
    </Dialog>

    <Dialog v-model:visible="roleDialogVisible" modal header="Cambiar rol" :style="{ width: '400px' }">
      <div class="form__field">
        <label>Rol</label>
        <Select
          v-model="selectedRoleId"
          :options="roleOptions"
          option-label="roleTypeName"
          option-value="roleTypeId"
          placeholder="Selecciona un rol"
          fluid
        />
      </div>
      <template #footer>
        <Button
          label="Cancelar"
          text
          severity="secondary"
          :disabled="assignments.changeRole.isPending.value"
          @click="roleDialogVisible = false"
        />
        <Button
          label="Aplicar"
          :loading="assignments.changeRole.isPending.value"
          :disabled="!selectedRoleId"
          @click="submitChangeRole"
        />
      </template>
    </Dialog>
  </div>
</template>

<style scoped>
.back {
  display: inline-block;
  margin-bottom: 14px;
  color: var(--ca-text-muted);
  text-decoration: none;
  font-size: 14px;
}

.back:hover {
  color: var(--ca-text);
}

.summary {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: 12px;
  margin-bottom: 26px;
}

.summary__card {
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 12px;
  padding: 16px;
}

.summary__value {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 26px;
  color: var(--ca-text-bright);
}

.summary__label {
  font-size: 13px;
  color: var(--ca-text-muted);
}

.block {
  margin-bottom: 30px;
}

.block__title {
  font-family: var(--ca-font-display);
  font-size: 20px;
  font-weight: 600;
  color: var(--ca-text-bright);
  margin-bottom: 14px;
}

.assignment-name {
  display: flex;
  align-items: center;
  gap: 8px;
}

.assignment-name--child {
  color: var(--ca-text-muted);
}

.assignment-name__child-icon {
  font-size: 12px;
  color: var(--ca-text-muted);
}

.assignment-name__count {
  font-size: 11px;
}

:deep(.assignment-row--child) > td:first-child {
  border-left: 2px solid var(--ca-border-soft);
}

.row-actions {
  display: flex;
  gap: 2px;
}

.form__field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form__field label {
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-muted);
}
</style>
