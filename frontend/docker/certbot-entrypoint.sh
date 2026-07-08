#!/bin/sh
# Obtains (and then renews) an ECDSA Let's Encrypt certificate via the webroot
# challenge that nginx serves. Retries quickly until the first issuance succeeds.
set -eu
: "${SERVER_NAME:?SERVER_NAME is required}"
: "${LETSENCRYPT_EMAIL:?LETSENCRYPT_EMAIL is required}"

staging=""
[ "${LETSENCRYPT_STAGING:-0}" = "1" ] && staging="--staging"

# certbot writes the key root-only (0600); grant read to the non-root nginx
# (gid 101) over the shared volume without exposing it world-wide.
fix_perms() {
    [ -d /etc/letsencrypt/live ] || return 0
    find /etc/letsencrypt/live /etc/letsencrypt/archive -type d -exec chmod 0750 {} \; 2>/dev/null || true
    find /etc/letsencrypt/live /etc/letsencrypt/archive -type f -exec chmod 0640 {} \; 2>/dev/null || true
    chgrp -R 101 /etc/letsencrypt/live /etc/letsencrypt/archive 2>/dev/null || true
}

trap 'exit 0' TERM INT

while true; do
    if [ ! -d "/etc/letsencrypt/live/${SERVER_NAME}" ]; then
        if certbot certonly --webroot -w /var/www/certbot \
            -d "${SERVER_NAME}" --email "${LETSENCRYPT_EMAIL}" \
            --agree-tos --no-eff-email --non-interactive \
            --key-type ecdsa --elliptic-curve secp384r1 ${staging}; then
            fix_perms
            delay=43200
        else
            echo "[certbot] issuance failed; retrying in 120s"
            delay=120
        fi
    else
        certbot renew --webroot -w /var/www/certbot --non-interactive || true
        fix_perms
        delay=43200
    fi
    sleep "${delay}" &
    wait $!
done
