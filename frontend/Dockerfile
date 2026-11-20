# node:22-alpine
FROM node:26-alpine@sha256:6bd9ab43c0ec0190ca7b2d193906cb8b7edf6e7f4afa43ddeccd6452710bffb5
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
