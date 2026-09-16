FROM node:25-alpine AS build
WORKDIR /app
ENV NODE_OPTIONS=--max-old-space-size=2048
# AQ-416 — allow callers to pin the displayed app version (used when .git
# is not available inside the image); falls back to package.json / "dev".
ARG APP_VERSION=""
ENV APP_VERSION=${APP_VERSION}

# Reproduce repo layout so scripts/generate-version.mjs can resolve its
# relative paths (it expects src/AquaPlan.Web/ClientApp/... under repo root).
COPY src/AquaPlan.Web/ClientApp/package*.json /app/src/AquaPlan.Web/ClientApp/
WORKDIR /app/src/AquaPlan.Web/ClientApp
RUN npm ci

COPY src/AquaPlan.Web/ClientApp/ /app/src/AquaPlan.Web/ClientApp/
COPY scripts/ /app/scripts/
COPY GitVersion.yml /app/GitVersion.yml

# Stamp version.ts before building.
WORKDIR /app
RUN node scripts/generate-version.mjs || echo "[docker] version generation failed — continuing with default"

WORKDIR /app/src/AquaPlan.Web/ClientApp
RUN npx ng build --configuration production

FROM nginx:alpine AS runtime
COPY --from=build --chown=nginx:nginx /app/src/AquaPlan.Web/ClientApp/dist/client-app/browser /usr/share/nginx/html
COPY src/AquaPlan.Web/nginx.conf /etc/nginx/conf.d/default.conf
# Sprint Sec F-006 (cross-cutting) — shared security headers included by nginx.conf.
COPY src/AquaPlan.Web/security-headers.conf /etc/nginx/security-headers.conf
RUN chmod -R 755 /usr/share/nginx/html
EXPOSE 80
