<script setup lang="ts">
import { BrandButton } from '@/shared/ui/brand-button'

import { usePersonalDataDownload } from '../model/use-personal-data-download'

const { download, isDownloading } = usePersonalDataDownload()
</script>

<template>
  <section class="acc-pane">
    <div class="acc-pane__head">
      <div class="acc-pane__heading">
        <h2 class="acc-pane__title">{{ $t('pages.account.personalData.title') }}</h2>
        <p class="acc-pane__lead">{{ $t('pages.account.personalData.lead') }}</p>
      </div>
      <BrandButton variant="ghost" :loading="isDownloading" @click="download">
        {{ $t('pages.account.personalData.download') }}
      </BrandButton>
    </div>
  </section>
</template>

<style scoped>
.acc-pane__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  flex-wrap: wrap;
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
</style>
