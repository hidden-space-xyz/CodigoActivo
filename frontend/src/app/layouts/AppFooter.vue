<script setup lang="ts">
import { CONTACT } from '@/shared/config'
import { BrandLogo } from '@/shared/ui'

const currentYear = new Date().getFullYear()
</script>

<template>
  <footer class="footer">
    <div class="footer__inner">
      <div class="footer__brandcol">
        <BrandLogo size="sm" />
        <p class="footer__tagline">
          Programación para tod@s. Asociación sin ánimo de lucro · León · desde 2018.
        </p>
      </div>

      <div class="footer__col">
        <div class="footer__heading">Contacto</div>
        <div class="footer__links">
          <a :href="`mailto:${CONTACT.email}`" class="footer__link">{{ CONTACT.email }}</a>
          <span class="footer__link footer__link--static">{{ CONTACT.phone }}</span>
          <div class="footer__social">
            <a
              :href="CONTACT.social.instagram"
              target="_blank"
              rel="noopener"
              class="footer__social-link"
            >
              <i class="pi pi-instagram" aria-hidden="true"></i>
              Instagram
            </a>
            <a
              :href="CONTACT.social.facebook"
              target="_blank"
              rel="noopener"
              class="footer__social-link"
            >
              <i class="pi pi-facebook" aria-hidden="true"></i>
              Facebook
            </a>
            <a
              :href="CONTACT.social.linkedin"
              target="_blank"
              rel="noopener"
              class="footer__social-link"
            >
              <i class="pi pi-linkedin" aria-hidden="true"></i>
              LinkedIn
            </a>
          </div>
        </div>
      </div>
    </div>

    <div class="footer__legal">© {{ currentYear }} Código Activo — programación para tod@s</div>
  </footer>
</template>

<style scoped>
.footer {
  border-top: 1px solid var(--ca-border);
  background: var(--ca-bg-deep);
  padding: 48px 24px 30px;
}

.footer__inner {
  max-width: var(--ca-container);
  margin: 0 auto;
  display: grid;
  grid-template-columns: 1.4fr 1fr;
  gap: 36px;
}

.footer__tagline {
  margin-top: 14px;
  font-size: 14.5px;
  line-height: 1.6;
  color: var(--ca-text-faint-2);
  max-width: 320px;
}

.footer__heading {
  font-family: var(--ca-font-mono);
  font-size: 12px;
  color: var(--ca-text-ghost);
  letter-spacing: 0.1em;
  text-transform: uppercase;
  margin-bottom: 14px;
}

.footer__links {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.footer__link {
  font-size: 14.5px;
  color: var(--ca-text-muted);
  text-decoration: none;
  transition: color 0.15s ease;
}

.footer__link:hover {
  color: var(--ca-text-bright);
}

.footer__link--static {
  cursor: default;
}

.footer__social {
  display: flex;
  gap: 14px;
  margin-top: 6px;
  font-family: var(--ca-font-mono);
  font-size: 13px;
}

.footer__social-link {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: var(--ca-text-muted);
  text-decoration: none;
}

.footer__social-link .pi {
  font-size: 13px;
}

.footer__social-link:hover {
  color: var(--ca-orange-ink);
}

.footer__legal {
  max-width: var(--ca-container);
  margin: 34px auto 0;
  padding-top: 20px;
  border-top: 1px solid var(--ca-border);
  font-size: 13px;
  color: var(--ca-text-ghost);
  font-family: var(--ca-font-mono);
}

@media (max-width: 720px) {
  .footer__inner {
    grid-template-columns: 1fr;
    gap: 28px;
  }
}
</style>
