# PermitTorch API. Build with the repo root as context:
#   docker build -f apps/api/Dockerfile -t permittorch-api .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ./
COPY apps/api/PermitTorch.Api.csproj apps/api/
RUN dotnet restore apps/api/PermitTorch.Api.csproj
COPY apps/api/ apps/api/
RUN dotnet publish apps/api/PermitTorch.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# tini as PID 1: forwards SIGTERM and lets a crashed runtime exit (a bare PID-1 dotnet
# ignores its own SIGABRT and spins after an unhandled startup exception, e.g. a
# missing DATABASE_URL). libgssapi-krb5-2 is probed by Npgsql at connect time.
RUN apt-get update \
    && apt-get install -y --no-install-recommends tini libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV DOTNET_EnableDiagnostics=0 \
    ASPNETCORE_ENVIRONMENT=Production
# The aspnet image ships an unprivileged "app" user; never run as root.
USER $APP_UID
EXPOSE 8080
# Railway injects PORT; default 8080 for local runs. Health endpoint: GET /api/health
# (Railway healthcheck path, set on the service; the runtime image has no curl for a Docker HEALTHCHECK).
# `docker run … permittorch-api seed` runs the idempotent seeder instead of serving.
ENTRYPOINT ["tini", "--", "sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet PermitTorch.Api.dll \"$@\"", "--"]
