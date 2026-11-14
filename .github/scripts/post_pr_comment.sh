#!/usr/bin/env bash
# Creates or updates the single test-report comment on a PR.
# Usage: post_pr_comment.sh <dir containing pr-number.txt and comment.md> [marker]   (needs GH_TOKEN and REPO)
# The marker identifies which of our comments to update, so different reports keep separate comments.
set -euo pipefail

dir=$1
pr=$(tr -dc '0-9' < "$dir/pr-number.txt")
[ -n "$pr" ] || { echo "No PR number"; exit 0; }

marker=${2:-'<!-- twitchdownloader-test-report -->'}
{ echo "$marker"; head -c 60000 "$dir/comment.md"; } > "$dir/body.md"

existing=$(gh api "repos/$REPO/issues/$pr/comments" --paginate --jq ".[] | select(.body | startswith(\"$marker\")) | .id" | head -n1)
if [ -n "$existing" ]; then
  gh api -X PATCH "repos/$REPO/issues/comments/$existing" -F body=@"$dir/body.md" > /dev/null
else
  gh api -X POST "repos/$REPO/issues/$pr/comments" -F body=@"$dir/body.md" > /dev/null
fi
