#!/usr/bin/env bash
set -euo pipefail

umask 077

readonly SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
readonly DEFAULT_DEV_DIR="$REPO_ROOT/artifacts/dev"
readonly SYSTEMD_CONFIG="$REPO_ROOT/src/Ufw.Systemd/appsettings.json"
readonly WEB_PROJECT="$REPO_ROOT/src/Ufw.Web/Ufw.Web.csproj"
readonly TLS_SERVER_NAME="ufw-systemd"
readonly CA_COMMON_NAME="UFW WebUI Development CA"
readonly SERVER_COMMON_NAME="$TLS_SERVER_NAME"
readonly CLIENT_COMMON_NAME="ufw-web-dev-client"

DEV_DIR="${UFW_DEV_DIR:-$DEFAULT_DEV_DIR}"
FORCE=false
INSTALL_CA=false
CONFIGURE_WEB=true

HOST_OS="unix"
case "$(uname -s 2>/dev/null || true)" in
    MINGW*|MSYS*|CYGWIN*)
        HOST_OS="windows"
        ;;
esac

is_windows() {
    [[ "$HOST_OS" == "windows" ]]
}

usage() {
    cat <<'USAGE'
Usage: scripts/setup-dev.sh [options]

Generates local development credentials and configuration for UFW WebUI:
  - a development CA;
  - daemon TLS server and web-client mTLS certificates;
  - the Ufw.Web RSA JWT signing key;
  - a P-256 browser intent-signing keypair and daemon authorized_keys file;
  - src/Ufw.Systemd/appsettings.json configured for local mTLS;
  - Ufw.Web user-secrets entries for JWT signing and mTLS IPC.

Options:
  --force            Replace generated development material and managed Ufw.Web
                     user-secret values.
  --install-ca       Install the generated development CA into the current host trust
                     store. On Windows/Git Bash this uses the current-user Root store;
                     on Unix this may invoke sudo and supports update-ca-certificates or
                     p11-kit trust.
  --no-user-secrets  Generate files/configuration without modifying Ufw.Web user secrets.
  -h, --help         Show this help.

Environment:
  UFW_DEV_DIR        Generated material directory (default: artifacts/dev). Git Bash
                     accepts either POSIX (/c/...) or native (C:\...) paths.
  UFW_PATH           UFW executable written to daemon config. Defaults to command -v
                     ufw; on Unix falls back to /usr/sbin/ufw. On Windows, point this
                     at a Windows-compatible UFW mock/executable when one is not on PATH.
USAGE
}

fail() {
    printf 'error: %s\n' "$*" >&2
    exit 1
}

warn() {
    printf 'warning: %s\n' "$*" >&2
}

require_command() {
    command -v "$1" >/dev/null 2>&1 || fail "required command '$1' was not found"
}

to_shell_path() {
    local value="$1"
    if ! is_windows; then
        printf '%s\n' "$value"
        return
    fi

    if [[ "$value" =~ ^[A-Za-z]:[\\/] || "$value" == \\* ]]; then
        cygpath -u "$value"
    else
        printf '%s\n' "$value"
    fi
}

to_host_path() {
    local value="$1"
    if is_windows; then
        cygpath -aw "$value"
    else
        printf '%s\n' "$value"
    fi
}

run_windows_native() {
    # Git Bash rewrites path-looking arguments for native Windows executables. All
    # callers of this helper already pass native paths and must preserve them verbatim.
    MSYS2_ARG_CONV_EXCL='*' "$@"
}

dotnet_user_secrets() {
    local project_path
    project_path="$(to_host_path "$WEB_PROJECT")"

    if is_windows; then
        run_windows_native dotnet user-secrets --project "$project_path" "$@"
    else
        dotnet user-secrets --project "$project_path" "$@"
    fi
}

run_as_root() {
    if [[ "$(id -u)" -eq 0 ]]; then
        "$@"
        return
    fi

    require_command sudo
    sudo "$@"
}

json_escape() {
    local value="$1"
    value="${value//\\/\\\\}"
    value="${value//\"/\\\"}"
    value="${value//$'\n'/\\n}"
    value="${value//$'\r'/\\r}"
    value="${value//$'\t'/\\t}"
    printf '%s' "$value"
}

while (($# > 0)); do
    case "$1" in
        --force)
            FORCE=true
            ;;
        --install-ca)
            INSTALL_CA=true
            ;;
        --no-user-secrets)
            CONFIGURE_WEB=false
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

require_command openssl
if is_windows; then
    require_command cygpath
    require_command icacls.exe
    DEV_DIR="$(to_shell_path "$DEV_DIR")"
fi

if [[ "$CONFIGURE_WEB" == true ]]; then
    require_command dotnet

    if [[ "$FORCE" != true ]]; then
        existing_secrets="$(dotnet_user_secrets list 2>/dev/null || true)"
        for key in \
            'Auth:Jwt:SigningKeyPath' \
            'IpcOptions:Endpoint' \
            'IpcOptions:TlsEnabled' \
            'IpcOptions:TlsServerName' \
            'IpcOptions:SslProtocols' \
            'IpcOptions:ClientCertificatePath' \
            'IpcOptions:ClientCertificateKeyPath'; do
            if grep -Fq -- "$key =" <<<"$existing_secrets"; then
                fail "Ufw.Web user secret '$key' is already configured; rerun with --force to replace it or --no-user-secrets to leave user secrets unchanged"
            fi
        done
    fi
fi

[[ -n "$DEV_DIR" && "$DEV_DIR" != "/" ]] || fail "unsafe UFW_DEV_DIR '$DEV_DIR'"
if is_windows && [[ "$DEV_DIR" =~ ^/[A-Za-z]/?$ ]]; then
    fail "unsafe UFW_DEV_DIR '$DEV_DIR'"
fi

readonly PKI_DIR="$DEV_DIR/pki"
readonly AUTH_DIR="$DEV_DIR/auth"
readonly INTENT_DIR="$DEV_DIR/intent"
readonly STATE_DIR="$DEV_DIR/systemd-state"

PIPE_NAME="$DEV_DIR/ufw-systemd.pipe"
PIPE_ENDPOINT="$PIPE_NAME"
# NamedPipeServerStream expects only the pipe name on Windows, while the client
# accepts the canonical \\.\pipe\... endpoint form.
if is_windows; then
    PIPE_NAME="ufw-systemd-dev.pipe"
    PIPE_ENDPOINT='\\.\pipe\ufw-systemd-dev.pipe'
fi
readonly PIPE_NAME
readonly PIPE_ENDPOINT

readonly CA_KEY="$PKI_DIR/ca-key.pem"
readonly CA_CERT="$PKI_DIR/ca-cert.pem"
readonly CA_SERIAL="$PKI_DIR/ca-cert.srl"
readonly SERVER_KEY="$PKI_DIR/server-key.pem"
readonly SERVER_CERT="$PKI_DIR/server-cert.pem"
readonly CLIENT_KEY="$PKI_DIR/client-key.pem"
readonly CLIENT_CERT="$PKI_DIR/client-cert.pem"
readonly JWT_KEY="$AUTH_DIR/jwt-signing-key.pem"
readonly INTENT_PRIVATE_KEY="$INTENT_DIR/intent-key.pem"
readonly INTENT_PUBLIC_KEY="$INTENT_DIR/intent-key.pub.pem"
readonly AUTHORIZED_KEYS="$INTENT_DIR/authorized_keys"

GENERATED_PATHS=(
    "$PKI_DIR"
    "$AUTH_DIR"
    "$INTENT_DIR"
    "$STATE_DIR"
)
if ! is_windows; then
    GENERATED_PATHS+=("$PIPE_NAME")
fi

if [[ "$FORCE" != true ]]; then
    for path in "${GENERATED_PATHS[@]}" "$SYSTEMD_CONFIG"; do
        [[ ! -e "$path" ]] || fail "'$path' already exists; rerun with --force to replace development material"
    done
fi

if [[ "$FORCE" == true ]]; then
    rm -rf -- "$PKI_DIR" "$AUTH_DIR" "$INTENT_DIR" "$STATE_DIR"
    if ! is_windows; then
        rm -f -- "$PIPE_NAME"
    fi
    rm -f -- "$SYSTEMD_CONFIG"
fi

mkdir -p -- "$PKI_DIR" "$AUTH_DIR" "$INTENT_DIR" "$STATE_DIR/nonces"

tmp_dir="$(mktemp -d)"
cleanup() {
    rm -rf -- "$tmp_dir"
}
trap cleanup EXIT

# Use request config files instead of -subj /CN=... so MSYS does not interpret the
# distinguished name as a POSIX path when invoking OpenSSL from Git Bash.
cat > "$tmp_dir/ca-req.cnf" <<EOF_CA_REQ
[req]
prompt = no
distinguished_name = subject

[subject]
CN = $CA_COMMON_NAME
EOF_CA_REQ

cat > "$tmp_dir/server-req.cnf" <<EOF_SERVER_REQ
[req]
prompt = no
distinguished_name = subject

[subject]
CN = $SERVER_COMMON_NAME
EOF_SERVER_REQ

cat > "$tmp_dir/client-req.cnf" <<EOF_CLIENT_REQ
[req]
prompt = no
distinguished_name = subject

[subject]
CN = $CLIENT_COMMON_NAME
EOF_CLIENT_REQ

cat > "$tmp_dir/server.ext" <<EOF_SERVER_EXT
basicConstraints=critical,CA:FALSE
keyUsage=critical,digitalSignature
extendedKeyUsage=serverAuth
subjectAltName=DNS:$TLS_SERVER_NAME
subjectKeyIdentifier=hash
authorityKeyIdentifier=keyid,issuer
EOF_SERVER_EXT

cat > "$tmp_dir/client.ext" <<'EOF_CLIENT_EXT'
basicConstraints=critical,CA:FALSE
keyUsage=critical,digitalSignature
extendedKeyUsage=clientAuth
subjectKeyIdentifier=hash
authorityKeyIdentifier=keyid,issuer
EOF_CLIENT_EXT

printf 'Generating development CA...\n'
openssl genpkey \
    -algorithm RSA \
    -pkeyopt rsa_keygen_bits:3072 \
    -out "$CA_KEY" >/dev/null 2>&1
openssl req \
    -x509 \
    -new \
    -sha256 \
    -days 3650 \
    -key "$CA_KEY" \
    -config "$tmp_dir/ca-req.cnf" \
    -addext 'basicConstraints=critical,CA:TRUE' \
    -addext 'keyUsage=critical,keyCertSign,cRLSign' \
    -addext 'subjectKeyIdentifier=hash' \
    -out "$CA_CERT"

printf 'Generating daemon TLS server certificate...\n'
openssl genpkey \
    -algorithm EC \
    -pkeyopt ec_paramgen_curve:P-256 \
    -out "$SERVER_KEY" >/dev/null 2>&1
openssl req \
    -new \
    -sha256 \
    -key "$SERVER_KEY" \
    -config "$tmp_dir/server-req.cnf" \
    -out "$tmp_dir/server.csr"
openssl x509 \
    -req \
    -sha256 \
    -days 825 \
    -in "$tmp_dir/server.csr" \
    -CA "$CA_CERT" \
    -CAkey "$CA_KEY" \
    -CAserial "$CA_SERIAL" \
    -CAcreateserial \
    -extfile "$tmp_dir/server.ext" \
    -out "$SERVER_CERT" >/dev/null

printf 'Generating Ufw.Web mTLS client certificate...\n'
openssl genpkey \
    -algorithm EC \
    -pkeyopt ec_paramgen_curve:P-256 \
    -out "$CLIENT_KEY" >/dev/null 2>&1
openssl req \
    -new \
    -sha256 \
    -key "$CLIENT_KEY" \
    -config "$tmp_dir/client-req.cnf" \
    -out "$tmp_dir/client.csr"
openssl x509 \
    -req \
    -sha256 \
    -days 825 \
    -in "$tmp_dir/client.csr" \
    -CA "$CA_CERT" \
    -CAkey "$CA_KEY" \
    -CAserial "$CA_SERIAL" \
    -extfile "$tmp_dir/client.ext" \
    -out "$CLIENT_CERT" >/dev/null

openssl verify -CAfile "$CA_CERT" -purpose sslserver "$SERVER_CERT" >/dev/null
openssl verify -CAfile "$CA_CERT" -purpose sslclient "$CLIENT_CERT" >/dev/null

printf 'Generating JWT signing key...\n'
openssl genpkey \
    -algorithm RSA \
    -pkeyopt rsa_keygen_bits:3072 \
    -out "$JWT_KEY" >/dev/null 2>&1

printf 'Generating browser intent-signing keypair...\n'
openssl genpkey \
    -algorithm EC \
    -pkeyopt ec_paramgen_curve:P-256 \
    -out "$INTENT_PRIVATE_KEY" >/dev/null 2>&1
openssl pkey \
    -in "$INTENT_PRIVATE_KEY" \
    -pubout \
    -out "$INTENT_PUBLIC_KEY"
{
    printf '# Development browser intent-signing public key\n'
    cat "$INTENT_PUBLIC_KEY"
} > "$AUTHORIZED_KEYS"

protect_private_file() {
    local path="$1"
    if ! is_windows; then
        chmod 0600 "$path"
        return
    fi

    local native_path principal
    native_path="$(to_host_path "$path")"
    principal="$(whoami.exe 2>/dev/null | tr -d '\r' || whoami | tr -d '\r')"
    run_windows_native icacls.exe "$native_path" \
        /inheritance:r \
        /grant:r "$principal:(F)" 'SYSTEM:(F)' >/dev/null
}

for private_file in "$CA_KEY" "$SERVER_KEY" "$CLIENT_KEY" "$JWT_KEY" "$INTENT_PRIVATE_KEY"; do
    protect_private_file "$private_file"
done
if ! is_windows; then
    chmod 0644 "$CA_CERT" "$SERVER_CERT" "$CLIENT_CERT" "$INTENT_PUBLIC_KEY" "$AUTHORIZED_KEYS"
fi

ufw_path="${UFW_PATH:-}"
if [[ -n "$ufw_path" ]]; then
    ufw_path="$(to_shell_path "$ufw_path")"
else
    ufw_path="$(command -v ufw || true)"
fi
if [[ -z "$ufw_path" ]]; then
    if is_windows; then
        windows_mock="$REPO_ROOT/src/artifacts/bin/Ufw.Mock/debug/Ufw.Mock.exe"
        if [[ -f "$windows_mock" ]]; then
            ufw_path="$windows_mock"
            printf 'Using built Ufw.Mock for Windows development: %s\n' "$windows_mock"
        else
            ufw_path="ufw.exe"
            warn "ufw was not found; set UFW_PATH to a Windows-compatible UFW mock/executable before running Ufw.Systemd (for example a built Ufw.Mock.exe)"
        fi
    else
        ufw_path="/usr/sbin/ufw"
        warn "ufw was not found; generated daemon config uses '$ufw_path' and will not validate until UFW is installed or UFW_PATH is set"
    fi
fi
if is_windows && [[ "$ufw_path" != "ufw.exe" ]]; then
    ufw_path="$(to_host_path "$ufw_path")"
fi

config_server_cert="$(to_host_path "$SERVER_CERT")"
config_server_key="$(to_host_path "$SERVER_KEY")"
config_authorized_keys="$(to_host_path "$AUTHORIZED_KEYS")"
config_nonce_store="$(to_host_path "$STATE_DIR/nonces")"
config_deployment_id="$(to_host_path "$STATE_DIR/deployment-id")"

escaped_ufw_path="$(json_escape "$ufw_path")"
escaped_pipe_name="$(json_escape "$PIPE_NAME")"
escaped_server_cert="$(json_escape "$config_server_cert")"
escaped_server_key="$(json_escape "$config_server_key")"
escaped_authorized_keys="$(json_escape "$config_authorized_keys")"
escaped_nonce_store="$(json_escape "$config_nonce_store")"
escaped_deployment_id="$(json_escape "$config_deployment_id")"

cat > "$SYSTEMD_CONFIG" <<EOF_SYSTEMD_CONFIG
{
  "debug_mode": true,
  "ufw_path": "$escaped_ufw_path",
  "write_to_console": true,
  "pipe": {
    "pipe_name": "$escaped_pipe_name",
    "tls_enabled": true,
    "ssl_protocols": "none",
    "remote_certificate_validation": {
      "required_issuer": "CN=$CA_COMMON_NAME",
      "required_subject": "CN=$CLIENT_COMMON_NAME"
    },
    "server_certificate_path": "$escaped_server_cert",
    "server_certificate_key_path": "$escaped_server_key"
  },
  "network": {
    "max_connections": 8,
    "io_timeout": "00:00:30",
    "request_timeout": "00:30:00"
  },
  "security": {
    "authorized_keys_path": "$escaped_authorized_keys",
    "nonce_store_path": "$escaped_nonce_store",
    "deployment_id_path": "$escaped_deployment_id",
    "max_intent_age": "00:05:00",
    "clock_skew": "00:00:30"
  }
}
EOF_SYSTEMD_CONFIG
if ! is_windows; then
    chmod 0600 "$SYSTEMD_CONFIG"
fi

if [[ "$CONFIGURE_WEB" == true ]]; then
    printf 'Configuring Ufw.Web user secrets...\n'
    web_jwt_key="$(to_host_path "$JWT_KEY")"
    web_client_cert="$(to_host_path "$CLIENT_CERT")"
    web_client_key="$(to_host_path "$CLIENT_KEY")"

    dotnet_user_secrets set 'Auth:Jwt:SigningKeyPath' "$web_jwt_key" >/dev/null
    dotnet_user_secrets set 'IpcOptions:Endpoint' "$PIPE_ENDPOINT" >/dev/null
    dotnet_user_secrets set 'IpcOptions:TlsEnabled' 'true' >/dev/null
    dotnet_user_secrets set 'IpcOptions:TlsServerName' "$TLS_SERVER_NAME" >/dev/null
    dotnet_user_secrets set 'IpcOptions:SslProtocols' 'None' >/dev/null
    dotnet_user_secrets set 'IpcOptions:ClientCertificatePath' "$web_client_cert" >/dev/null
    dotnet_user_secrets set 'IpcOptions:ClientCertificateKeyPath' "$web_client_key" >/dev/null
fi

install_ca() {
    if is_windows; then
        require_command certutil.exe
        local native_ca
        native_ca="$(to_host_path "$CA_CERT")"
        run_windows_native certutil.exe -user -addstore -f Root "$native_ca" >/dev/null
        return
    fi

    if command -v update-ca-certificates >/dev/null 2>&1; then
        local destination='/usr/local/share/ca-certificates/ufw-webui-development-ca.crt'
        run_as_root install -m 0644 "$CA_CERT" "$destination"
        run_as_root update-ca-certificates
        return
    fi

    if command -v trust >/dev/null 2>&1; then
        run_as_root trust anchor "$CA_CERT"
        return
    fi

    fail "could not install the development CA: neither update-ca-certificates nor p11-kit trust is available"
}

if [[ "$INSTALL_CA" == true ]]; then
    printf 'Installing development CA into the host trust store...\n'
    install_ca
fi

cat <<EOF_SUMMARY

Development material generated successfully.

  Development CA:       $CA_CERT
  Daemon server cert:   $SERVER_CERT
  Web mTLS client cert: $CLIENT_CERT
  JWT signing key:      $JWT_KEY
  Intent private key:   $INTENT_PRIVATE_KEY
  Authorized keys:      $AUTHORIZED_KEYS
  Daemon config:        $SYSTEMD_CONFIG
  IPC endpoint:         $PIPE_ENDPOINT

The generated intent private key is the value to paste into the client's per-mutation
private-key field during development.
EOF_SUMMARY

if [[ "$INSTALL_CA" != true ]]; then
    cat <<EOF_TRUST

The development CA has NOT been installed into the OS trust store. The IPC TLS/mTLS
handshake uses normal .NET certificate-chain validation, so trust '$CA_CERT' before
running Ufw.Web/Ufw.Systemd. Re-run this script with --install-ca, or install that CA
manually using your platform's trust-store tooling. On Windows, --install-ca targets
the current user's Root certificate store and does not require an elevated Git Bash.
EOF_TRUST
fi

cat <<EOF_RUN

Suggested development commands after the CA is trusted:

  dotnet run --project "$REPO_ROOT/src/Ufw.Systemd" --no-launch-profile -- serve --config "$SYSTEMD_CONFIG"
  dotnet run --project "$REPO_ROOT/src/Ufw.Web"
  dotnet run --project "$REPO_ROOT/src/Ufw.Client"
EOF_RUN
