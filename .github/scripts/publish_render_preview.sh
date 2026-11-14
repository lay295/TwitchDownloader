#!/usr/bin/env bash
# Hosts the render-preview images on the orphan "render-previews" branch and posts/updates the PR comment that shows them.
# Usage: publish_render_preview.sh <dir from render_preview.py, plus pr-number.txt and head-sha.txt>   (needs GH_TOKEN and REPO)
# ponytail: the branch only grows (~0.5 MB per run); delete it to reclaim space, which breaks the images in old comments.
set -euo pipefail

dir=$(realpath "$1")
pr=$(tr -dc '0-9' < "$dir/pr-number.txt")
sha=$(tr -dc '0-9a-f' < "$dir/head-sha.txt" | head -c 12)
[ -n "$pr" ] && [ -n "$sha" ] || { echo "Missing PR number or head SHA"; exit 1; }
branch=render-previews
dest="pr-$pr/$sha"

# Every case failed to render: nothing to host, the comment only carries the error logs
if ! compgen -G "$dir/images/*.png" > /dev/null; then
  bash "$(dirname "$0")/post_pr_comment.sh" "$dir" '<!-- twitchdownloader-render-preview -->'
  exit 0
fi

work=$(mktemp -d)
git -C "$work" init -q
git -C "$work" remote add origin "https://x-access-token:$GH_TOKEN@github.com/$REPO.git"
git -C "$work" config user.name "github-actions[bot]"
git -C "$work" config user.email "41898282+github-actions[bot]@users.noreply.github.com"

# Retry so two PRs publishing at the same moment don't fail on a non-fast-forward push
for attempt in 1 2 3; do
  if git -C "$work" fetch -q --depth 1 origin "$branch" 2>/dev/null; then
    git -C "$work" checkout -q -B "$branch" FETCH_HEAD
  else
    git -C "$work" checkout -q --orphan "$branch"
    git -C "$work" rm -rq --cached . 2>/dev/null || true
  fi

  rm -rf "${work:?}/$dest"
  mkdir -p "$work/$dest"
  # The images may come from a fork's run: copy only small, real PNG files with plain names
  for f in "$dir"/images/*.png; do
    [ -e "$f" ] || continue
    name=$(basename "$f")
    [[ $name =~ ^[A-Za-z0-9._-]+\.png$ ]] || continue
    [ "$(head -c 8 "$f" | od -An -tx1 | tr -d ' \n')" = "89504e470d0a1a0a" ] || continue
    [ "$(stat -c %s "$f")" -le 5000000 ] || continue
    cp "$f" "$work/$dest/$name"
  done

  git -C "$work" add "$dest"
  git -C "$work" commit -qm "Render preview for #$pr ($sha)" || true
  git -C "$work" push -q origin "HEAD:$branch" && break
  [ "$attempt" = 3 ] && { echo "Could not push $branch"; exit 1; }
  sleep $((attempt * 5))
done

commit=$(git -C "$work" rev-parse HEAD)
sed -i "s|{BASE_URL}|https://raw.githubusercontent.com/$REPO/$commit/$dest|g" "$dir/comment.md"
bash "$(dirname "$0")/post_pr_comment.sh" "$dir" '<!-- twitchdownloader-render-preview -->'
