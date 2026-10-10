# Secrets de producción

Este directorio guarda los secretos de la pila de producción
(`docker-compose.prod.yml`). Los archivos `secrets/*.txt` están excluidos del
repositorio por `.gitignore`, así que **nunca se versionan**. Ningún valor
sensible debe vivir en el compose ni en un `.env`.

Docker Compose monta cada archivo en el contenedor en `/run/secrets/<nombre>`.
El backend consume tres de ellos con la variante `*_FILE`
(`SecretFileConfigurationProvider`), que lee el contenido del archivo y lo
asigna a su clave de configuración. Por eso el archivo **no debe tener una
línea final**: los valores se leen con `Trim()`, pero es más limpio que el
secreto sea el valor puro.

## Archivos de secreto

| Archivo                          | Contenido                                    | Consumido por                            |
|----------------------------------|----------------------------------------------|------------------------------------------|
| `secrets/postgres_password.txt`  | Password del usuario `postgres`              | postgres (`POSTGRES_PASSWORD_FILE`)      |
| `secrets/database_url.txt`       | Connection string Npgsql completo al postgres | backend (`Database__ConnectionString_FILE`) |
| `secrets/jwt_secret.txt`         | Clave de firma de JWT (>= 16 caracteres)     | backend (`JWT_SECRET_FILE`)              |
| `secrets/google_client_secret.txt` | Client secret de Google OAuth              | backend (`GOOGLE_CLIENT_SECRET_FILE`)    |

### Requisitos cruzados

- El password embebido en `database_url.txt` debe coincidir con
  `postgres_password.txt` (es la misma credencial).
- Dentro de la red de Compose el host de la base es **`postgres`**, nunca
  `localhost`, y el puerto es `5432`.
- Solo se consumen si se configura la contraparte no sensible en
  `.env.production` (usuario/nombre de BD, `GOOGLE_CLIENT_ID`, URLs).

## Cómo generar cada secreto

Crea/rota los archivos, quita el salto de línea final y ajusta permisos a `0600`:

```sh
umask 077   # los archivos nuevos solo los lee el usuario
```

### `postgres_password.txt`

```sh
openssl rand -base64 32 | tr -d '\n' > secrets/postgres_password.txt
```

### `database_url.txt`

Connection string con el mismo password del archivo anterior
(puedes generarlo sin exponerlo en el historial de shell):

```sh
DB_PASS="$(cat secrets/postgres_password.txt)"
printf 'Host=postgres;Port=5432;Database=mentalhealthtracker;Username=postgres;Password=%s' \
  "$DB_PASS" | tr -d '\n' > secrets/database_url.txt
```

### `jwt_secret.txt`

```sh
openssl rand -base64 48 | tr -d '\n' > secrets/jwt_secret.txt
```

### `google_client_secret.txt`

No se genera localmente: se obtiene de Google Cloud Console (Credenciales OAuth
2.0 → Client secret del proyecto) y se vuelca sin salto de línea final:

```sh
printf '%s' 'el-client-secret-de-google' > secrets/google_client_secret.txt
```

## Verificación

Al levantar la pila, el backend debe quedar `healthy` y aplicar las migraciones:
si un secreto falta o está mal formado, el contenedor falla la validación de
configuración al arrancar (antes del healthcheck). Revisa `docker compose logs
backend` ante cualquier error.

---
> **Nunca** commits un `secrets/*.txt` con valores reales. Para rotar un
> secreto, sustituye el archivo y reinicia los servicios que lo consumen.