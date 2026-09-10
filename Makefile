.DEFAULT_GOAL := help

DOTNET ?= dotnet

up: ## Levanta el stack completo de desarrollo (postgres, backend, frontend)
	docker compose up -d --build

down: ## Detiene el stack de desarrollo
	docker compose down

logs: ## Muestra los logs en vivo de todos los servicios
	docker compose logs -f

test: ## Ejecuta los tests xUnit del backend contra postgres-test
	docker compose -f docker-compose.test.yml up -d
	$(DOTNET) test backend/MentalHealthTracker.slnx
	docker compose -f docker-compose.test.yml down

test-e2e: ## Ejecuta las pruebas end-to-end con Playwright (requiere stack arriba)
	cd e2e && npx playwright test

migrate: ## Aplica las migraciones de EF Core a la base de datos
	$(DOTNET) ef database update --project backend/MentalHealthTracker.Infrastructure --startup-project backend/MentalHealthTracker.Api

seed: ## Puebla la base de datos con datos de ejemplo
	$(DOTNET) run --project backend/MentalHealthTracker.Api --no-launch-profile -- --seed

help: ## Muestra los atajos disponibles
	@grep -E '^[a-zA-Z0-9_-]+:.*?## ' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-12s\033[0m %s\n", $$1, $$2}'