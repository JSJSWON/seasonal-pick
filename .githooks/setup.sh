#!/bin/bash
#
# Points git at the tracked hooks directory.
#
# Run once per clone. Without it the hooks in .githooks are inert, because
# git only looks in .git/hooks unless told otherwise.
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"
git config core.hooksPath .githooks
echo "core.hooksPath = $(git config core.hooksPath)"
