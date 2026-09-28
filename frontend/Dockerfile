FROM node:22-alpine
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# El dev server lee la config al arrancar, así que el target debe existir en
# runtime, no solo en build. Por defecto se apunta al nombre de servicio, que
# es lo único que resuelve dentro de la red de Compose; sobre el host se usa
# localhost:3000 (ver el comentario en vite.config.ts).
ARG VITE_PROXY_TARGET=http://backend:3000
ENV VITE_PROXY_TARGET=${VITE_PROXY_TARGET}

EXPOSE 5173

CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0"]
