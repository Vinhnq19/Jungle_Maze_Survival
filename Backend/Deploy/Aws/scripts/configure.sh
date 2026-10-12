#!/usr/bin/env bash
set -Eeuo pipefail
source "$(dirname "$0")/common.sh"
require_root
load_release
[[ ! -f "$CONFIG_FILE" ]] || { echo 'Config exists; edit it with sudo nano. Existing keys are preserved.' >&2; exit 1; }
umask 077
install -d -m 0700 "$ROOT/config"
read -r -p "DuckDNS hostname [$DEFAULT_DOMAIN]: " domain
domain="${domain:-$DEFAULT_DOMAIN}"
[[ "$domain" =~ ^[a-z0-9-]+\.duckdns\.org$ ]] || { echo 'Use a .duckdns.org hostname.' >&2; exit 1; }
read -r -s -p 'DuckDNS token (hidden): ' token; echo
[[ "$token" =~ ^[a-zA-Z0-9-]{16,128}$ ]] || { echo 'Invalid DuckDNS token format.' >&2; exit 1; }
read -r -p 'Email for HTTPS certificate: ' email
[[ "$email" =~ ^[a-zA-Z0-9._+%-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$ ]] || { echo 'Invalid email.' >&2; exit 1; }
read -r -p 'Swagger admin username [teacher]: ' admin
admin="${admin:-teacher}"
[[ "$admin" =~ ^[a-zA-Z0-9_-]{1,32}$ ]] || { echo 'Invalid username.' >&2; exit 1; }
read -r -s -p 'Swagger password, 12-64 ASCII characters (hidden): ' password; echo
read -r -s -p 'Repeat Swagger password (hidden): ' repeated; echo
[[ "$password" == "$repeated" && "${#password}" -ge 12 && "${#password}" -le 64 ]] || { echo 'Password mismatch/length.' >&2; exit 1; }
if printf '%s' "$password" | LC_ALL=C grep -q '[^ -~]'; then echo 'Use printable ASCII for this administrator password.' >&2; exit 1; fi
# Stdin, không truyền password vào argv/process list. Chỉ lưu bcrypt hash của Swagger.
hash="$(printf '%s\n' "$password" | docker run --rm -i "$CADDY_IMAGE" caddy hash-password --algorithm bcrypt --bcrypt-cost 12)"
[[ "$hash" == '$2'* ]] || { echo 'Cannot create password hash.' >&2; exit 1; }
unset password repeated
read -r -s -p 'Room code (hidden; Enter generates one): ' room; echo
room="${room:-Class_$(openssl rand -hex 16)}"
[[ "$room" =~ ^[a-zA-Z0-9_-]{8,64}$ ]] || { echo 'Use 8-64 letters/digits/_/- for the room code.' >&2; exit 1; }
key="$(openssl rand -base64 48 | tr -d '\n')"
# Single quotes giữ nguyên dấu $ của bcrypt trong cả Bash và Compose dotenv.
{
  printf "PUBLIC_DOMAIN='%s'\nACME_EMAIL='%s'\nDUCKDNS_TOKEN='%s'\n" "$domain" "$email" "$token"
  printf "SWAGGER_USER='%s'\nSWAGGER_HASH='%s'\nJWT_SIGNING_KEY='%s'\n" "$admin" "$hash" "$key"
  printf "AUTH_MODE='RoomCode'\nROOM_CODE='%s'\n" "$room"
} > "$CONFIG_FILE"
chmod 600 "$CONFIG_FILE"
python3 - "$ROOT/client.aws.json" "$domain" <<'PY'
import json, sys
with open(sys.argv[1], 'w') as output:
    json.dump(dict(mode='Online', apiBaseUrl='https://' + sys.argv[2], gameServerHost=sys.argv[2],
                   gameServerPort=7777, authMode='RoomCode', protocolVersion=1, connectTimeoutMs=5000), output, indent=2)
PY
chmod 644 "$ROOT/client.aws.json"
echo 'Config saved privately. Read only ROOM_CODE with: sudo grep "^ROOM_CODE=" /opt/junglemaze/config/.env.aws'
