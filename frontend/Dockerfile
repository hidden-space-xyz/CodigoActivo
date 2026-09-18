# syntax=docker/dockerfile:1

FROM node:26-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npm run build
RUN find dist -type f -size +1023c \
        \( -name '*.html' -o -name '*.js' -o -name '*.mjs' -o -name '*.css' -o -name '*.svg' \
           -o -name '*.json' -o -name '*.xml' -o -name '*.txt' -o -name '*.map' -o -name '*.webmanifest' \) \
        -exec gzip -9 -k {} +

FROM nginxinc/nginx-unprivileged:1-alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY docker/nginx.conf /etc/nginx/nginx.conf
COPY docker/default.conf /etc/nginx/conf.d/default.conf
COPY docker/security-headers.conf docker/proxy-api.conf /etc/nginx/snippets/
USER 101
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -q -O /dev/null http://127.0.0.1:8080/healthz || exit 1
