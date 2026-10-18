<template>
  <ul class="legal-entry-list">
    <slot />
  </ul>
</template>

<style scoped>
.legal-entry-list {
  list-style: none;
  margin: 20px 0 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 14px;
}
</style>
