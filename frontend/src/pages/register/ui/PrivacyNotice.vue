<script setup lang="ts">
import { useQuery } from '@tanstack/vue-query'

import { legalEntityQueries } from '@/entities/legal-entity'

const { data: legalEntity } = useQuery(legalEntityQueries.detail())
</script>

<template>
  <section class="reg-privacy" aria-labelledby="reg-privacy-title">
    <h2 id="reg-privacy-title" class="reg-privacy__title">
      {{ $t('pages.register.form.privacy.title') }}
    </h2>
    <dl class="reg-privacy__facts">
      <dt>{{ $t('pages.register.form.privacy.controller') }}</dt>
      <dd>{{ legalEntity?.name || '—' }}</dd>
      <dt>{{ $t('pages.register.form.privacy.purpose') }}</dt>
      <dd>{{ $t('pages.register.form.privacy.purposeText') }}</dd>
      <dt>{{ $t('pages.register.form.privacy.legalBasis') }}</dt>
      <dd>{{ $t('pages.register.form.privacy.legalBasisText') }}</dd>
      <dt>{{ $t('pages.register.form.privacy.recipients') }}</dt>
      <dd>{{ $t('pages.register.form.privacy.recipientsText') }}</dd>
      <dt>{{ $t('pages.register.form.privacy.rights') }}</dt>
      <dd>{{ $t('pages.register.form.privacy.rightsText') }}</dd>
      <dt>{{ $t('pages.register.form.privacy.more') }}</dt>
      <i18n-t keypath="pages.register.form.privacy.moreText" tag="dd">
        <template #policy>
          <RouterLink
            :to="{ name: 'privacy-policy' }"
            class="reg-privacy__link"
            target="_blank"
            rel="noopener"
            >{{ $t('pages.register.form.privacy.policy') }}</RouterLink
          >
        </template>
      </i18n-t>
    </dl>
  </section>
</template>

<style scoped>
.reg-privacy {
  margin-top: 20px;
  padding: 14px 16px;
  border: 1px solid var(--ca-border);
  border-radius: 12px;
  background: var(--ca-bg-deep);
}

.reg-privacy__title {
  font-size: 13px;
  font-weight: 700;
  line-height: 1.4;
  color: var(--ca-text);
}

.reg-privacy__facts {
  display: grid;
  grid-template-columns: max-content minmax(0, 1fr);
  gap: 6px 14px;
  margin: 10px 0 0;
  font-size: 12.5px;
  line-height: 1.5;
}

.reg-privacy__facts dt {
  font-weight: 600;
  color: var(--ca-text-dim);
}

.reg-privacy__facts dd {
  margin: 0;
  color: var(--ca-text-muted);
}

.reg-privacy__link {
  font-weight: 600;
  color: var(--ca-orange-ink);
  text-decoration: none;
}

.reg-privacy__link:hover {
  text-decoration: underline;
}

@media (max-width: 640px) {
  .reg-privacy__facts {
    grid-template-columns: minmax(0, 1fr);
    gap: 2px;
  }

  .reg-privacy__facts dd + dt {
    margin-top: 8px;
  }
}
</style>
