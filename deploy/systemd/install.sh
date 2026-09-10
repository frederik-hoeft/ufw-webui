#!/usr/bin/env bash
set -euo pipefail

readonly SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly INSTALL_ROOT="/usr/local/lib/ufw-webui"
readonly BINARY_TARGET="$INSTALL_ROOT/ufw-systemd"
readonly CONFIG_DIR="/etc/ufw-manager"
readonly CONFIG_TARGET="$CONFIG_DIR/settings.json"
readonly AUTHORIZED_KEYS_TARGET="$CONFIG_DIR/authorized_keys"
readonly IPC_ROOT="/var/lib/ufw-webui"
readonly IPC_DIR="$IPC_ROOT/ipc"
readonly IPC_SOCKET="$IPC_DIR/ufw-systemd.sock"
readonly LEGACY_IPC_SOCKET="/run/ufw-manager/ufw-systemd.sock"
readonly UNIT_TARGET="/etc/systemd/system/ufw-systemd.service"
readonly DOC_DIR="/usr/local/share/doc/ufw-webui"

BINARY_SOURCE=""
SETTINGS_SOURCE=""
AUTHORIZED_KEYS_SOURCE=""
IPC_GROUP="ufw-webui-ipc"
START_SERVICE=true

usage() {
    cat <<'USAGE'
Usage: sudo deploy/systemd/install.sh --binary PATH [options]

Installs or updates the privileged UFW WebUI daemon.

Required:
  --binary PATH             Published Linux Ufw.Systemd executable.

Options:
  --ipc-group GROUP         Host group allowed to connect to the daemon socket.
                            Default: ufw-webui-ipc. If absent, a system group is
                            created. For rootless Docker, pass the rootless Docker
                            user's existing primary group instead.
  --settings PATH           Install/replace /etc/ufw-manager/settings.json from PATH.
                            Without this option, an existing config is preserved;
                            first install uses settings.json.example.
  --authorized-keys PATH    Install/replace the daemon authorized_keys file.
                            Without this option, an existing file is preserved;
                            first install creates an empty root-owned file.
  --no-start                Install files but do not enable/start/restart the service.
  -h, --help                Show this help.
USAGE
}

fail() {
    printf 'error: %s\n' "$*" >&2
    exit 1
}

[[ "$(id -u)" -eq 0 ]] || fail "this installer must run as root"
command -v systemctl >/dev/null 2>&1 || fail "systemctl was not found"
command -v getent >/dev/null 2>&1 || fail "getent was not found"

while (($# > 0)); do
    case "$1" in
        --binary)
            (($# >= 2)) || fail "--binary requires a path"
            BINARY_SOURCE="$2"
            shift
            ;;
        --ipc-group)
            (($# >= 2)) || fail "--ipc-group requires a group"
            IPC_GROUP="$2"
            shift
            ;;
        --settings)
            (($# >= 2)) || fail "--settings requires a path"
            SETTINGS_SOURCE="$2"
            shift
            ;;
        --authorized-keys)
            (($# >= 2)) || fail "--authorized-keys requires a path"
            AUTHORIZED_KEYS_SOURCE="$2"
            shift
            ;;
        --no-start)
            START_SERVICE=false
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            fail "unknown option '$1'"
            ;;
    esac
    shift
done

[[ -n "$BINARY_SOURCE" ]] || fail "--binary is required"
[[ -f "$BINARY_SOURCE" && -x "$BINARY_SOURCE" ]] || fail "daemon binary '$BINARY_SOURCE' is not an executable file"
[[ -z "$SETTINGS_SOURCE" || -f "$SETTINGS_SOURCE" ]] || fail "settings file '$SETTINGS_SOURCE' does not exist"
[[ -z "$AUTHORIZED_KEYS_SOURCE" || -f "$AUTHORIZED_KEYS_SOURCE" ]] || fail "authorized-keys file '$AUTHORIZED_KEYS_SOURCE' does not exist"

if ! getent group "$IPC_GROUP" >/dev/null; then
    command -v groupadd >/dev/null 2>&1 || fail "group '$IPC_GROUP' does not exist and groupadd was not found"
    groupadd --system "$IPC_GROUP"
fi
IPC_GROUP="$(getent group "$IPC_GROUP" | cut -d: -f1)"
[[ -n "$IPC_GROUP" ]] || fail "could not resolve IPC group"

install -d -o root -g root -m 0755 "$INSTALL_ROOT" "$DOC_DIR" "$IPC_ROOT"
install -d -o root -g root -m 0750 "$CONFIG_DIR"
install -d -o root -g "$IPC_GROUP" -m 0750 "$IPC_DIR"
install -o root -g root -m 0755 "$BINARY_SOURCE" "$BINARY_TARGET"

if [[ -n "$SETTINGS_SOURCE" ]]; then
    install -o root -g root -m 0640 "$SETTINGS_SOURCE" "$CONFIG_TARGET"
elif [[ ! -e "$CONFIG_TARGET" ]]; then
    install -o root -g root -m 0640 "$SCRIPT_DIR/settings.json.example" "$CONFIG_TARGET"
elif grep -Fq "$LEGACY_IPC_SOCKET" "$CONFIG_TARGET"; then
    # Migrate only the previous deployment default. Other custom endpoint paths
    # remain administrator-owned configuration and are deliberately preserved.
    sed -i "s|$LEGACY_IPC_SOCKET|$IPC_SOCKET|g" "$CONFIG_TARGET"
    chown root:root "$CONFIG_TARGET"
    chmod 0640 "$CONFIG_TARGET"
    printf 'Migrated daemon socket path to %s\n' "$IPC_SOCKET"
fi

if [[ -n "$AUTHORIZED_KEYS_SOURCE" ]]; then
    install -o root -g root -m 0640 "$AUTHORIZED_KEYS_SOURCE" "$AUTHORIZED_KEYS_TARGET"
elif [[ ! -e "$AUTHORIZED_KEYS_TARGET" ]]; then
    install -o root -g root -m 0640 /dev/null "$AUTHORIZED_KEYS_TARGET"
fi

sed "s/@IPC_GROUP@/$IPC_GROUP/g" "$SCRIPT_DIR/ufw-systemd.service.in" \
    | install -o root -g root -m 0644 /dev/stdin "$UNIT_TARGET"

if [[ -f "$SCRIPT_DIR/../../docs/deployment/deployment.md" ]]; then
    install -o root -g root -m 0644 "$SCRIPT_DIR/../../docs/deployment/deployment.md" "$DOC_DIR/deployment.md"
fi

systemctl daemon-reload
if [[ "$START_SERVICE" == true ]]; then
    systemctl enable ufw-systemd.service >/dev/null
    if systemctl is-active --quiet ufw-systemd.service; then
        systemctl restart ufw-systemd.service
    else
        systemctl start ufw-systemd.service
    fi
fi

printf 'Installed Ufw.Systemd to %s\n' "$BINARY_TARGET"
printf 'IPC group: %s (gid %s)\n' "$IPC_GROUP" "$(getent group "$IPC_GROUP" | cut -d: -f3)"
printf 'Configuration: %s\n' "$CONFIG_TARGET"
printf 'Authorized keys: %s\n' "$AUTHORIZED_KEYS_TARGET"
printf 'Host socket directory: %s\n' "$IPC_DIR"
