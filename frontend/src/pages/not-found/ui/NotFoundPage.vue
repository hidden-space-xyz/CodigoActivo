<script setup lang="ts">
import { BrandButton } from '@/shared/ui/brand-button'
import { PageHead } from '@/shared/ui/page-head'
</script>

<template>
  <div class="not-found">
    <PageHead :eyebrow="$t('pages.notFound.eyebrow')" :title="$t('pages.notFound.title')">
      <p class="not-found__text">
        {{ $t('pages.notFound.text') }}
      </p>
      <div class="not-found__actions">
        <BrandButton variant="primary" :to="{ name: 'home' }">{{
          $t('common.backToHome')
        }}</BrandButton>
      </div>
    </PageHead>
  </div>
</template>

<style scoped>
.not-found {
  padding-bottom: 64px;
}

.not-found__text {
  margin-top: 14px;
  font-size: 17px;
  line-height: 1.6;
  color: var(--ca-text-muted);
  max-width: 520px;
}

.not-found__actions {
  margin-top: 28px;
}
</style>
