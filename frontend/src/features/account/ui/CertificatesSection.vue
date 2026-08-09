<script setup lang="ts">
import { certificateKey, useAccountCertificates } from '../model/useAccountCertificates'
import type { CertificateFormat } from '../model/useAccountCertificates'
import CertificatePreviewDialog from './CertificatePreviewDialog.vue'
import type { AccountCertificate } from '@/entities/account'
import { AppIcon, BaseButton } from '@/shared/ui'
import { formatDateRange, fullName, useCrudFeedback } from '@/shared/lib'

const feedback = useCrudFeedback()
const { certificates, entries, preview, isBusy, open, close, download } = useAccountCertificates()

async function onDownload(
  certificate: AccountCertificate,
  format: CertificateFormat,
): Promise<void> {
  try {
    await download(certificate, format)
  } catch (error) {
    feedback.error(error)
  }
}
</script>

<template>
  <section class="acc-pane">
    <div class="acc-pane__head">
      <p class="acc-pane__lead">{{ $t('features.account.certificates.lead') }}</p>
    </div>

    <p v-if="certificates.isLoading.value" class="acc-pane__state">{{ $t('common.loading') }}</p>
    <p v-else-if="certificates.isError.value" class="acc-pane__state">
      {{ $t('features.account.certificates.error') }}
    </p>
    <p v-else-if="entries.length === 0" class="acc-pane__state">
      {{ $t('features.account.certificates.empty') }}
    </p>

    <ul v-else class="cert-grid">
      <li v-for="certificate in entries" :key="certificateKey(certificate)" class="cert-tile">
        <button
          type="button"
          class="cert-tile__open"
          :aria-label="
            $t('features.account.certificates.openAria', {
              name: fullName(certificate),
              event: certificate.eventTitle,
            })
          "
          @click="open(certificate)"
        >
          <span class="cert-tile__paper" aria-hidden="true">
            <span class="cert-tile__band" />
            <span class="cert-tile__seal">&lt;/&gt;</span>
            <span class="cert-tile__rule" />
          </span>

          <span class="cert-tile__body">
            <span class="cert-tile__name">{{ fullName(certificate) }}</span>
            <span class="cert-tile__event">{{ certificate.eventTitle }}</span>
            <span class="cert-tile__date">{{
              formatDateRange(certificate.startsAt, certificate.endsAt)
            }}</span>
            <span class="cert-tile__code">{{ certificate.code }}</span>
          </span>
        </button>

        <div class="cert-tile__actions">
          <BaseButton variant="ghost" @click="open(certificate)">
            {{ $t('features.account.certificates.view') }}
          </BaseButton>
          <span class="cert-tile__formats">
            <BaseButton
              variant="link"
              :loading="isBusy(certificate, 'png')"
              @click="onDownload(certificate, 'png')"
            >
              <AppIcon v-if="!isBusy(certificate, 'png')" name="download" />
              <span>{{ $t('features.account.certificates.png') }}</span>
            </BaseButton>
            <BaseButton
              variant="link"
              :loading="isBusy(certificate, 'pdf')"
              @click="onDownload(certificate, 'pdf')"
            >
              <AppIcon v-if="!isBusy(certificate, 'pdf')" name="download" />
              <span>{{ $t('features.account.certificates.pdf') }}</span>
            </BaseButton>
          </span>
        </div>
      </li>
    </ul>

    <CertificatePreviewDialog
      :certificate="preview"
      :busy-png="isBusy(preview, 'png')"
      :busy-pdf="isBusy(preview, 'pdf')"
      @download="onDownload"
      @close="close"
    />
  </section>
</template>

<style scoped>
.acc-pane__head {
  margin-bottom: 18px;
}

.acc-pane__lead {
  font-size: 14px;
  line-height: 1.5;
  color: var(--ca-text-muted);
  max-width: 62ch;
}

.acc-pane__state {
  color: var(--ca-text-dim);
  font-family: var(--ca-font-mono);
}

.cert-grid {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 14px;
}

.cert-tile {
  /* Fixed print-like palette: the tile mirrors the paper certificate, so it deliberately
     does not follow the --ca-* theme tokens (dark mode remaps those). */
  --cert-orange: #f9a320;
  --cert-orange-glow: rgb(249 163 32 / 0.16);
  --cert-lime: #7cb518;
  --cert-azure: #159fde;
  --cert-ink: #8f5900;
  --cert-ink-soft: rgb(143 89 0 / 0.28);
  --cert-ink-border: rgb(143 89 0 / 0.45);
  --cert-paper: #fdfbf7;
  --cert-paper-shade: #f4efe6;
  display: flex;
  flex-direction: column;
  border: 1px solid var(--ca-border-soft);
  border-radius: 14px;
  background: var(--ca-surface);
  overflow: hidden;
  transition:
    border-color 0.18s ease,
    transform 0.18s ease;
}

.cert-tile:hover {
  border-color: var(--ca-orange);
  transform: translateY(-2px);
}

.cert-tile__open {
  display: flex;
  flex-direction: column;
  gap: 12px;
  flex: 1;
  padding: 0;
  border: none;
  background: none;
  color: inherit;
  font: inherit;
  text-align: left;
  cursor: pointer;
}

.cert-tile__paper {
  position: relative;
  display: block;
  height: 76px;
  background:
    radial-gradient(120% 160% at 100% 0%, var(--cert-orange-glow), transparent 60%),
    linear-gradient(150deg, var(--cert-paper), var(--cert-paper-shade));
  border-bottom: 1px solid var(--ca-border-soft);
}

.cert-tile__band {
  position: absolute;
  inset: 8px 12px auto;
  height: 3px;
  border-radius: 2px;
  background: linear-gradient(
    90deg,
    var(--cert-orange) 0 34%,
    var(--cert-lime) 34% 67%,
    var(--cert-azure) 67% 100%
  );
  opacity: 0.85;
}

.cert-tile__rule {
  position: absolute;
  inset: auto 12px 12px;
  height: 1px;
  background: var(--cert-ink-soft);
}

.cert-tile__seal {
  position: absolute;
  right: 14px;
  bottom: 20px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 34px;
  height: 34px;
  border: 1px solid var(--cert-ink-border);
  border-radius: 50%;
  background: rgb(255 255 255 / 0.6);
  color: var(--cert-ink);
  font-family: var(--ca-font-mono);
  font-size: 11px;
  font-weight: 600;
}

.cert-tile__body {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 12px 16px 0;
}

.cert-tile__name {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 17px;
  line-height: 1.2;
  color: var(--ca-text-bright);
}

.cert-tile__event {
  font-size: 14px;
  line-height: 1.35;
  color: var(--ca-text);
}

.cert-tile__date {
  font-size: 13px;
  color: var(--ca-text-muted);
}

.cert-tile__code {
  margin-top: 2px;
  font-family: var(--ca-font-mono);
  font-size: 11px;
  letter-spacing: 0.06em;
  color: var(--ca-text-faint);
}

.cert-tile__actions {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  flex-wrap: wrap;
  padding: 14px 16px 16px;
}

.cert-tile__formats {
  display: inline-flex;
  align-items: center;
  gap: 10px;
}

@media (max-width: 640px) {
  .cert-grid {
    grid-template-columns: 1fr;
  }
}
</style>
