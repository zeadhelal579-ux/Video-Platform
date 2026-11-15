# File: Dockerfile
# Purpose: Builds the React SPA and serves it with nginx. The same nginx also
# reverse-proxies /api to the Api container (see nginx.conf), so the browser
# talks to one origin only — no CORS, and the refresh cookie is first-party.
# Build context: the frontend/ folder (the root docker-compose.yml passes it).

# ---------- build stage ----------
FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json* ./
# Use the lockfile when there is one (reproducible), otherwise resolve fresh.
RUN if [ -f package-lock.json ]; then npm ci --no-audit --no-fund; else npm install --no-audit --no-fund; fi

COPY . .

# Empty = same-origin requests ("/api/..."), which is what the nginx proxy expects.
ARG VITE_API_BASE_URL=""
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

# "vite build" only (no "tsc -b"): type errors are caught by "npm run typecheck"
# in development/CI and never change the runtime behavior of the bundle.
RUN npx vite build

# ---------- runtime stage ----------
FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
