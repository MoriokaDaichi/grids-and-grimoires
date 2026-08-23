#!/usr/bin/env bash
# PostToolUse(Bash) hook: after a `git commit` runs successfully, push to origin.
raw=$(cat)

if ! printf '%s' "$raw" | grep -Eq 'git[[:space:]]+commit\b'; then
  exit 0
fi

OUT=$(git -C "c:/Users/user/G&G" push 2>&1)
RC=$?

if [ $RC -ne 0 ]; then
  ESC=$(printf '%s' "Auto-push failed: $OUT" | sed 's/\\/\\\\/g; s/"/\\"/g' | tr '\n' ' ')
  printf '{"systemMessage": "%s"}' "$ESC"
fi
