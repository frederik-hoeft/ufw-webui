#!/usr/bin/env bash
set -euo pipefail

readonly SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly REPO_ROOT="$(cd -- "$SCRIPT_DIR/../.." && pwd)"

usage() {
    cat <<'USAGE'
Usage: deploy/systemd/publish.sh [linux-x64|linux-arm64] [OUTPUT_DIRECTORY]

Builds Ufw.Systemd as a NativeAOT Linux executable inside Docker Buildx and
exports the executable to the host. If no runtime is supplied, the current
host architecture is used.
USAGE
}

fail() {
    printf 'error: %s\n' "$*" >&2
    exit 1
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
    usage
    exit 0
fi

if (($# > 2)); then
    usage >&2
    exit 2
fi

if (($# >= 1)); then
    RID="$1"
else
    case "$(uname -m)" in
        x86_64|amd64) RID="linux-x64" ;;
        aarch64|arm64) RID="linux-arm64" ;;
        *) fail "cannot infer a supported runtime from host architecture '$(uname -m)'" ;;
    esac
fi

case "$RID" in
    linux-x64) PLATFORM="linux/amd64" ;;
    linux-arm64) PLATFORM="linux/arm64" ;;
    *) fail "unsupported runtime '$RID' (supported: linux-x64, linux-arm64)" ;;
esac

OUTPUT="${2:-$REPO_ROOT/artifacts/publish/ufw-systemd/$RID}"

command -v docker >/dev/null 2>&1 || fail "docker was not found"
docker buildx version >/dev/null 2>&1 || fail "docker buildx is required"

rm -rf -- "$OUTPUT"
mkdir -p -- "$OUTPUT"

docker buildx build \
    --platform "$PLATFORM" \
    --file "$SCRIPT_DIR/Dockerfile" \
    --target artifact \
    --output "type=local,dest=$OUTPUT" \
    "$REPO_ROOT"

BINARY="$OUTPUT/Ufw.Systemd"
[[ -f "$BINARY" ]] || fail "Docker build did not export $BINARY"
chmod 0755 -- "$BINARY"
printf '%s\n' "$BINARY"
