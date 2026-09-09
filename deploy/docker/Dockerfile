# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0-bookworm-slim AS build
WORKDIR /source

COPY src ./src
RUN dotnet restore src/Ufw.Web/Ufw.Web.csproj \
    && dotnet restore src/Ufw.Client/Ufw.Client.csproj
RUN dotnet publish src/Ufw.Web/Ufw.Web.csproj \
        --configuration Release \
        --no-restore \
        --output /out/web \
    && dotnet publish src/Ufw.Client/Ufw.Client.csproj \
        --configuration Release \
        --no-restore \
        --output /out/client \
    && mkdir -p /out/web/wwwroot \
    && cp -a /out/client/wwwroot/. /out/web/wwwroot/

FROM mcr.microsoft.com/dotnet/aspnet:10.0-bookworm-slim AS runtime
ARG APP_UID=1654
ARG APP_GID=1654

WORKDIR /app
RUN mkdir -p /var/lib/ufw-webui/state \
    && touch /var/lib/ufw-webui/state/.volume-seed \
    && chown -R "${APP_UID}:${APP_GID}" /var/lib/ufw-webui/state
COPY --from=build /out/web/ ./

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    HOME=/var/lib/ufw-webui/state

EXPOSE 8080
USER ${APP_UID}:${APP_GID}
ENTRYPOINT ["dotnet", "Ufw.Web.dll"]
