<script setup lang="ts">
import { CONTACT } from '@/shared/config'
import { AppIcon, BrandLogo } from '@/shared/ui'
</script>

<template>
  <footer class="footer">
    <div class="footer__inner">
      <div class="footer__brandcol">
        <BrandLogo size="md" class="footer__brand" />
        <p class="footer__tagline">
          {{ $t('layout.footerTagline') }}
        </p>
      </div>

      <div class="footer__contact">
        <div class="footer__group">
          <div class="footer__heading">{{ $t('layout.footerContact') }}</div>
          <div class="footer__links">
            <a :href="`mailto:${CONTACT.email}`" class="footer__link footer__link--icon">
              <AppIcon name="envelope" />
              {{ CONTACT.email }}
            </a>
            <a :href="`tel:${CONTACT.phoneHref}`" class="footer__link footer__link--icon">
              <AppIcon name="phone" />
              {{ CONTACT.phone }}
            </a>
          </div>
        </div>

        <div class="footer__group">
          <div class="footer__heading">{{ $t('layout.footerFollow') }}</div>
          <div class="footer__channels">
            <a
              :href="CONTACT.social.instagram"
              target="_blank"
              rel="noopener"
              class="footer__channel-link"
            >
              <AppIcon name="instagram" />
              {{ $t('layout.socialInstagram') }}
            </a>
            <a
              :href="CONTACT.social.facebook"
              target="_blank"
              rel="noopener"
              class="footer__channel-link"
            >
              <AppIcon name="facebook" />
              {{ $t('layout.socialFacebook') }}
            </a>
            <a
              :href="CONTACT.social.linkedin"
              target="_blank"
              rel="noopener"
              class="footer__channel-link"
            >
              <AppIcon name="linkedin" />
              {{ $t('layout.socialLinkedIn') }}
            </a>
            <a
              :href="CONTACT.social.youtube"
              target="_blank"
              rel="noopener"
              class="footer__channel-link"
            >
              <AppIcon name="youtube" />
              {{ $t('layout.socialYouTube') }}
            </a>
          </div>
        </div>
      </div>
    </div>

    <nav class="footer__legal" :aria-label="$t('layout.footerLegal')">
      <RouterLink :to="{ name: 'cookie-policy' }" class="footer__legal-link">
        {{ $t('layout.footerCookies') }}
      </RouterLink>
    </nav>
  </footer>
</template>

<style scoped>
.footer {
  position: relative;
  overflow: hidden;
  background: var(--ca-bg-deep);
  padding: 36px var(--ca-gutter);
}

.footer::before {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 1px;
  background: linear-gradient(90deg, var(--ca-orange), var(--ca-lime), var(--ca-azure));
  opacity: 0.18;
  content: '';
}

.footer__inner {
  max-width: var(--ca-container);
  margin: 0 auto;
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 64px;
  align-items: stretch;
}

.footer__brandcol {
  min-width: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
}

.footer__brand :deep(.brand) {
  gap: 14px;
}

.footer__brand :deep(.brand__mark) {
  width: 40px;
  height: 40px;
}

.footer__brand :deep(.brand__word) {
  font-size: 25px;
}

.footer__tagline {
  max-width: 470px;
  margin: 16px auto 0;
  font-size: 14.5px;
  line-height: 1.65;
  color: var(--ca-text-faint-2);
}

.footer__contact {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 32px;
  max-width: 700px;
  align-items: center;
}

.footer__heading {
  font-family: var(--ca-font-mono);
  font-size: 12px;
  font-weight: 700;
  color: var(--ca-text-muted);
  letter-spacing: 0.1em;
  text-transform: uppercase;
  margin-bottom: 12px;
  text-align: center;
}

.footer__links,
.footer__channels {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  justify-content: center;
  font-family: var(--ca-font-mono);
  font-size: 12px;
}

.footer__link,
.footer__channel-link {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 34px;
  padding: 6px 10px;
  border-radius: 10px;
  color: var(--ca-text-muted);
  text-decoration: none;
  transition: color 0.15s ease;
}

.footer__link:hover,
.footer__channel-link:hover {
  color: var(--ca-orange-ink);
}

.footer__link--icon .app-icon,
.footer__channel-link .app-icon {
  font-size: 14px;
  color: var(--ca-orange-ink);
  transition: color 0.15s ease;
}

.footer__legal {
  max-width: var(--ca-container);
  margin: 28px auto 0;
  padding-top: 18px;
  border-top: 1px solid var(--ca-border);
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 8px 24px;
}

.footer__legal-link {
  display: inline-flex;
  align-items: center;
  min-height: 34px;
  padding: 6px 10px;
  border-radius: 10px;
  font-family: var(--ca-font-mono);
  font-size: 12px;
  color: var(--ca-text-muted);
  text-decoration: none;
  transition: color 0.15s ease;
}

.footer__legal-link:hover {
  color: var(--ca-orange-ink);
}

@media (max-width: 1024px) {
  .footer__inner {
    grid-template-columns: 1fr;
    gap: 24px;
    align-items: stretch;
  }
}

@media (max-width: 640px) {
  .footer {
    padding-block: 28px;
  }

  .footer__contact {
    gap: 16px;
  }

  .footer__links {
    align-items: stretch;
    flex-direction: column;
  }

  .footer__link {
    width: 100%;
  }
}

@media (pointer: coarse) {
  .footer__link {
    min-height: var(--ca-tap);
  }

  .footer__links,
  .footer__channels {
    gap: 6px;
  }

  .footer__channel-link,
  .footer__legal-link {
    min-height: var(--ca-tap);
  }
}
</style>
