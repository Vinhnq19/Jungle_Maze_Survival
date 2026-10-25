#!/usr/bin/env bash
# Owned, bounded collector; no credentials, payload or RoomCode. Called only by the guarded test.
set -Eeuo pipefail
for ((i=0; i<140; i++)); do
  docker stats --no-stream --format '{{.MemPerc}}' junglemaze-aws-game-server-1
  sleep 5
done
