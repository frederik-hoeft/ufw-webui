#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_FILE="${UFW_COMPOSE_FILE:-$SCRIPT_DIR/compose.yml}"
ENV_FILE="${UFW_COMPOSE_ENV_FILE:-$SCRIPT_DIR/.env}"
BACKUP_DIR="${1:-$SCRIPT_DIR/backups}"

mkdir -p -- "$BACKUP_DIR"
chmod 0700 "$BACKUP_DIR"

timestamp="$(date -u +'%Y%m%dT%H%M%SZ')"
output="$BACKUP_DIR/ufw-webui-$timestamp.dump"
temporary="$output.tmp"
trap 'rm -f -- "$temporary"' EXIT

docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" exec -T postgres \
    sh -c 'exec pg_dump --format=custom --dbname="$POSTGRES_DB" --username="$POSTGRES_USER"' \
    > "$temporary"
chmod 0600 "$temporary"
mv -- "$temporary" "$output"
trap - EXIT
printf '%s\n' "$output"
