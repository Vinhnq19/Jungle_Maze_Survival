#!/usr/bin/env bash
set -Eeuo pipefail
source "$(dirname "$0")/common.sh"
require_root
previous="$(readlink -f "$ROOT/previous" 2>/dev/null || true)"
[[ "$previous" == "$ROOT/releases/"* && -f "$previous/release.env" ]] || { echo 'No previous release.' >&2; exit 1; }
current="$(readlink -f "$ROOT/current")"
# Rollback không được tự đổi major DB hoặc mở lại image lỗi kernel của release cũ.
previous_major="$(sed -n 's/^MONGO_MAJOR=//p' "$previous/release.env")"
current_major="$(sed -n 's/^MONGO_MAJOR=//p' "$current/release.env")"
if [[ "${previous_major:-8}" != "${current_major:-8}" ]]; then
  echo 'Rollback across MongoDB major versions is blocked; database migration needs a separate backup/restore plan.' >&2
  exit 1
fi
ln -sfn "$previous" "$ROOT/current"
ln -sfn "$current" "$ROOT/previous"
# Image cũ được giữ trong Docker; không đổi config hoặc MongoDB schema/volume.
systemctl restart junglemaze.service
echo 'Previous release restored; database volume preserved.'
