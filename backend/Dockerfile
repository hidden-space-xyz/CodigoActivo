# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /source
COPY Directory.Packages.props ./
COPY src/Directory.Build.props src/
COPY src/CodigoActivo.Domain/CodigoActivo.Domain.csproj                 src/CodigoActivo.Domain/
COPY src/CodigoActivo.Application/CodigoActivo.Application.csproj       src/CodigoActivo.Application/
COPY src/CodigoActivo.Infrastructure/CodigoActivo.Infrastructure.csproj src/CodigoActivo.Infrastructure/
COPY src/CodigoActivo.Composition/CodigoActivo.Composition.csproj      src/CodigoActivo.Composition/
COPY src/CodigoActivo.API/CodigoActivo.API.csproj                      src/CodigoActivo.API/
RUN dotnet restore src/CodigoActivo.API/CodigoActivo.API.csproj
COPY . .
RUN dotnet publish src/CodigoActivo.API/CodigoActivo.API.csproj \
    -c Release -o /app/publish --no-restore -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
RUN apk add --no-cache tzdata
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true \
    TZ=Europe/Madrid \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    HOME=/home/app \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1
WORKDIR /app
COPY --from=build --chown=1654:1654 /app/publish ./
RUN mkdir -p /app/files /app/logs /home/app/.aspnet/DataProtection-Keys \
    && chown -R 1654:1654 /app/files /app/logs /home/app
USER 1654
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
    CMD wget -q -O /dev/null --header="X-Forwarded-Proto: https" http://127.0.0.1:8080/api/auth/csrf || exit 1
ENTRYPOINT ["dotnet", "CodigoActivo.API.dll"]
