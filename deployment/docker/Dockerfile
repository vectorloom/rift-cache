# Builds and runs the RiftCache core service (src/RiftCache) only. No cloud SDKs are
# referenced by that project, so this image stays cloud-agnostic — see ARCHITECTURE.md.
# Build context must be the repo root, e.g.:
#   docker build -f deployment/docker/Dockerfile -t riftcache/riftcache:latest .
#   podman build -f deployment/docker/Dockerfile -t riftcache/riftcache:latest .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, on just the files restore depends on, so source-only edits don't bust the
# Docker layer cache and force a full package re-download on every build.
COPY Directory.Build.props ./
COPY src/RiftCache/RiftCache.csproj src/RiftCache/
RUN dotnet restore src/RiftCache/RiftCache.csproj

COPY src/RiftCache/ src/RiftCache/
RUN dotnet publish src/RiftCache/RiftCache.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# Runs as the base image's built-in non-root user rather than root.
USER $APP_UID
WORKDIR /app

# Matches the README Quick Start's `-p 8080:8080`. RIFTCACHE_API_KEY is intentionally not
# set here — EnvironmentSecretProvider reads it from the container's environment at runtime,
# so it's supplied via `docker run -e` / `podman run -e` / compose, never baked into the image.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "RiftCache.dll"]
