<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import {
  AdminPageHeader,
  AppButton as Button,
  ColorTag,
  ColumnFilterSelect,
  ColumnSearch,
  DataState,
} from '@/shared/ui'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import type { DataTablePageEvent, DataTableSortEvent } from 'primevue/datatable'
import Dialog from 'primevue/dialog'
import Select from 'primevue/select'
import Tag from 'primevue/tag'

import { useActivityAssignments, useAssignments } from '@/features/manage-activities'
import { useAssignmentStatusTypesList } from '@/entities/catalog'
import type { ActivityAssignmentRowResponse } from '@/shared/api/generated/models'
import { ageFrom, useCrudFeedback, useHierarchyFilter } from '@/shared/lib'

const route = useRoute()
const eventId = computed(() => String(route.params.eventId))
const activityId = computed(() => String(route.params.activityId))

const feedback = useCrudFeedback()
const report = useActivityAssignments(activityId)
const assignments = useAssignments(eventId)
const statusTypes = useAssignmentStatusTypesList()

const NOT_SIGNED_UP = 'not-signed-up'

type AssignmentRow = ActivityAssignmentRowResponse & { age: number | null }

const assignmentRows = computed<AssignmentRow[]>(() =>
  (report.data.value?.rows ?? []).map((row) => ({ ...row, age: ageFrom(row.birthDate) })),
)

const summaryCards = computed(() => {
  const data = report.data.value
  const cards = [{ label: 'Apuntados', value: data?.totalSignups ?? 0 }]
  for (const role of data?.roleTypeBreakdown ?? []) {
    cards.push({ label: role.roleTypeName ?? '—', value: role.approvedAssignments ?? 0 })
  }
  return cards
})

function fullName(row: ActivityAssignmentRowResponse): string {
  return `${row.firstName ?? ''} ${row.lastName ?? ''}`.trim() || '—'
}

const firstNameQuery = ref<string | number | null>(null)
const lastNameQuery = ref<string | number | null>(null)
const emailQuery = ref<string | number | null>(null)
const phoneQuery = ref<string | number | null>(null)
const roleFilter = ref<string | boolean | null>(null)
const statusFilter = ref<string | boolean | null>(null)

const roleFilterOptions = computed(() =>
  (report.data.value?.roleTypeBreakdown ?? []).map((role) => ({
    label: role.roleTypeName ?? '—',
    value: role.roleTypeId ?? '',
  })),
)

const statusFilterOptions = computed(() => {
  const options = (statusTypes.data.value ?? []).map((status) => ({
    label: status.name ?? '—',
    value: status.id ?? '',
  }))
  options.push({ label: 'No apuntado', value: NOT_SIGNED_UP })
  return options
})

function textMatch(haystack: string, query: string | number | null): boolean {
  if (query == null || query === '') return true
  return haystack.toLowerCase().includes(String(query).toLowerCase())
}

function matchesStatus(row: ActivityAssignmentRowResponse): boolean {
  const value = statusFilter.value
  if (value == null) return true
  if (value === NOT_SIGNED_UP) return !row.signedUp
  return !!row.signedUp && row.statusId === value
}

function matches(row: ActivityAssignmentRowResponse): boolean {
  return (
    textMatch(row.firstName ?? '', firstNameQuery.value) &&
    textMatch(row.lastName ?? '', lastNameQuery.value) &&
    textMatch(row.email ?? '', emailQuery.value) &&
    textMatch(row.phone ?? '', phoneQuery.value) &&
    (roleFilter.value == null || (!!row.signedUp && row.roleTypeId === roleFilter.value)) &&
    matchesStatus(row)
  )
}

const filterActive = computed(
  () =>
    (firstNameQuery.value != null && firstNameQuery.value !== '') ||
    (lastNameQuery.value != null && lastNameQuery.value !== '') ||
    (emailQuery.value != null && emailQuery.value !== '') ||
    (phoneQuery.value != null && phoneQuery.value !== '') ||
    roleFilter.value != null ||
    statusFilter.value != null,
)

const sortField = ref<string | undefined>(undefined)
const sortOrder = ref<1 | -1>(1)

function onSort(event: DataTableSortEvent): void {
  sortField.value = typeof event.sortField === 'string' ? event.sortField : undefined
  sortOrder.value = event.sortOrder === -1 ? -1 : 1
}

const first = ref(0)

function onPage(event: DataTablePageEvent): void {
  first.value = event.first
}

watch([firstNameQuery, lastNameQuery, emailQuery, phoneQuery, roleFilter, statusFilter], () => {
  first.value = 0
})

const table = useHierarchyFilter<AssignmentRow>({
  items: () => assignmentRows.value,
  getId: (row) => row.userId,
  getParentId: (row) => row.parentId,
  getName: (row) => fullName(row),
  matches,
  filterActive,
  sortActive: () => !!sortField.value,
})

function rowClass(row: AssignmentRow): string {
  return table.treeMode.value && table.isChild(row) ? 'assignment-row--child' : ''
}

const statusColorById = computed(() => {
  const map = new Map<string, string>()
  for (const status of statusTypes.data.value ?? []) {
    if (status.id) map.set(status.id, status.color ?? '')
  }
  return map
})

function statusColor(row: ActivityAssignmentRowResponse): string | null {
  return row.statusId ? (statusColorById.value.get(row.statusId) ?? null) : null
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
      },
      onError: (error) => feedback.error(error),
    },
  )
}

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
      },
      onError: (error) => feedback.error(error),
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
          :value="table.rows.value"
          :row-class="rowClass"
          data-key="userId"
          striped-rows
          paginator
          :rows="10"
          :first="first"
          :sort-field="sortField"
          :sort-order="sortOrder"
          removable-sort
          @page="onPage"
          @sort="onSort"
        >
          <template #empty>Sin coincidencias.</template>

          <Column field="firstName" sortable>
            <template #header>
              <ColumnSearch v-model="firstNameQuery" label="Nombre" placeholder="Buscar nombre" />
            </template>
            <template #body="{ data }">
              <div
                class="assignment-name"
                :class="{
                  'assignment-name--child': table.treeMode.value && table.isChild(data),
                }"
                :style="
                  table.treeMode.value
                    ? { paddingLeft: table.depthOf(data) * 22 + 'px' }
                    : undefined
                "
              >
                <i
                  v-if="table.treeMode.value && table.isChild(data)"
                  class="pi pi-angle-right assignment-name__child-icon"
                />
                <span>{{ data.firstName || '—' }}</span>
                <Tag
                  v-if="table.treeMode.value && table.childCountOf(data) > 0"
                  :value="String(table.childCountOf(data))"
                  icon="pi pi-users"
                  severity="secondary"
                  class="assignment-name__count"
                />
              </div>
            </template>
          </Column>
          <Column field="lastName" sortable>
            <template #header>
              <ColumnSearch
                v-model="lastNameQuery"
                label="Apellidos"
                placeholder="Buscar apellidos"
              />
            </template>
            <template #body="{ data }">{{ data.lastName || '—' }}</template>
          </Column>
          <Column field="age" header="Edad" sortable style="width: 70px">
            <template #body="{ data }">{{ ageFrom(data.birthDate) ?? '—' }}</template>
          </Column>
          <Column field="email" sortable>
            <template #header>
              <ColumnSearch v-model="emailQuery" label="Correo" placeholder="Buscar correo" />
            </template>
            <template #body="{ data }">{{ data.email || '—' }}</template>
          </Column>
          <Column field="phone" sortable>
            <template #header>
              <ColumnSearch v-model="phoneQuery" label="Teléfono" placeholder="Buscar teléfono" />
            </template>
            <template #body="{ data }">{{ data.phone || '—' }}</template>
          </Column>
          <Column field="roleTypeName" sortable>
            <template #header>
              <ColumnFilterSelect v-model="roleFilter" label="Rol" :options="roleFilterOptions" />
            </template>
            <template #body="{ data }">
              {{ data.signedUp && data.roleTypeName ? data.roleTypeName : '—' }}
            </template>
          </Column>
          <Column field="statusName" sortable>
            <template #header>
              <ColumnFilterSelect
                v-model="statusFilter"
                label="Estado"
                :options="statusFilterOptions"
              />
            </template>
            <template #body="{ data }">
              <ColorTag
                v-if="data.signedUp"
                :value="data.statusName || '—'"
                :color="statusColor(data)"
              />
              <Tag v-else value="No apuntado" severity="secondary" />
            </template>
          </Column>
          <Column v-if="!table.treeMode.value" header="Tutor">
            <template #body="{ data }">{{ table.parentName(data.parentId) }}</template>
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

    <Dialog
      v-model:visible="roleDialogVisible"
      modal
      header="Cambiar rol"
      :style="{ width: '400px' }"
    >
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
