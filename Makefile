.DEFAULT_GOAL := help
.PHONY: up down logs test coverage test-e2e migrate seed help

DOTNET ?= dotnet
COVERAGE_DIR := $(CURDIR)/coverage
COVERAGE_THRESHOLD := 70

up: ## Levanta el stack completo de desarrollo (postgres, backend, frontend)
	docker compose up -d --build

down: ## Detiene el stack de desarrollo
	docker compose down

logs: ## Muestra los logs en vivo de todos los servicios
	docker compose logs -f

test: ## Ejecuta los tests xUnit del backend contra postgres-test y falla si la cobertura fusionada de líneas baja del umbral
	docker compose -f docker-compose.test.yml up -d
	DOTNET_ROLL_FORWARD=LatestMajor $(DOTNET) test backend/tests/MentalHealthTracker.UnitTests/MentalHealthTracker.UnitTests.csproj \
		-p:CoverletOutput=$(COVERAGE_DIR)/unit/
	DOTNET_ROLL_FORWARD=LatestMajor $(DOTNET) test backend/tests/MentalHealthTracker.IntegrationTests/MentalHealthTracker.IntegrationTests.csproj \
		-p:CoverletOutput=$(COVERAGE_DIR)/int/
	scripts/check-coverage.sh $(COVERAGE_THRESHOLD)
	docker compose -f docker-compose.test.yml down

coverage: ## Fusiona la cobertura del último make test en un informe HTML y verifica el umbral
	$(DOTNET) tool restore
	$(DOTNET) tool run reportgenerator "-reports:$(COVERAGE_DIR)/**/coverage.cobertura.xml" \
		"-targetdir:$(COVERAGE_DIR)/html" "-reporttypes:HtmlInline_AzurePipelines_Dark;TextSummary"
	scripts/check-coverage.sh $(COVERAGE_THRESHOLD)

test-e2e: ## Ejecuta las pruebas end-to-end con Playwright (requiere stack arriba)
	cd e2e && npx playwright test

migrate: ## Aplica las migraciones de EF Core a la base de datos
	$(DOTNET) ef database update --project backend/MentalHealthTracker.Infrastructure --startup-project backend/MentalHealthTracker.Api

seed: ## Puebla la base de datos con datos de ejemplo
	$(DOTNET) run --project backend/MentalHealthTracker.Api --no-launch-profile -- --seed

help: ## Muestra los atajos disponibles
	@grep -E '^[a-zA-Z0-9_-]+:.*?## ' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-12s\033[0m %s\n", $$1, $$2}'