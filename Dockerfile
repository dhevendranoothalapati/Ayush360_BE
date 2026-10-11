# syntax=docker/dockerfile:1
# =====================================================================================================
#  Ayush360 backend images. One Dockerfile, three targets:
#
#    docker build --target identity -t ayush360-identity .   # Identity API  (login, tokens, users)   port 8080
#    docker build --target hms      -t ayush360-hms .        # HMS API       (front desk, doctor...)  port 8080
#    docker build --target migrator -t ayush360-migrator .   # one-off job: applies EF Core migrations, then exits
#
#  Images run as the non-root "app" user and listen on 8080 (ASP.NET Core default for containers).
#  Build on linux/amd64 (Azure Container Apps runs amd64): on an ARM Mac add --platform linux/amd64.
# =====================================================================================================

ARG DOTNET_VERSION=10.0

# ---------- restore + compile everything once ----------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

# Project files first: the NuGet restore layer is reused until a .csproj changes.
COPY Ayush360.slnx ./
COPY src/Ayush360.Platform/Ayush360.Platform.csproj src/Ayush360.Platform/
COPY src/Ayush360.Identity.Api/Ayush360.Identity.Api.csproj src/Ayush360.Identity.Api/
COPY src/Ayush360.Hms.Api/Ayush360.Hms.Api.csproj src/Ayush360.Hms.Api/
RUN dotnet restore src/Ayush360.Identity.Api/Ayush360.Identity.Api.csproj \
 && dotnet restore src/Ayush360.Hms.Api/Ayush360.Hms.Api.csproj

COPY src/ src/

FROM build AS publish-identity
RUN dotnet publish src/Ayush360.Identity.Api/Ayush360.Identity.Api.csproj -c Release -o /out --no-restore /p:UseAppHost=false

FROM build AS publish-hms
RUN dotnet publish src/Ayush360.Hms.Api/Ayush360.Hms.Api.csproj -c Release -o /out --no-restore /p:UseAppHost=false

# ---------- EF Core migration bundles (self-contained executables, one per database schema owner) ----------
FROM build AS bundles
ARG DOTNET_EF_VERSION=10.0.12
RUN dotnet tool install --global dotnet-ef --version ${DOTNET_EF_VERSION}
ENV PATH="${PATH}:/root/.dotnet/tools"
# Building a bundle starts each API's Program.cs far enough to find its DbContext, and Program.cs insists on a
# connection string. This placeholder is never connected to; the real one is passed when the job runs.
ENV ConnectionStrings__Default="Host=placeholder;Database=ayush360;Username=build;Password=build"
# Identity owns schema "core"; HMS owns "hospital", "registration", "doctor". Identity runs first.
# Framework-dependent bundles (small, quick to build); they run on the ASP.NET runtime image below.
RUN dotnet ef migrations bundle --project src/Ayush360.Identity.Api --configuration Release \
      -o /bundles/identity-migrate --force \
 && dotnet ef migrations bundle --project src/Ayush360.Hms.Api --configuration Release \
      -o /bundles/hms-migrate --force

# ---------- runtime images ----------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime-base
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    # Trust X-Forwarded-For / -Proto from the frontend proxy and Azure ingress (real client IP for rate limits and logs).
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    TZ=Asia/Kolkata
# Development-only keys (token signing, Aadhaar) are written under ~/.local/share/Ayush360. In Azure the keys come
# from Key Vault instead; locally docker-compose mounts a volume here so they survive restarts.
RUN mkdir -p /home/app/.local/share/Ayush360 && chown -R app:app /home/app/.local
EXPOSE 8080

FROM runtime-base AS identity
COPY --from=publish-identity /out .
USER app
ENTRYPOINT ["dotnet", "Ayush360.Identity.Api.dll"]

FROM runtime-base AS hms
COPY --from=publish-hms /out .
USER app
ENTRYPOINT ["dotnet", "Ayush360.Hms.Api.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS migrator
WORKDIR /migrations
COPY --from=bundles /bundles/ ./
COPY deploy/migrate.sh ./migrate.sh
# Strip Windows line endings in case Git checked the script out with CRLF.
RUN sed -i 's/\r$//' ./migrate.sh && chmod +x ./migrate.sh ./identity-migrate ./hms-migrate
USER app
ENTRYPOINT ["./migrate.sh"]
