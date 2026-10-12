#!/usr/bin/env bash
set -Eeuo pipefail
source "$(dirname "$0")/common.sh"
require_root
load_release
load_config
# Stop giữ volumes, khác down -v. EC2 Stop do người quản trị thực hiện trên Console.
compose stop --timeout 20
