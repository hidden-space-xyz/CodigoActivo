# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0-resolute AS build
ARG TARGETARCH
WORKDIR /source
COPY Directory.Packages.props Directory.Build.props Directory.Build.Analyzers.props Directory.Build.targets ./
COPY src/CodigoActivo.Domain/CodigoActivo.Domain.csproj                 src/CodigoActivo.Domain/
COPY src/CodigoActivo.Application/CodigoActivo.Application.csproj       src/CodigoActivo.Application/
COPY src/CodigoActivo.Infrastructure/CodigoActivo.Infrastructure.csproj src/CodigoActivo.Infrastructure/
COPY src/CodigoActivo.Composition/CodigoActivo.Composition.csproj      src/CodigoActivo.Composition/
COPY src/CodigoActivo.API/CodigoActivo.API.csproj                      src/CodigoActivo.API/
RUN dotnet restore src/CodigoActivo.API/CodigoActivo.API.csproj -a $TARGETARCH
COPY . .

FROM mcr.microsoft.com/dotnet/aspnet:10.0-resolute-chiseled AS base
COPY --from=build /usr/share/zoneinfo /usr/share/zoneinfo
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true \
    ASPNETCORE_HTTP_PORTS=8080 \
    HOME=/home/app \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1
WORKDIR /app
EXPOSE 8080

FROM build AS publish
ARG TARGETARCH
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish src/CodigoActivo.API/CodigoActivo.API.csproj \
    -c $BUILD_CONFIGURATION -a $TARGETARCH -o /app/publish --no-restore -p:UseAppHost=false
RUN dotnet publish docker/healthcheck.cs -c $BUILD_CONFIGURATION -o /app/healthcheck -p:UseAppHost=false \
    && mkdir -p /app/writable

FROM base AS final
ENV ASPNETCORE_ENVIRONMENT=Production
WORKDIR /app
COPY --from=publish --chown=1654:1654 /app/publish ./
COPY --from=publish /app/healthcheck /healthcheck
COPY --from=publish --chown=1654:1654 /app/writable /app/files
COPY --from=publish --chown=1654:1654 /app/writable /app/state
COPY --from=publish --chown=1654:1654 /app/writable /home/app/.aspnet/DataProtection-Keys
USER 1654
HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
    CMD ["dotnet", "/healthcheck/healthcheck.dll", "http://127.0.0.1:8080/api/auth/csrf", "X-Forwarded-Proto: https"]
ENTRYPOINT ["dotnet", "CodigoActivo.API.dll"]
