<script setup lang="ts">
import { useRoute } from 'vue-router'

import { useAuth } from '@/features/auth'
import { PRIMARY_NAV } from '@/shared/config'
import { BaseButton, BrandLogo, ThemeToggle } from '@/shared/ui'

const route = useRoute()
const { isAuthenticated, isAdmin, displayName, logout } = useAuth()

function isActive(routeName: string): boolean {
  if (route.name === routeName) return true
  return routeName === 'events' && route.name === 'event-detail'
}
</script>

<template>
  <header class="header">
    <div class="header__inner">
      <RouterLink :to="{ name: 'home' }" class="header__brand" :aria-label="$t('layout.brandAria')">
        <BrandLogo />
      </RouterLink>

      <div class="header__spacer" />

      <nav class="header__nav" :aria-label="$t('layout.navAria')">
        <RouterLink
          v-for="item in PRIMARY_NAV"
          :key="item.routeName"
          :to="{ name: item.routeName }"
          class="header__link"
          :class="{ 'header__link--active': isActive(item.routeName) }"
        >
          {{ $t(item.labelKey) }}
        </RouterLink>

        <template v-if="isAuthenticated">
          <RouterLink v-if="isAdmin" :to="{ name: 'admin-dashboard' }" class="header__link">{{
            $t('common.admin')
          }}</RouterLink>
          <RouterLink
            :to="{ name: 'account' }"
            class="header__link"
            :class="{ 'header__link--active': isActive('account') }"
          >
            {{ $t('common.myAccount') }}
          </RouterLink>
          <span class="header__greeting">{{ $t('common.greeting', { name: displayName }) }}</span>
          <ThemeToggle class="header__theme" />
          <BaseButton variant="ghost" class="header__cta" @click="logout()">
            {{ $t('common.logout') }}
          </BaseButton>
        </template>
        <template v-else>
          <ThemeToggle class="header__theme" />
          <BaseButton :to="{ name: 'login' }" variant="link" class="header__login">
            {{ $t('common.login') }}
          </BaseButton>
          <BaseButton :to="{ name: 'register' }" variant="primary" class="header__cta">
            {{ $t('common.register') }}
          </BaseButton>
        </template>
      </nav>
    </div>
  </header>
</template>

<style scoped>
.header {
  position: sticky;
  top: 0;
  z-index: 50;
  background: var(--ca-glass-bg);
  backdrop-filter: blur(14px);
  -webkit-backdrop-filter: blur(14px);
  border-bottom: 1px solid var(--ca-border);
  animation: ca-header-in 0.55s cubic-bezier(0.16, 1, 0.3, 1) both;
}

.header__inner {
  max-width: var(--ca-container);
  margin: 0 auto;
  padding: 14px 24px;
  display: flex;
  align-items: center;
  gap: 24px;
}

.header__brand {
  text-decoration: none;
  transition: transform 0.2s ease;
}

.header__brand:hover {
  transform: translateY(-1px);
}

.header__spacer {
  flex: 1;
}

.header__nav {
  display: flex;
  align-items: center;
  gap: 22px;
}

.header__link {
  position: relative;
  font-size: 15px;
  font-weight: 600;
  color: var(--ca-text-muted);
  text-decoration: none;
  animation: ca-navitem-in 0.5s ease both;
  transition: color 0.18s ease;
}

.header__link::after {
  content: '';
  position: absolute;
  left: 0;
  right: 0;
  bottom: -7px;
  height: 2px;
  border-radius: 2px;
  background: var(--ca-orange);
  transform: scaleX(0);
  transform-origin: left center;
  transition: transform 0.25s cubic-bezier(0.16, 1, 0.3, 1);
}

.header__link:hover,
.header__link--active {
  color: var(--ca-text-bright);
}

.header__link:hover::after,
.header__link--active::after {
  transform: scaleX(1);
}

.header__link:nth-child(1) {
  animation-delay: 0.08s;
}
.header__link:nth-child(2) {
  animation-delay: 0.16s;
}
.header__link:nth-child(3) {
  animation-delay: 0.24s;
}
.header__link:nth-child(4) {
  animation-delay: 0.32s;
}

.header__cta {
  font-size: 14px;
  padding: 10px 18px;
  border-radius: 10px;
}

.header__greeting {
  font-size: 14px;
  font-weight: 600;
  color: var(--ca-text);
  white-space: nowrap;
}

@keyframes ca-header-in {
  from {
    opacity: 0;
    transform: translateY(-100%);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@keyframes ca-navitem-in {
  from {
    opacity: 0;
    transform: translateY(-6px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@media (max-width: 720px) {
  .header__nav {
    gap: 14px;
  }
  .header__link {
    display: none;
  }
  .header__greeting {
    display: none;
  }
}
</style>
