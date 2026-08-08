<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { CertificatesSection, HistorySection, MinorsSection, ProfileSection } from '@/features/account'
import { AppIcon, SectionEyebrow } from '@/shared/ui'

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
        <SectionEyebrow :text="$t('pages.account.eyebrow')" color="var(--ca-orange-ink)" />
        <h1 class="account-head__title">{{ $t('pages.account.title') }}</h1>
        <p class="account-head__intro">
          {{ $t('pages.account.intro') }}
        </p>
      </div>
    </section>

    <section class="account-body">
      <div class="ca-container--narrow">
        <div class="account-panel">
          <el-tabs v-model="tab" class="account-tabs">
            <el-tab-pane name="profile" lazy>
              <template #label>
                <span class="account-tabs__label">
                  <AppIcon class="account-tabs__icon" name="user" />
                  <span>{{ $t('pages.account.tabs.profile') }}</span>
                </span>
              </template>
              <ProfileSection />
              <hr class="account-divider" />
              <MinorsSection />
            </el-tab-pane>

            <el-tab-pane name="history" lazy>
              <template #label>
                <span class="account-tabs__label">
                  <AppIcon class="account-tabs__icon" name="clock" />
                  <span>{{ $t('pages.account.tabs.history') }}</span>
                </span>
              </template>
              <HistorySection />
            </el-tab-pane>

            <el-tab-pane name="certificates" lazy>
              <template #label>
                <span class="account-tabs__label">
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
  padding: 64px var(--ca-gutter) 16px;
}

.account-head__glow {
  position: absolute;
  inset: 0;
  background: radial-gradient(700px 400px at 80% -20%, var(--ca-orange-soft), transparent 60%);
}

.account-head__inner {
  position: relative;
}

.account-head__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 42px;
  letter-spacing: -0.03em;
  color: var(--ca-text-bright);
}

.account-head__intro {
  margin-top: 14px;
  font-size: 17px;
  line-height: 1.6;
  color: var(--ca-text-muted);
  max-width: 560px;
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
    padding-top: 40px;
  }

  .account-head__title {
    font-size: 32px;
  }

  .account-panel {
    padding: 6px var(--ca-gutter) 20px;
  }

  .account-tabs__icon {
    display: none;
  }
}
</style>
