#!/bin/bash
#
# Installs the two scheduled collector jobs into launchd.
#
# Why launchd rather than cron: this runs on a laptop that sleeps at night.
# cron skips a job whose time passed while the machine was asleep; launchd
# runs it once after waking. Missing a night is not fatal either way - the
# collector is idempotent and the next run catches up - but the schedule is
# supposed to be unattended.
#
# The plists are generated here rather than committed, because they must hold
# absolute paths and those differ per machine.
#
# Usage:  ./install-launchd.sh            install and start
#         ./install-launchd.sh uninstall  stop and remove
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SCRIPT="$REPO_ROOT/collector/scripts/collect.sh"
LOG_DIR="$HOME/Library/Logs/seasonal-pick"
AGENTS="$HOME/Library/LaunchAgents"

RESYNC_LABEL="com.seasonal-pick.resync"
BACKFILL_LABEL="com.seasonal-pick.backfill"

uninstall() {
    for label in "$RESYNC_LABEL" "$BACKFILL_LABEL"; do
        launchctl bootout "gui/$(id -u)/$label" 2>/dev/null || true
        rm -f "$AGENTS/$label.plist"
        echo "제거: $label"
    done
}

if [ "${1:-}" = "uninstall" ]; then
    uninstall
    exit 0
fi

mkdir -p "$LOG_DIR" "$AGENTS"

# $1 label, $2 hour, $3 extra calendar keys, $4... collector arguments
write_plist() {
    local label="$1" hour="$2" extra="$3"
    shift 3
    local args=""
    for arg in "$@"; do
        args="$args
        <string>$arg</string>"
    done

    cat > "$AGENTS/$label.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
  "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>$label</string>
    <key>ProgramArguments</key>
    <array>
        <string>$SCRIPT</string>$args
    </array>
    <key>StartCalendarInterval</key>
    <dict>
        <key>Hour</key><integer>$hour</integer>
        <key>Minute</key><integer>0</integer>$extra
    </dict>
    <key>StandardOutPath</key>
    <string>$LOG_DIR/$label.log</string>
    <key>StandardErrorPath</key>
    <string>$LOG_DIR/$label.log</string>
    <key>WorkingDirectory</key>
    <string>$REPO_ROOT</string>
</dict>
</plist>
PLIST
}

# 02:00 every day. aT registers the day's survey around 15:00, so anything
# after midnight has had time; the exact hour does not matter because the
# re-sync re-reads three weeks each time.
write_plist "$RESYNC_LABEL" 2 "" resync

# 03:00 on the 1st. Picks up anything registered later than the re-sync window
# and any region aT has newly started surveying - the re-sync cannot find one,
# because it only asks regions that already hold data.
write_plist "$BACKFILL_LABEL" 3 "
        <key>Day</key><integer>1</integer>" backfill

for label in "$RESYNC_LABEL" "$BACKFILL_LABEL"; do
    launchctl bootout "gui/$(id -u)/$label" 2>/dev/null || true
    launchctl bootstrap "gui/$(id -u)" "$AGENTS/$label.plist"
    echo "설치: $label"
done

echo
echo "로그: $LOG_DIR"
echo "확인: launchctl list | grep seasonal-pick"
echo "즉시 실행: launchctl kickstart -k gui/$(id -u)/$RESYNC_LABEL"
