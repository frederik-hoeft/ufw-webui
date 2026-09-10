# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0-resolute AS build
WORKDIR /source

COPY src ./src
RUN dotnet restore src/Ufw.Web/Ufw.Web.csproj
RUN dotnet publish src/Ufw.Web/Ufw.Web.csproj \
        --configuration Release \
        --no-restore \
        --output /out/web \
    && test ! -e /out/web/wwwroot/index.html

FROM mcr.microsoft.com/dotnet/aspnet:10.0-resolute AS runtime
ARG APP_UID=1654
ARG APP_GID=1654

WORKDIR /app
RUN mkdir -p /var/lib/ufw-webui/state /run/secrets /run/ufw-manager /run/ufw-web-api/socket \
    && touch /var/lib/ufw-webui/state/.volume-seed /run/ufw-web-api/socket/.volume-seed \
    && chown -R "${APP_UID}:${APP_GID}" /var/lib/ufw-webui/state /run/ufw-web-api/socket \
    && chmod 0770 /run/ufw-web-api/socket \
    && chmod 0555 /run/secrets /run/ufw-manager
COPY --from=build /out/web/ ./
COPY deploy/docker/asp-entrypoint.sh /usr/local/bin/ufw-webui-asp-entrypoint
RUN chmod +x /usr/local/bin/ufw-webui-asp-entrypoint

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    HOME=/var/lib/ufw-webui/state

USER ${APP_UID}:${APP_GID}
ENTRYPOINT ["/usr/local/bin/ufw-webui-asp-entrypoint"]
