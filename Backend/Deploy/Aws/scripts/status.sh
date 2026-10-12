#!/usr/bin/env bash
set -Eeuo pipefail
source "$(dirname "$0")/common.sh"
require_root
load_release
load_config
compose ps
mapfile -t containers < <(compose ps -q)
if (( ${#containers[@]} )); then docker stats --no-stream "${containers[@]}"; fi
echo "HTTPS: https://$PUBLIC_DOMAIN/health ; Swagger: https://$PUBLIC_DOMAIN/swagger"
compose logs --tail 12 game-server
