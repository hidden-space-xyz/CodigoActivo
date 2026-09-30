<script setup lang="ts">
import { ref, toRefs, watch } from 'vue'
import { useRoute } from 'vue-router'

import { useSession } from '@/entities/session'
import { useLogout } from '@/features/logout'
import type { NavSection } from '@/shared/routes'
import { AppIcon } from '@/shared/ui/app-icon'
import { BrandButton } from '@/shared/ui/brand-button'
import { BrandLogo } from '@/shared/ui/brand-logo'
import { ThemeToggle } from '@/shared/ui/theme-toggle'

import { PRIMARY_NAV } from '../config/navigation'

const route = useRoute()
const { isAuthenticated, isAdmin, displayName } = toRefs(useSession())
const logout = useLogout()

const menuOpen = ref(false)

watch(
  () => route.fullPath,
  () => {
    menuOpen.value = false
  },
)

function isActive(section: NavSection): boolean {
  return route.meta.section === section
}
</script>

<template>
  <header class="header">
    <div class="header__inner">
      <RouterLink
        :to="{ name: 'home' }"
        class="header__brand"
        :aria-label="$t('common.brand.name')"
      >
        <BrandLogo />
      </RouterLink>

      <div class="header__spacer" />

      <nav class="header__nav" :aria-label="$t('widgets.siteHeader.navAria')">
        <RouterLink
          v-for="item in PRIMARY_NAV"
          :key="item.routeName"
          :to="{ name: item.routeName }"
          class="header__link"
          :class="{ 'header__link--active': isActive(item.section) }"
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
          <BrandButton variant="ghost" class="header__cta" @click="logout()">
            {{ $t('common.logout') }}
          </BrandButton>
        </template>
        <template v-else>
          <ThemeToggle class="header__theme" />
          <BrandButton :to="{ name: 'login' }" variant="link" class="header__login">
            {{ $t('common.login') }}
          </BrandButton>
          <BrandButton :to="{ name: 'register' }" variant="primary" class="header__cta">
            {{ $t('common.register') }}
          </BrandButton>
        </template>
      </nav>

      <button
        type="button"
        class="header__burger"
        :aria-label="$t('common.openMenu')"
        :aria-expanded="menuOpen"
        @click="menuOpen = true"
      >
        <AppIcon name="bars" />
      </button>
    </div>

    <el-drawer
      v-model="menuOpen"
      append-to-body
      direction="rtl"
      size="min(320px, 86vw)"
      class="header__drawer"
      :title="$t('widgets.siteHeader.menuTitle')"
      :aria-label="$t('widgets.siteHeader.menuTitle')"
    >
      <nav class="menu" :aria-label="$t('widgets.siteHeader.navAria')">
        <p v-if="isAuthenticated" class="menu__greeting">
          {{ $t('common.greeting', { name: displayName }) }}
        </p>

        <RouterLink
          v-for="item in PRIMARY_NAV"
          :key="item.routeName"
          :to="{ name: item.routeName }"
          class="menu__link"
          :class="{ 'menu__link--active': isActive(item.section) }"
          @click="menuOpen = false"
        >
          {{ $t(item.labelKey) }}
        </RouterLink>

        <template v-if="isAuthenticated">
          <RouterLink
            v-if="isAdmin"
            :to="{ name: 'admin-dashboard' }"
            class="menu__link"
            @click="menuOpen = false"
          >
            {{ $t('common.admin') }}
          </RouterLink>
          <RouterLink
            :to="{ name: 'account' }"
            class="menu__link"
            :class="{ 'menu__link--active': isActive('account') }"
            @click="menuOpen = false"
          >
            {{ $t('common.myAccount') }}
          </RouterLink>
        </template>

        <div class="menu__actions">
          <template v-if="isAuthenticated">
            <BrandButton variant="ghost" block @click="logout()">
              {{ $t('common.logout') }}
            </BrandButton>
          </template>
          <template v-else>
            <BrandButton :to="{ name: 'register' }" variant="primary" block>
              {{ $t('common.register') }}
            </BrandButton>
            <BrandButton :to="{ name: 'login' }" variant="ghost" block>
              {{ $t('common.login') }}
            </BrandButton>
          </template>
        </div>
      </nav>
    </el-drawer>
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
  padding: 14px var(--ca-gutter);
  display: flex;
  align-items: center;
  gap: 24px;
}

.header__brand {
  min-width: 0;
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

.header__burger {
  display: none;
  align-items: center;
  justify-content: center;
  flex: none;
  width: var(--ca-tap);
  height: var(--ca-tap);
  margin-right: -10px;
  padding: 0;
  border: none;
  border-radius: 10px;
  background: transparent;
  color: var(--ca-text);
  font-size: 22px;
  cursor: pointer;
}

.header__burger:hover {
  color: var(--ca-text-bright);
}

.header__link {
  position: relative;
  font-size: 15px;
  font-weight: 600;
  color: var(--ca-text-muted);
  text-decoration: none;
  white-space: nowrap;
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
  white-space: nowrap;
}

.header__greeting {
  font-size: 14px;
  font-weight: 600;
  color: var(--ca-text);
  white-space: nowrap;
}

.menu {
  display: flex;
  flex-direction: column;
}

.menu__greeting {
  margin-bottom: 6px;
  padding: 0 4px 12px;
  border-bottom: 1px solid var(--ca-border);
  font-size: 15px;
  font-weight: 600;
  color: var(--ca-text-muted);
}

.menu__link {
  display: flex;
  align-items: center;
  min-height: var(--ca-tap);
  padding: 10px 4px;
  border-radius: 10px;
  color: var(--ca-text);
  text-decoration: none;
  font-family: var(--ca-font-display);
  font-size: 17px;
  font-weight: 600;
}

.menu__link--active {
  color: var(--ca-orange-ink);
}

.menu__actions {
  display: flex;
  flex-direction: column;
  gap: 10px;
  margin-top: 18px;
  padding-top: 18px;
  border-top: 1px solid var(--ca-border);
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

@media (max-width: 1024px) {
  .header__inner {
    gap: 12px;
  }

  .header__burger {
    display: inline-flex;
  }

  .header__link,
  .header__greeting,
  .header__login,
  .header__cta {
    display: none;
  }
}
</style>
