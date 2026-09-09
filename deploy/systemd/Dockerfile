# syntax=docker/dockerfile:1.7

# NativeAOT requires a native compiler/linker in addition to the .NET SDK.
# Build for the requested target platform so no .NET or C/C++ toolchain is
# required on the firewall host itself.
FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-bookworm-slim AS build
ARG TARGETARCH
WORKDIR /source

RUN apt-get update \
    && apt-get install -y --no-install-recommends clang zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

COPY src ./src
RUN case "$TARGETARCH" in \
        amd64) RID=linux-x64 ;; \
        arm64) RID=linux-arm64 ;; \
        *) echo "unsupported target architecture: $TARGETARCH" >&2; exit 1 ;; \
    esac \
    && dotnet restore src/Ufw.Systemd/Ufw.Systemd.csproj --runtime "$RID" \
    && dotnet publish src/Ufw.Systemd/Ufw.Systemd.csproj \
        --configuration Release \
        --runtime "$RID" \
        --self-contained true \
        --no-restore \
        --output /out \
    && test -x /out/Ufw.Systemd

# Deliberately empty export stage: `docker buildx build --output type=local`
# copies only the deployable NativeAOT executable back to the host.
FROM scratch AS artifact
COPY --from=build /out/Ufw.Systemd /Ufw.Systemd
