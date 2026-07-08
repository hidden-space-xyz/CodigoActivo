#!/bin/sh
# Enable the HTTPS server once certbot has issued the certificate, and reload
# nginx periodically so renewed certificates are picked up.
set -eu
: "${SERVER_NAME:?SERVER_NAME is required}"

LIVE="/etc/letsencrypt/live/${SERVER_NAME}"
CONF="/etc/nginx/conf.d/https.conf"
TMPL="/etc/nginx/https.conf.template"

enable_tls() {
    [ -s "${LIVE}/fullchain.pem" ] && [ -s "${LIVE}/privkey.pem" ] || return 1
    [ -f "$CONF" ] && return 1
    sed "s|__SERVER_NAME__|${SERVER_NAME}|g" "$TMPL" > "$CONF"
}

# Enable synchronously if the certificate already exists (nginx boots with TLS).
enable_tls || true

# Reparented to nginx after exec; waits for the first cert, then reloads on renewals.
(
    while [ ! -s "${LIVE}/fullchain.pem" ]; do sleep 5; done
    enable_tls && nginx -s reload 2>/dev/null || true
    while true; do sleep 43200; nginx -s reload 2>/dev/null || true; done
) &
