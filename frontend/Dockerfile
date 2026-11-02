# Build Angular with the same Node major version used by CI.
FROM node:24-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY . .
RUN npm run build -- --configuration production

# Only the compiled frontend is needed at runtime.
FROM nginx:stable-alpine AS runtime
ENV API_UPSTREAM=host.docker.internal:5278
ENV NGINX_ENVSUBST_FILTER=API_UPSTREAM

COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
