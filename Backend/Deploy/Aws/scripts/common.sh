#!/usr/bin/env bash
set -Eeuo pipefail
# Các script quản trị chạy bằng sudo; config chỉ root đọc được, không dùng set -x.
ROOT=/opt/junglemaze
CONFIG_FILE="$ROOT/config/.env.aws"
RELEASE_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
require_root() { [[ $EUID -eq 0 ]] || { echo 'Run this command with sudo.' >&2; exit 1; }; }
load_release() { set -a; source "$RELEASE_DIR/release.env"; set +a; }
load_config() {
  [[ -f "$CONFIG_FILE" ]] || { echo 'Run configure.sh first.' >&2; exit 1; }
  set -a; source "$CONFIG_FILE"; set +a
  [[ "${AUTH_MODE:-}" == RoomCode ]] || { echo 'AWS public deployment requires RoomCode.' >&2; exit 1; }
  [[ "${PUBLIC_DOMAIN:-}" =~ ^[a-z0-9-]+\.duckdns\.org$ ]] || { echo 'Invalid DuckDNS hostname.' >&2; exit 1; }
}
compose() {
  docker compose --project-name junglemaze-aws --project-directory "$RELEASE_DIR" \
    --env-file "$RELEASE_DIR/release.env" --env-file "$CONFIG_FILE" -f "$RELEASE_DIR/compose.aws.yaml" "$@"
}
