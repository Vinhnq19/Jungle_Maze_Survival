#!/usr/bin/env bash
set -Eeuo pipefail
source "$(dirname "$0")/common.sh"
require_root
bundle="$(realpath "${1:-$RELEASE_DIR}")"
cd "$bundle"
# Kiểm tra mọi file trước khi chạy script/import image. Không nhận archive chứa config riêng.
sha256sum -c SHA256SUMS
[[ ! -f .env && ! -f .env.aws ]] || { echo 'Bundle must not contain real secrets.' >&2; exit 1; }
source "$bundle/release.env"
[[ "$RELEASE_ID" =~ ^[a-z0-9-]+$ ]] || { echo 'Invalid release ID.' >&2; exit 1; }
[[ "$(uname -m)" == x86_64 ]] || { echo 'Use x86_64 EC2.' >&2; exit 1; }
docker load -i "$bundle/images.tar.gz"
if [[ "${MONGO_MAJOR:-8}" == 7 ]]; then
  previous_major=8
  if [[ -f "$ROOT/current/release.env" ]]; then
    previous_major="$(sed -n 's/^MONGO_MAJOR=//p' "$ROOT/current/release.env")"
    previous_major="${previous_major:-8}"
  fi
  # Release cũ dùng MongoDB 8: không hạ version trên data files. Guard chạy trước đổi current.
  bash "$bundle/scripts/check-mongo-volume.sh" junglemaze-aws_mongo-data "$MONGO_IMAGE" "$previous_major"
fi
install -d -m 0755 "$ROOT/releases"
target="$ROOT/releases/$RELEASE_ID"
if [[ -d "$target" ]]; then
  cmp -s "$bundle/SHA256SUMS" "$target/SHA256SUMS" || { echo 'Release ID already exists with different contents.' >&2; exit 1; }
else
  mkdir "$target"
  cp -a "$bundle/." "$target/"
fi
# Xóa archive image đã load khỏi bản cài để không giữ trùng hàng trăm MB mỗi release.
# Đường dẫn target cố định trong /opt/junglemaze/releases; config/volume không nằm ở đây.
rm -f -- "$target/images.tar.gz"
if [[ ! -f "$CONFIG_FILE" ]]; then bash "$target/scripts/configure.sh"; fi
previous="$(readlink -f "$ROOT/current" 2>/dev/null || true)"
[[ -z "$previous" || "$previous" == "$target" ]] || ln -sfn "$previous" "$ROOT/previous"
ln -sfn "$target" "$ROOT/current"
cat > /etc/systemd/system/junglemaze.service <<'UNIT'
[Unit]
Description=Jungle Maze Docker services
Requires=docker.service
After=docker.service network-online.target
Wants=network-online.target junglemaze-dns.timer
[Service]
Type=oneshot
RemainAfterExit=yes
ExecStart=/bin/bash /opt/junglemaze/current/scripts/start.sh
ExecStop=/bin/bash /opt/junglemaze/current/scripts/stop.sh
TimeoutStartSec=240
TimeoutStopSec=60
[Install]
WantedBy=multi-user.target
UNIT
cat > /etc/systemd/system/junglemaze-dns.service <<'UNIT'
[Unit]
Description=Update Jungle Maze DuckDNS IPv4
After=network-online.target
[Service]
Type=oneshot
User=root
ExecStart=/usr/bin/python3 /opt/junglemaze/current/scripts/duckdns.py
UNIT
cat > /etc/systemd/system/junglemaze-dns.timer <<'UNIT'
[Unit]
Description=Retry DuckDNS update every minute
[Timer]
OnBootSec=20s
OnUnitActiveSec=60s
[Install]
WantedBy=timers.target
UNIT
systemctl daemon-reload
systemctl enable --now junglemaze-dns.timer
systemctl enable junglemaze.service
if ! systemctl restart junglemaze.service; then
  echo 'Deploy failed. Read journalctl -u junglemaze; use rollback.sh if a previous release exists.' >&2
  exit 1
fi
echo "Deployed release $RELEASE_ID. Run: sudo bash /opt/junglemaze/current/scripts/status.sh"
