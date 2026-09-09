#!/bin/sh
set -eu

readonly socket_path=/run/ufw-web-api/socket/asp.sock
readonly socket_dir=${socket_path%/*}

if [ ! -d "$socket_dir" ] || [ ! -w "$socket_dir" ]; then
    printf 'error: ASP API socket directory is not writable: %s\n' "$socket_dir" >&2
    exit 1
fi

# Kestrel does not remove a stale Unix socket left behind by every possible
# unclean container termination. The socket volume is ASP-owned, so clean only
# this known endpoint before binding it again.
rm -f -- "$socket_path"

# Kestrel creates Unix sockets according to the process umask. Restrict the
# socket and any other runtime-created files to the ASP user/group.
umask 0007

exec dotnet Ufw.Web.dll "$@"
