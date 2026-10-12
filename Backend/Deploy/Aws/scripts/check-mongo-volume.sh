#!/usr/bin/env bash
set -Eeuo pipefail
# Chỉ đọc volume. Không xóa file, không chạy mongod, không sửa config hoặc gọi DuckDNS.
# Tham số riêng cho phép test bằng volume synthetic, tránh đụng DB của team.
[[ $# -eq 3 && "$1" =~ ^[a-zA-Z0-9_.-]+$ && "$3" =~ ^[0-9]+$ ]] || {
  echo 'Usage: check-mongo-volume.sh VOLUME MONGO_IMAGE PREVIOUS_MAJOR' >&2; exit 1;
}
volume="$1"
image="$2"
previous_major="$3"
[[ "$previous_major" == 7 ]] && exit 0
volumes="$(docker volume ls --format '{{.Name}}')"
printf '%s\n' "$volumes" | grep -Fxq -- "$volume" || exit 0
# Khi source là 8/unknown, yêu cầu volume trống hoàn toàn. Từ chối cả lock/index file.
if ! docker run --rm --network none --memory 128m --entrypoint bash \
    --mount "type=volume,source=$volume,target=/check,readonly" "$image" \
    -c 'entry="$(find /check -mindepth 1 -maxdepth 1 -print -quit)" || exit 1; test -z "$entry"'; then
  echo 'Refusing MongoDB 8/unknown -> 7: volume is not empty. Preserve it; migrate with backup/restore on compatible hosts.' >&2
  exit 1
fi
echo 'MongoDB volume is empty; switching to 7.0 is safe for this new test database.'
