# syntax=docker/dockerfile:1.7

# ===== Stage 1: build the SPA ============================================
# Multi-stage Dockerfile for the React backoffice SPA.
# Consult the multi-stage-dockerfile skill when authoring/optimizing this file.
FROM node:22-alpine AS build

WORKDIR /app

# Copy dependency manifests first to maximize layer caching.
COPY package.json package-lock.json* ./
RUN npm install --no-audit --no-fund

# Copy the rest of the source and build with Vite.
COPY . .

# Build-time env vars. These are inlined into the bundle by Vite, so they must
# reflect the URLs the BROWSER will use. When running the SPA in Docker, the
# browser is on the host and reaches Keycloak via http://localhost:8080 and
# the API via http://localhost:5157 (the host-mapped ports).
ARG VITE_OIDC_CLIENT_ID=backoffice-web
ARG VITE_OIDC_REALM=company
ARG VITE_OIDC_AUTHORITY=http://localhost:8080/realms/company
ARG VITE_OIDC_REDIRECT_URI=http://localhost:5173/
ARG VITE_API_BASE_URL=http://localhost:5157

ENV VITE_OIDC_CLIENT_ID=$VITE_OIDC_CLIENT_ID \
    VITE_OIDC_REALM=$VITE_OIDC_REALM \
    VITE_OIDC_AUTHORITY=$VITE_OIDC_AUTHORITY \
    VITE_OIDC_REDIRECT_URI=$VITE_OIDC_REDIRECT_URI \
    VITE_API_BASE_URL=$VITE_API_BASE_URL

RUN npm run build

# ===== Stage 2: serve with Nginx =========================================
FROM nginx:1.27-alpine AS final

# Copy the built static assets into the Nginx html directory.
COPY --from=build /app/dist /usr/share/nginx/html

# Nginx config: SPA history fallback (so /oidc redirect callbacks resolve to index.html)
# and a gzip-friendly setup.
RUN printf '%s\n' \
  'server {' \
  '    listen 80;' \
  '    server_name _;' \
  '    root /usr/share/nginx/html;' \
  '    index index.html;' \
  '    location / {' \
  '        try_files $uri $uri/ /index.html;' \
  '    }' \
  '}' > /etc/nginx/conf.d/default.conf

EXPOSE 80

# nginx unprivileged by default runs as root in the official image; keep it simple for dev.
CMD ["nginx", "-g", "daemon off;"]
