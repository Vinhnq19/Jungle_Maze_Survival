#!/usr/bin/env bash
set -Eeuo pipefail
source "$(dirname "$0")/common.sh"
require_root
load_release
load_config
[[ $# -eq 1 && -f "$1" ]] || { echo 'Usage: restore.sh /absolute/path/backup.archive.gz' >&2; exit 1; }
read -r -p 'Replace jungle_maze database? Type RESTORE: ' confirm
[[ "$confirm" == RESTORE ]] || exit 1
compose stop api caddy
trap 'compose up -d --no-build --pull never api caddy' EXIT
compose exec -T mongo mongorestore --archive --gzip --drop --nsInclude 'jungle_maze.*' < "$1"
echo 'Restore complete.'
