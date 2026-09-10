# Restore stage: dependencies restored in a dedicated layer to leverage Docker cache
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS restore
WORKDIR /source

COPY Directory.Build.props .
COPY MentalHealthTracker.slnx .

COPY MentalHealthTracker.Api/MentalHealthTracker.Api.csproj MentalHealthTracker.Api/
COPY MentalHealthTracker.Domain/MentalHealthTracker.Domain.csproj MentalHealthTracker.Domain/
COPY MentalHealthTracker.Infrastructure/MentalHealthTracker.Infrastructure.csproj MentalHealthTracker.Infrastructure/
COPY tests/MentalHealthTracker.UnitTests/MentalHealthTracker.UnitTests.csproj tests/MentalHealthTracker.UnitTests/
COPY tests/MentalHealthTracker.IntegrationTests/MentalHealthTracker.IntegrationTests.csproj tests/MentalHealthTracker.IntegrationTests/

RUN dotnet restore MentalHealthTracker.slnx

# Dev stage: hot-reload with dotnet watch
FROM mcr.microsoft.com/dotnet/sdk:9.0
WORKDIR /app

COPY --from=restore /source ./
COPY . .

ENV ASPNETCORE_URLS=http://+:3000
ENV ASPNETCORE_ENVIRONMENT=Development
ENV DOTNET_USE_POLLING_FILE_WATCHER=1
ENV DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1

EXPOSE 3000

ENTRYPOINT ["dotnet", "watch", "run", "--project", "MentalHealthTracker.Api/MentalHealthTracker.Api.csproj", "--no-launch-profile"]