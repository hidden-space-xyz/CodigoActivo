# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/playwright:v1.64.1-noble
WORKDIR /repo/tests/e2e
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY --from=contract . /repo/frontend/src/shared/api/generated/models
COPY . .
CMD ["npx", "playwright", "test"]
