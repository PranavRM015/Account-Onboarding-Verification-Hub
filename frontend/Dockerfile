# Build Angular with the same Node major version used by CI.
FROM node:24-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY . .
RUN npm run build -- --configuration production

# Only the compiled frontend is needed at runtime.
FROM nginx:stable-alpine AS runtime
# Origins (scheme://host) of the three API services; set per environment.
ENV IDENTITY_UPSTREAM=http://host.docker.internal:5290
ENV INTAKE_UPSTREAM=http://host.docker.internal:5168
ENV REVIEW_UPSTREAM=http://host.docker.internal:5292
ENV DNS_RESOLVER=127.0.0.11
ENV NGINX_ENVSUBST_FILTER=_UPSTREAM|DNS_RESOLVER

COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY nginx/proxy_params.conf /etc/nginx/proxy_params.conf
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
