<script setup lang="ts">
import { MinorsSection, ProfileSection } from '@/features/account'
import { SectionEyebrow } from '@/shared/ui'
</script>

<template>
  <div>
    <section class="account-head">
      <div class="account-head__glow" aria-hidden="true" />
      <div class="ca-container--narrow account-head__inner">
        <SectionEyebrow text="// mi cuenta" color="var(--ca-cyan)" />
        <h1 class="account-head__title">Mi área de usuario</h1>
        <p class="account-head__intro">
          Gestiona tus datos, tu contraseña y los menores a tu cargo.
        </p>
      </div>
    </section>

    <section class="account-body">
      <div class="ca-container--narrow account-body__stack">
        <ProfileSection />
        <MinorsSection />
      </div>
    </section>
  </div>
</template>

<style scoped>
.account-head {
  position: relative;
  overflow: hidden;
  padding: 64px 24px 16px;
}

.account-head__glow {
  position: absolute;
  inset: 0;
  background: radial-gradient(700px 400px at 80% -20%, rgba(45, 212, 217, 0.1), transparent 60%);
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
  padding: 24px 24px 80px;
}

.account-body__stack {
  display: flex;
  flex-direction: column;
  gap: 22px;
}
</style>
