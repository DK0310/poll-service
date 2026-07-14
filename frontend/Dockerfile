# ── Stage 1: Build the SPA ───────────────────────────────────
FROM node:22-alpine AS build
WORKDIR /app

# In the containerized topology the SPA calls the Gateway through Nginx on the
# SAME origin, so these default to relative paths. Compose can override via build args.
ARG VITE_API_URL=/api
ARG VITE_HUB_URL=/hubs/poll
ARG VITE_GOOGLE_CLIENT_ID=
ENV VITE_API_URL=$VITE_API_URL
ENV VITE_HUB_URL=$VITE_HUB_URL
ENV VITE_GOOGLE_CLIENT_ID=$VITE_GOOGLE_CLIENT_ID

COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

# ── Stage 2: Serve with Nginx ────────────────────────────────
FROM nginx:alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/templates/default.conf.template
EXPOSE 80
