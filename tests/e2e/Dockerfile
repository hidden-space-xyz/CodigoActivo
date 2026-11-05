# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/playwright:v1.65.0-noble
WORKDIR /repo/tests/e2e
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY --from=contract . /repo/frontend/src/shared/api/generated/models
COPY --from=locales . /repo/frontend/src/shared/i18n/locales
COPY . .
CMD ["npx", "playwright", "test"]
