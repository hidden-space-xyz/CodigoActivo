<script setup lang="ts">
import ConfirmDialog from 'primevue/confirmdialog'

import { useAuth } from '@/features/auth'
import { AppToast, ThemeToggle } from '@/shared/ui'
import { ADMIN_NAV } from '@/shared/config'

const { displayName, logout } = useAuth()
</script>

<template>
  <div class="admin">
    <aside class="admin__sidebar">
      <RouterLink :to="{ name: 'home' }" class="admin__brand">Código Activo</RouterLink>
      <nav class="admin__nav">
        <RouterLink
          v-for="item in ADMIN_NAV"
          :key="item.routeName"
          :to="{ name: item.routeName }"
          class="admin__link"
          active-class="admin__link--active"
        >
          <i :class="item.icon" />
          <span>{{ item.label }}</span>
        </RouterLink>
      </nav>
    </aside>

    <div class="admin__body">
      <header class="admin__topbar">
        <RouterLink :to="{ name: 'home' }" class="admin__home">← Ir al sitio</RouterLink>
        <div class="admin__user">
          <ThemeToggle />
          <span class="admin__username">{{ displayName }}</span>
          <button type="button" class="admin__logout" title="Cerrar sesión" @click="logout()">
            Cerrar sesión
          </button>
        </div>
      </header>

      <main class="admin__main">
        <slot />
      </main>
    </div>

    <AppToast position="top-right" />
    <ConfirmDialog />
  </div>
</template>

<style scoped>
.admin {
  display: grid;
  grid-template-columns: 248px 1fr;
  min-height: 100vh;
  background: var(--ca-bg);
  color: var(--ca-text);
}

.admin__sidebar {
  border-right: 1px solid var(--ca-border);
  background: var(--ca-bg-deep);
  padding: 22px 16px;
  display: flex;
  flex-direction: column;
  gap: 22px;
  position: sticky;
  top: 0;
  height: 100vh;
}

.admin__brand {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 18px;
  letter-spacing: -0.01em;
  color: var(--ca-text-bright);
  text-decoration: none;
  padding: 0 8px;
}

.admin__nav {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.admin__link {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 12px;
  border-radius: 10px;
  color: var(--ca-text-muted);
  text-decoration: none;
  font-size: 14.5px;
  font-weight: 500;
  transition:
    background 0.15s ease,
    color 0.15s ease;
}

.admin__link:hover {
  background: var(--ca-surface);
  color: var(--ca-text);
}

.admin__link--active {
  background: var(--ca-orange-soft);
  color: var(--ca-orange-ink);
  font-weight: 600;
  box-shadow: inset 2px 0 0 var(--ca-orange);
}

.admin__body {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.admin__topbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 28px;
  border-bottom: 1px solid var(--ca-border);
  background: var(--ca-glass-bg);
  backdrop-filter: blur(14px);
  -webkit-backdrop-filter: blur(14px);
  position: sticky;
  top: 0;
  z-index: 20;
}

.admin__home {
  color: var(--ca-text-muted);
  text-decoration: none;
  font-size: 14px;
}

.admin__home:hover {
  color: var(--ca-text);
}

.admin__user {
  display: flex;
  align-items: center;
  gap: 16px;
}

.admin__username {
  font-weight: 600;
  font-size: 14px;
}

.admin__logout {
  background: transparent;
  border: 1px solid var(--ca-border-strong);
  color: var(--ca-text-muted);
  padding: 7px 14px;
  border-radius: 9px;
  cursor: pointer;
  font-size: 13.5px;
}

.admin__logout:hover {
  color: var(--ca-text);
  border-color: var(--ca-orange);
}

.admin__main {
  padding: 28px;
  flex: 1;
  min-width: 0;
}

@media (max-width: 860px) {
  .admin {
    grid-template-columns: 1fr;
  }
  .admin__sidebar {
    position: static;
    height: auto;
  }
}
</style>
