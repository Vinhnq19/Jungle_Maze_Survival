#!/usr/bin/env bash
set -Eeuo pipefail
source "$(dirname "$0")/common.sh"
require_root
load_release
load_config
compose config --quiet
python3 "$RELEASE_DIR/scripts/duckdns.py" || echo 'DNS update will retry via systemd timer.' >&2
compose up -d --no-build --pull never --wait --wait-timeout 180
echo "Services running. HTTPS certificate may need DNS propagation: https://$PUBLIC_DOMAIN/health"
