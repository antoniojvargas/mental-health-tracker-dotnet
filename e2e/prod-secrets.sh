#!/bin/sh
# Genera los cuatro secretos DESECHABLES de la prueba prod-smoke en e2e/prod-secrets/
# (ignorado por git). Cada ejecucion los regenera; no se reutilizan en ningun otro sitio.
set -eu

dir="$(cd "$(dirname "$0")" && pwd)/prod-secrets"
# Legibles por todos: en Linux el secreto se monta con los permisos del host y el backend
# corre con un usuario sin privilegios. Son valores desechables, no hace falta 0600.
umask 022
mkdir -p "$dir"

password="$(openssl rand -hex 16)"

printf '%s' "$password" > "$dir/postgres_password.txt"
printf 'Host=postgres;Port=5432;Database=mentalhealthtracker;Username=postgres;Password=%s' "$password" \
  > "$dir/database_url.txt"
printf '%s' "$(openssl rand -hex 32)" > "$dir/jwt_secret.txt"
printf '%s' "prod-smoke-disposable-client-secret" > "$dir/google_client_secret.txt"

# umask solo aplica a archivos nuevos: al regenerar, los existentes conservan su modo.
chmod 755 "$dir"
chmod 644 "$dir"/*.txt
