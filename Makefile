.DEFAULT_GOAL := help
.PHONY: up down logs test test-run coverage test-frontend test-e2e lint format audit build-images smoke-prod ci migrate seed help

DOTNET ?= dotnet
COVERAGE_DIR := $(CURDIR)/coverage
COVERAGE_THRESHOLD := 77.8
# Misma pila que el job `prod-smoke` del CI: proyecto propio, aparte de `mht-prod`.
PROD_SMOKE_COMPOSE := docker compose -p mht-prod-e2e --env-file e2e/prod-smoke.env \
	-f docker-compose.prod.yml -f docker-compose.prod.e2e.yml

up: ## Levanta el stack completo de desarrollo (postgres, backend, frontend)
	docker compose up -d --build

down: ## Detiene el stack de desarrollo
	docker compose down

logs: ## Muestra los logs en vivo de todos los servicios
	docker compose logs -f

test: ## Ejecuta los tests xUnit del backend contra postgres-test y falla si la cobertura fusionada de líneas baja del umbral
	@# postgres-test se baja siempre, también si los tests o el umbral fallan; el código de salida es el de los tests.
	docker compose -f docker-compose.test.yml up -d && $(MAKE) --no-print-directory test-run; \
	status=$$?; \
	docker compose -f docker-compose.test.yml down; \
	exit $$status

# Cuerpo de `make test` (sin ##: no sale en `make help`); requiere postgres-test arriba.
test-run:
	DOTNET_ROLL_FORWARD=LatestMajor $(DOTNET) test backend/tests/MentalHealthTracker.UnitTests/MentalHealthTracker.UnitTests.csproj \
		-p:CoverletOutput=$(COVERAGE_DIR)/unit/
	DOTNET_ROLL_FORWARD=LatestMajor $(DOTNET) test backend/tests/MentalHealthTracker.IntegrationTests/MentalHealthTracker.IntegrationTests.csproj \
		-p:CoverletOutput=$(COVERAGE_DIR)/int/
	scripts/check-coverage.sh $(COVERAGE_THRESHOLD)

coverage: ## Fusiona la cobertura del último make test en un informe HTML y verifica el umbral
	$(DOTNET) tool restore
	$(DOTNET) tool run reportgenerator "-reports:$(COVERAGE_DIR)/**/coverage.cobertura.xml" \
		"-targetdir:$(COVERAGE_DIR)/html" "-reporttypes:HtmlInline_AzurePipelines_Dark;TextSummary"
	scripts/check-coverage.sh $(COVERAGE_THRESHOLD)

test-frontend: ## Frontend como en el CI: tipos generados al día, build y tests de Vitest con umbral de cobertura
	cd frontend && npm run generate:types && git diff --exit-code src/types/api.generated.d.ts
	cd frontend && npm run build
	cd frontend && npm run test:unit:coverage

test-e2e: ## Ejecuta las pruebas end-to-end con Playwright (requiere la pila E2E arriba, ver README)
	cd e2e && npx playwright test

lint: ## Lint como en el CI: dotnet format --verify-no-changes, ESLint y Prettier (solo comprueba)
	cd backend && DOTNET_ROLL_FORWARD=LatestMajor $(DOTNET) format MentalHealthTracker.slnx --verify-no-changes
	cd frontend && npm run lint
	cd frontend && npm run format

format: ## Corrige el formato: dotnet format, ESLint --fix y Prettier --write (modifica ficheros)
	cd backend && DOTNET_ROLL_FORWARD=LatestMajor $(DOTNET) format MentalHealthTracker.slnx
	cd frontend && npm run lint:fix
	cd frontend && npm run format:fix

audit: ## Auditoría de vulnerabilidades como en el CI: NuGet (con transitivas) y npm (>= high)
	cd backend && DOTNET_ROLL_FORWARD=LatestMajor $(DOTNET) list MentalHealthTracker.slnx package --vulnerable --include-transitive
	cd frontend && npm audit --audit-level=high

build-images: ## Construye las imágenes de producción y verifica usuario no root y reintento de BD, como el CI
	docker build -f backend/Dockerfile.prod -t mht-backend:ci backend
	docker build -f frontend/Dockerfile.prod -t mht-frontend:ci frontend
	@uid=$$(docker run --rm --entrypoint id mht-backend:ci -u); \
	echo "El proceso del backend corre con uid=$$uid"; \
	[ "$$uid" != "0" ] || { echo "El backend correría como root"; exit 1; }
	@# Sin postgres a mano el arranque debe quedarse reintentando, no morir por la dependencia.
	@trap 'docker rm -f mht-be-retry >/dev/null 2>&1 || true' EXIT; \
	docker rm -f mht-be-retry >/dev/null 2>&1 || true; \
	docker run -d --name mht-be-retry \
		-e Database__ConnectionString="Host=postgres;Port=5432;Database=mentalhealthtracker;Username=postgres;Password=postgres" \
		-e JWT_SECRET="ci-only-disposable-secret-0123456789" \
		-e GoogleOAuth__ClientId="ci-test.apps.googleusercontent.com" \
		-e GoogleOAuth__ClientSecret="ci-disposable-secret" \
		-e GoogleOAuth__RedirectUri="http://localhost:5173/api/auth/google/callback" \
		-e AppUrls__FrontendUrl="http://localhost:5173" \
		mht-backend:ci >/dev/null; \
	sleep 15; \
	logs=$$(docker logs mht-be-retry 2>&1); \
	echo "$$logs" | grep -q "Database not ready (attempt" || { echo "No se ve el reintento de conexión a la BD"; exit 1; }; \
	[ "$$(docker inspect -f '{{.State.Running}}' mht-be-retry)" = "true" ] || { echo "El contenedor murió en lugar de reintentar"; exit 1; }; \
	echo "Reintento de BD verificado"

smoke-prod: ## Recorrido crítico (Playwright) contra las imágenes de producción en :8080; baja la pila siempre
	./e2e/prod-secrets.sh
	@# La pila se baja (down -v) también si el recorrido falla; el código de salida es el del recorrido.
	$(PROD_SMOKE_COMPOSE) up -d --build --wait --wait-timeout 600 && (cd e2e && npm run test:prod-smoke); \
	status=$$?; \
	$(PROD_SMOKE_COMPOSE) down -v; \
	exit $$status

ci: lint test test-frontend audit build-images smoke-prod ## Todo lo del CI que no toca tu .env ni la pila de desarrollo (sin E2E ni Dev stack smoke)

migrate: ## Aplica las migraciones de EF Core a la base de datos
	$(DOTNET) tool restore
	$(DOTNET) tool run dotnet-ef database update --project backend/MentalHealthTracker.Infrastructure --startup-project backend/MentalHealthTracker.Api

seed: ## Puebla la base de datos con datos de ejemplo
	$(DOTNET) run --project backend/MentalHealthTracker.Api --no-launch-profile -- --seed

help: ## Muestra los atajos disponibles
	@grep -E '^[a-zA-Z0-9_-]+:.*?## ' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-14s\033[0m %s\n", $$1, $$2}'