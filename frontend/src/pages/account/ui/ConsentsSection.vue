<script setup lang="ts">
import { formatDate, formatDateTime } from '@/shared/lib/date'
import { BrandButton } from '@/shared/ui/brand-button'

import { consentKey, useAccountConsents } from '../model/use-account-consents'

const { consents, entries, isWithdrawing, withdraw } = useAccountConsents()
</script>

<template>
  <section class="acc-pane" data-testid="consents-section">
    <div class="acc-pane__head">
      <div class="acc-pane__heading">
        <h2 class="acc-pane__title">{{ $t('pages.account.consents.title') }}</h2>
        <p class="acc-pane__lead">{{ $t('pages.account.consents.lead') }}</p>
      </div>
    </div>

    <p v-if="consents.isLoading.value" class="acc-pane__state">{{ $t('common.loading') }}</p>
    <p v-else-if="consents.isError.value" class="acc-pane__state">
      {{ $t('pages.account.consents.error') }}
    </p>
    <p v-else-if="entries.length === 0" class="acc-pane__state" data-testid="consents-empty">
      {{ $t('pages.account.consents.empty') }}
    </p>

    <ul v-else class="acc-items">
      <li
        v-for="consent in entries"
        :key="consentKey(consent)"
        class="acc-item"
        data-testid="consents-item"
      >
        <div class="acc-item__info">
          <span class="acc-item__name">
            {{ consent.documentName }} · {{ consent.personName }}
          </span>
          <span class="acc-item__meta">
            {{ consent.eventTitle }} · {{ formatDate(consent.eventStartsAt) }}
          </span>
          <span class="acc-item__meta">
            {{
              $t('pages.account.consents.decided', {
                name: consent.decidedByName,
                date: formatDateTime(consent.decidedAt),
              })
            }}
          </span>
        </div>
        <div class="acc-item__actions">
          <el-tag
            :type="consent.accepted ? 'success' : consent.withdrawn ? 'warning' : 'info'"
            size="small"
            disable-transitions
          >
            {{
              consent.accepted
                ? $t('pages.account.consents.given')
                : consent.withdrawn
                  ? $t('pages.account.consents.withdrawn')
                  : $t('pages.account.consents.notGiven')
            }}
          </el-tag>
          <BrandButton
            v-if="consent.accepted"
            variant="ghost"
            data-testid="consents-item-withdraw"
            :loading="isWithdrawing(consent)"
            @click="withdraw(consent)"
          >
            {{ $t('pages.account.consents.withdraw.action') }}
          </BrandButton>
        </div>
      </li>
    </ul>
  </section>
</template>

<style scoped>
.acc-pane__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  flex-wrap: wrap;
  margin-bottom: 18px;
}

.acc-pane__heading {
  flex: 1;
  min-width: 240px;
}

.acc-pane__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 17px;
  color: var(--ca-text-bright);
}

.acc-pane__lead {
  margin-top: 5px;
  font-size: 14px;
  line-height: 1.5;
  color: var(--ca-text-muted);
  max-width: 62ch;
}

.acc-item__actions {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-shrink: 0;
}

@media (max-width: 640px) {
  .acc-item__actions {
    justify-content: space-between;
  }
}
</style>
