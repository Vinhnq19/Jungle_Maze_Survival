#!/usr/bin/env bash
set -Eeuo pipefail
source "$(dirname "$0")/common.sh"
require_root
load_release
load_config
umask 077
install -d -m 0700 "$ROOT/backups"
output="$ROOT/backups/users-$(date -u +%Y%m%dT%H%M%SZ)-$RANDOM.archive.gz"
partial="$output.partial"
trap 'rm -f -- "$partial"' EXIT
# Binary stream redirect tới file, không in account/hash ra terminal.
compose exec -T mongo mongodump --db jungle_maze --archive --gzip > "$partial"
[[ -s "$partial" ]] || { echo 'Empty backup.' >&2; exit 1; }
mv -- "$partial" "$output"
echo "Backup: $output. Copy off EC2 before account expiry/termination."
