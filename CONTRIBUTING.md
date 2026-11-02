# Contribuir

Guía corta para cambiar este repositorio sin romperlo: cómo se nombran las ramas y los commits, qué
hay que ejecutar antes de subir y las dos reglas que gobiernan todo cambio. Cómo arrancar el
proyecto, sus variables y sus comandos está en el [`README.md`](README.md); el porqué de las
decisiones, en [`docs/`](docs/README.md).

## Requisitos

.NET SDK 9 (`global.json` admite uno más nuevo), Node 22 o superior y Docker (los tests de integración
usan un PostgreSQL real y los de humo, la pila de Compose). Las herramientas de .NET (`dotnet-ef`,
`reportgenerator`) son locales: `dotnet tool restore`, o los propios targets de `make`.

## Flujo de trabajo

`main` está protegida: solo se cambia por pull request y con los checks del CI en verde, también
para los administradores. No se hace push directo.

1. **Una rama por tema**, creada desde `main` actualizada.
2. **Commits pequeños y autocontenidos** en esa rama, cada uno con la verificación en verde.
3. **Una PR** al terminar el tema, con el CI en verde.
4. **Merge con merge commit** (`gh pr merge --merge`); no se hace squash ni rebase de la historia
   publicada. GitHub no deja aprobar la PR propia: la protección exige los checks, no una aprobación.

### Ramas

`<tipo>/<descripcion-en-kebab-case>`, con el mismo vocabulario de tipos que los commits:

| Prefijo | Para | Ejemplo |
| --- | --- | --- |
| `feat/` | Funcionalidad o capacidad nueva | `feat/horizontal-scaling-readiness` |
| `fix/` | Corregir un defecto | `fix/forwarded-headers-and-proxy-trust` |
| `refactor/` | Cambiar la estructura sin cambiar el comportamiento | `refactor/daily-log-form-module` |
| `test/` | Solo tests | `test/integration-suite-consolidation` |
| `docs/` | Solo documentación | `docs/architecture-refresh` |
| `ci/` | Workflows, Dependabot, Makefile de CI | `ci/pipeline-optimization` |
| `build/`, `chore/`, `perf/` | Imágenes y dependencias, mantenimiento, rendimiento | `chore/accessibility-and-polish` |

El nombre dice el tema de toda la rama, no un commit. Las ramas de Dependabot (`dependabot/…`) las
crea la herramienta.

### Commits

[Conventional Commits](https://www.conventionalcommits.org/): `tipo(ámbito): resumen`.

- **Tipo:** `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `build`, `ci`, `perf` o `style`.
- **Ámbito** (opcional pero habitual): la parte que cambia, como `backend`, `frontend`, `api`,
  `daily-log`, `realtime`, `infrastructure`, `e2e` o `readme`.
- **Resumen:** en inglés, en imperativo, sin punto final y de unos 70 caracteres como máximo
  («fix(api): resolve the real client IP behind a trusted proxy»).
- **Cuerpo** (si hace falta): explica el **porqué** y lo que no se ve en el diff: el defecto que se
  corrige, la decisión, qué se verificó. Menciona los cambios de contrato.
- Si el commit se hizo con ayuda de un asistente, se declara con un trailer `Co-Authored-By:`.
- **Un commit, un cambio.** Mezclar un refactor con un cambio de comportamiento es justo lo que impide
  revisarlos. No se sube un commit que deje el build o los tests en rojo.

Nunca se versionan secretos: `.env`, `secrets/*` y similares están en `.gitignore`.

### Pull requests

Descripción en español con un **Resumen** de lo que cambia, la **Verificación** que se hizo (qué
comandos y qué resultado) y las **Notas** que un revisor necesita: cambios de contrato, dependencias
nuevas, cambios de infraestructura o de la protección de `main`.

## Verificación

Antes de subir, lo que corresponda al cambio. Todo se ejecuta desde la raíz.

| Qué cambias | Ejecuta | Qué comprueba |
| --- | --- | --- |
| Backend | `make test` | Unitarias e integración (levanta y baja `postgres-test`) y el umbral de cobertura de líneas |
| Frontend | `make test-frontend` | Tipos generados al día, build (`vue-tsc` + Vite) y Vitest con umbral de cobertura |
| Cualquiera | `make lint` | `dotnet format --verify-no-changes`, ESLint y Prettier (`make format` los aplica) |
| Dependencias | `make audit` | Vulnerabilidades de NuGet (con transitivas) y de npm (≥ alta) |
| Imágenes o Dockerfile | `make build-images` | Imágenes de producción; el backend no corre como root y reintenta la conexión a la BD |
| UI o flujo HTTP completo | `make smoke-prod`, y la [suite E2E](README.md#pruebas-e2e-playwright) | Recorrido real navegador → nginx → API → PostgreSQL |
| Todo lo anterior | `make ci` | `lint`, `test`, `test-frontend`, `audit`, `build-images` y `smoke-prod` |

`make ci` no incluye `E2E` ni `Dev stack smoke`, que necesitan un `.env` en la raíz y la pila de
desarrollo; el CI de GitHub sí los ejecuta. Los comandos sueltos están en el
[README](README.md#comandos-útiles).

Si el cambio toca el **contrato HTTP** (rutas, cuerpos, enums), regenera el snapshot y los tipos en el
mismo commit: `UPDATE_OPENAPI_SNAPSHOT=1 dotnet test --filter OpenApiSnapshotTests` y
`cd frontend && npm run generate:types`. Si añades o subes un paquete, `packages.lock.json` (NuGet) o
`package-lock.json` (npm) van en el commit; el CI restaura con `--locked-mode`. **Los paquetes o
herramientas nuevos se consultan antes de añadirlos**, igual que cualquier cambio de configuración o
de infraestructura.

## Reglas

### Un refactor no cambia comportamiento: primero un test de caracterización

Refactorizar es cambiar la estructura **sin** cambiar lo que el sistema hace. Para poder afirmarlo hace
falta algo que lo compruebe:

1. **Antes de tocar el código**, comprueba que el comportamiento actual está fijado por tests. Si no lo
   está, escribe primero un **test de caracterización**: uno que describe lo que el código hace *hoy*,
   aunque no sea lo ideal, y que pasa contra el código sin modificar.
2. **Refactoriza** hasta que la estructura sea la que quieres, con esos tests en verde en todo momento.
3. **No toques las expectativas** de esos tests en el mismo cambio. Si un test tiene que cambiar para que
   pase, no era un refactor: es un cambio de comportamiento y va aparte, con su propio test y su propia
   explicación.

En este repositorio los tests de caracterización ya existentes son el JSON exacto de las respuestas
(`OpenApiSnapshotTests`, los contratos de `Contracts/`), la ida y vuelta del formulario
(`daily-log-mapper.test.ts`), los datos de demo (`seed-demo-logs.json`) y las pruebas de interacción del
modal. Una buena señal de que el refactor es limpio es que el diff de los tests sea cero.

### Una corrección empieza con un test que reproduce el defecto

Una corrección sin un test que falle antes es una suposición. El orden es siempre el mismo:

1. **Reproduce el defecto con un test** que falle por la razón correcta (no por un error de montaje ni de
   datos). Comprueba que falla *antes* de arreglar nada.
2. **Corrige** lo mínimo para que ese test pase, sin mezclar otros cambios.
3. **Comprueba que el test protege**: si revierto la corrección, vuelve a fallar. Es el mismo test que
   evita que el defecto reaparezca.

Si el defecto se descubre pero no se corrige todavía, se deja el test marcado como fallo esperado
(`it.fails` en Vitest, con un comentario que cite el hallazgo) para que el CI siga en verde y el defecto
quede registrado; quien lo corrija convierte ese caso en un `it` normal. Cuando el defecto es de un
entorno (configuración, imagen, CI) y no de código, el «test» es la comprobación que lo detecta, como el
job `Dev stack smoke`, que arranca la pila tal como la documenta el README.
