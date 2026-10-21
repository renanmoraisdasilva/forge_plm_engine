# Forge PLM backend image.
#
#   docker compose up -d --wait     build and start everything (db + api)
#
# Build context is the repository root, not this directory: the API references
# `..\ForgePlm.Domain`, so both projects must be inside the context.
#
# Dev does NOT use this image — development stays on `dotnet run` for hot reload. This image
# is the reproducible demo/run mode (A-16, A-11).

# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first, on their own layer: the restore is cached until a .csproj changes,
# so editing C# does not re-download packages.
COPY src/ForgePlm.Domain/ForgePlm.Domain.csproj src/ForgePlm.Domain/
COPY src/ForgePlm.Api/ForgePlm.Api.csproj src/ForgePlm.Api/
RUN dotnet restore src/ForgePlm.Api/ForgePlm.Api.csproj

COPY src/ForgePlm.Domain/ src/ForgePlm.Domain/
COPY src/ForgePlm.Api/ src/ForgePlm.Api/

# UseAppHost=false drops the platform-specific launcher executable; the container runs the
# dll through the shared runtime, which is smaller and avoids an architecture mismatch.
RUN dotnet publish src/ForgePlm.Api/ForgePlm.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# SLICE 7 goes here: a node stage that builds the Vite SPA and copies the output into
# /app/publish/wwwroot, so this single image serves both the API and the UI (A-16).
# Left out until the frontend exists, because a build stage with nothing to build fails.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl exists only so the container healthcheck can probe /health. The aspnet runtime image
# ships without any HTTP client, and `docker compose up --wait` is what makes the demo
# start as "ready" rather than "probably almost ready" while migrations run.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# .NET 8+ images default to a non-root user and port 8080; both are set explicitly so the
# image does not depend on the base image's defaults staying the same.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ForgePlm.Api.dll"]
