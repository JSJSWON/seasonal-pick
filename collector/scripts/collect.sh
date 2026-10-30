#!/bin/bash
#
# Runs the collector with a timestamped log line, for launchd to call.
#
# launchd starts jobs with almost no environment - no PATH worth the name and
# no working directory - so both are established here rather than assumed.
# Everything is resolved from this script's own location, so moving the
# repository does not break the schedule.
#
# Usage:  collect.sh resync [--days 21]
#         collect.sh backfill
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PROJECT="$REPO_ROOT/collector/SeasonalPick.Collector"

# launchd's PATH does not include the .NET install, and `which dotnet` fails
# there for the same reason. The common install locations are tried in turn so
# the script keeps working if the SDK moves.
DOTNET=""
for candidate in /usr/local/share/dotnet/dotnet /opt/homebrew/bin/dotnet "$HOME/.dotnet/dotnet"; do
    if [ -x "$candidate" ]; then
        DOTNET="$candidate"
        break
    fi
done

if [ -z "$DOTNET" ]; then
    echo "[$(date '+%F %T')] dotnet을 찾을 수 없습니다. collect.sh의 후보 경로를 확인하세요." >&2
    exit 1
fi

echo "[$(date '+%F %T')] 시작: $*"
"$DOTNET" run --project "$PROJECT" --configuration Release -- "$@"
status=$?
echo "[$(date '+%F %T')] 종료코드 $status"
exit $status
