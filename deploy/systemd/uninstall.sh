#!/usr/bin/env bash
set -euo pipefail

readonly SERVICE_NAME="ufw-systemd.service"
readonly INSTALL_ROOT="/usr/local/lib/ufw-webui"
readonly BINARY_TARGET="$INSTALL_ROOT/ufw-systemd"
readonly CONFIG_DIR="/etc/ufw-manager"
readonly DAEMON_STATE_DIR="/var/lib/ufw-manager"
readonly IPC_ROOT="/var/lib/ufw-webui"
readonly IPC_DIR="$IPC_ROOT/ipc"
readonly LEGACY_IPC_DIR="/run/ufw-manager"
readonly UNIT_TARGET="/etc/systemd/system/$SERVICE_NAME"
readonly DOC_DIR="/usr/local/share/doc/ufw-webui"
readonly DOC_TARGET="$DOC_DIR/deployment.md"

PURGE=false

usage() {
    cat <<'USAGE'
Usage: sudo deploy/systemd/uninstall.sh [options]

Uninstalls the privileged UFW WebUI daemon.

By default the daemon configuration, authorized keys, replay state, and
persistent deployment identity are preserved so a later reinstall can resume
the same logical deployment safely.

Options:
  --purge       Also remove /etc/ufw-manager and /var/lib/ufw-manager.
                This deletes daemon configuration, authorized keys, nonce
                history, and deployment identity. It does not modify UFW rules.
  -h, --help    Show this help.
USAGE
}

fail() {
    printf 'error: %s\n' "$*" >&2
    exit 1
}

[[ "$(id -u)" -eq 0 ]] || fail "this uninstaller must run as root"
command -v systemctl >/dev/null 2>&1 || fail "systemctl was not found"

while (($# > 0)); do
    case "$1" in
        --purge)
            PURGE=true
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

# Stop before removing the executable or socket path. A failed stop is fatal:
# deleting files underneath a still-running privileged daemon would leave the
# host in a partially uninstalled state.
if systemctl is-active --quiet "$SERVICE_NAME"; then
    systemctl stop "$SERVICE_NAME"
fi

# disable returns nonzero when the unit is already disabled or absent, both of
# which are valid uninstall states.
systemctl disable "$SERVICE_NAME" >/dev/null 2>&1 || true

rm -f -- "$UNIT_TARGET"
rm -f -- "$BINARY_TARGET"
rmdir -- "$INSTALL_ROOT" 2>/dev/null || true

rm -f -- "$DOC_TARGET"
rmdir -- "$DOC_DIR" 2>/dev/null || true

# The IPC tree is installer-owned ephemeral runtime state. Remove it after the
# service has stopped, then remove the parent only when nothing else uses it.
rm -rf -- "$IPC_DIR"
rmdir -- "$IPC_ROOT" 2>/dev/null || true

# Clean up the previous deployment default if this host was upgraded from a
# version that placed the daemon socket below /run. Do not recursively remove
# the legacy directory because an administrator may have put other files there.
rm -f -- "$LEGACY_IPC_DIR/ufw-systemd.sock"
rmdir -- "$LEGACY_IPC_DIR" 2>/dev/null || true

if [[ "$PURGE" == true ]]; then
    rm -rf -- "$CONFIG_DIR" "$DAEMON_STATE_DIR"
fi

systemctl daemon-reload
systemctl reset-failed "$SERVICE_NAME" >/dev/null 2>&1 || true

printf 'Uninstalled Ufw.Systemd.\n'
printf 'UFW firewall rules were not modified.\n'
if [[ "$PURGE" == true ]]; then
    printf 'Removed daemon configuration and persistent state.\n'
else
    printf 'Preserved daemon configuration: %s\n' "$CONFIG_DIR"
    printf 'Preserved daemon persistent state: %s\n' "$DAEMON_STATE_DIR"
    printf 'Use --purge to remove those directories as well.\n'
fi
printf 'The IPC group was not removed because it may be administrator-owned or a user primary group.\n'
