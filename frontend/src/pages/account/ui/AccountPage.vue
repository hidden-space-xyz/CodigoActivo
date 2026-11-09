<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { AppIcon } from '@/shared/ui/app-icon'
import { PageHeading } from '@/shared/ui/page-heading'

import CertificatesSection from './CertificatesSection.vue'
import ConsentsSection from './ConsentsSection.vue'
import DeleteAccountSection from './DeleteAccountSection.vue'
import HistorySection from './HistorySection.vue'
import MinorsSection from './MinorsSection.vue'
import PersonalDataSection from './PersonalDataSection.vue'
import ProfileSection from './ProfileSection.vue'
import TwoFactorSection from './TwoFactorSection.vue'

const TABS = ['profile', 'history', 'certificates'] as const
type AccountTab = (typeof TABS)[number]

const DEFAULT_TAB: AccountTab = 'profile'

const route = useRoute()
const router = useRouter()

const tab = computed<AccountTab>({
  get: () => {
    const value = String(route.query.tab ?? '')
    return (TABS as readonly string[]).includes(value) ? (value as AccountTab) : DEFAULT_TAB
  },
  set: (value) => {
    const query = { ...route.query }
    if (value === DEFAULT_TAB) delete query.tab
    else query.tab = value
    void router.replace({ query })
  },
})
</script>

<template>
  <div>
    <section class="account-head">
      <div class="account-head__glow" aria-hidden="true" />
      <div class="ca-container--narrow account-head__inner">
        <PageHeading :title="$t('pages.account.title')" :description="$t('pages.account.intro')" />
      </div>
    </section>

    <section class="account-body">
      <div class="ca-container--narrow">
        <div class="account-panel">
          <el-tabs v-model="tab" class="account-tabs">
            <el-tab-pane name="profile" lazy>
              <template #label>
                <span class="account-tabs__label" data-testid="account-tab-profile">
                  <AppIcon class="account-tabs__icon" name="user" />
                  <span>{{ $t('pages.account.tabs.profile') }}</span>
                </span>
              </template>
              <ProfileSection />
              <hr class="account-divider" />
              <TwoFactorSection />
              <hr class="account-divider" />
              <MinorsSection />
              <hr class="account-divider" />
              <ConsentsSection />
              <hr class="account-divider" />
              <PersonalDataSection />
              <hr class="account-divider" />
              <DeleteAccountSection />
            </el-tab-pane>

            <el-tab-pane name="history" lazy>
              <template #label>
                <span class="account-tabs__label" data-testid="account-tab-history">
                  <AppIcon class="account-tabs__icon" name="clock" />
                  <span>{{ $t('pages.account.tabs.history') }}</span>
                </span>
              </template>
              <HistorySection />
            </el-tab-pane>

            <el-tab-pane name="certificates" lazy>
              <template #label>
                <span class="account-tabs__label" data-testid="account-tab-certificates">
                  <AppIcon class="account-tabs__icon" name="id-card" />
                  <span>{{ $t('pages.account.tabs.certificates') }}</span>
                </span>
              </template>
              <CertificatesSection />
            </el-tab-pane>
          </el-tabs>
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped>
.account-head {
  position: relative;
  overflow: hidden;
  padding: 48px var(--ca-gutter) 12px;
}

.account-head__glow {
  position: absolute;
  inset: 0;
  background: radial-gradient(700px 400px at 80% -20%, var(--ca-orange-soft), transparent 60%);
}

.account-head__inner {
  position: relative;
}

.account-body {
  padding: 24px var(--ca-gutter) 80px;
}

.account-panel {
  background: var(--ca-bg-elevated);
  border: 1px solid var(--ca-border-strong);
  border-radius: 18px;
  padding: 8px 28px 26px;
}

.account-divider {
  margin: 30px 0 26px;
  border: 0;
  border-top: 1px solid var(--ca-border-soft);
}

.account-tabs__label {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.account-tabs__icon {
  font-size: 15px;
  opacity: 0.85;
}

.account-tabs :deep(.el-tabs__header) {
  margin-bottom: 22px;
}

@media (max-width: 640px) {
  .account-head {
    padding-top: 32px;
  }

  .account-panel {
    padding: 6px var(--ca-gutter) 20px;
  }

  .account-tabs__icon {
    display: none;
  }
}
</style>

<style>
.acc-pane__state {
  color: var(--ca-text-dim);
  font-family: var(--ca-font-mono);
}

.acc-pane__actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.acc-items {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.acc-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 12px;
  padding: 14px 16px;
}

.acc-item__info {
  display: flex;
  flex-direction: column;
  gap: 3px;
  min-width: 0;
}

.acc-item__name {
  font-weight: 600;
  color: var(--ca-text-bright);
}

.acc-item__meta {
  font-size: 13px;
  color: var(--ca-text-muted);
}

.acc-info {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 14px 24px;
}

.acc-info__row {
  display: flex;
  flex-direction: column;
  gap: 3px;
  min-width: 0;
}

.acc-info__row dt {
  font-size: 12px;
  color: var(--ca-text-dim);
}

.acc-info__row dd {
  margin: 0;
  font-weight: 600;
  color: var(--ca-text);
  overflow-wrap: anywhere;
}

.acc-form__grid {
  row-gap: 28px;
  margin-bottom: 14px;
}

.acc-form__intro {
  margin: 0 0 14px;
  font-size: 14px;
  line-height: 1.55;
  color: var(--ca-text-muted);
}

.acc-form__field {
  margin-bottom: 14px;
}

.acc-form__alert {
  margin-bottom: 10px;
}

.acc-form__actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 8px;
}

@media (max-width: 640px) {
  .acc-pane__actions {
    width: 100%;
  }

  .acc-pane__actions > * {
    flex: 1 1 100%;
  }

  .acc-item {
    flex-direction: column;
    align-items: stretch;
    gap: 12px;
  }

  .acc-info {
    grid-template-columns: 1fr;
  }
}
</style>
