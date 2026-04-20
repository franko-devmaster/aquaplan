FROM node:22-alpine AS build
WORKDIR /app
ENV NODE_OPTIONS=--max-old-space-size=2048
COPY src/AquaPlan.Web/ClientApp/package*.json ./
RUN npm ci
COPY src/AquaPlan.Web/ClientApp/ .
RUN npx ng build --configuration production

FROM nginx:alpine AS runtime
COPY --from=build --chown=nginx:nginx /app/dist/client-app/browser /usr/share/nginx/html
COPY src/AquaPlan.Web/nginx.conf /etc/nginx/conf.d/default.conf
RUN chmod -R 755 /usr/share/nginx/html
EXPOSE 80
