#!/usr/bin/env python3
"""Render the committed sample chats with the base-branch CLI and the PR CLI, and lay the results side by side for review.

Writes <out>/images/*.png, <out>/logs/*.txt and <out>/comment.md. Image links in comment.md use the {BASE_URL} placeholder,
which publish_render_preview.sh replaces once the images are hosted.
"""
import argparse
import os
import subprocess
import tempfile

# Real chat, downloaded at run time rather than committed (it's large and contains real viewers' messages). Twitch highlights
# don't expire, and the embedded images (-E) let both builds render the same file offline.
REAL_CHAT = ["chatdownload", "--id", "1822879862", "-b", "0", "-e", "120", "-E"]

# (name, fixture relative to TestData or a chatdownload command, description, extra chatrender args)
CASES = [
    ("real-chat", REAL_CHAT, "real highlight chat: 1,000+ messages, real emotes and badges",
     []),
    ("default", "Chat/sample_chat_small.json", "default options",
     []),
    ("light-outline", "Chat/sample_chat_small.json", "light theme, outline, timestamps, avatars, alternate backgrounds",
     ["--background-color", "#FFFFFF", "--alt-background-color", "#E8E8E8", "--message-color", "#000000",
      "--outline", "--timestamp", "--avatars", "--alternate-backgrounds"]),
    ("wide-large-font", "Render/synthetic_chat.json", "wide chat, large font, Twemoji, animated emotes",
     ["--chat-width", "500", "--font-size", "18", "--emoji-vendor", "twemoji"]),
]
# Lossless output, so frame hashes and the difference image show only real rendering changes, not codec noise
COMMON_ARGS = ["--offline", "--framerate", "10", "--chat-height", "500", '--output-args=-c:v ffv1 "{save_path}"']
FPS = 10


def run(cmd, log_path=None, timeout=600):
    result = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=timeout)
    if log_path:
        with open(log_path, "w", encoding="utf-8") as f:
            f.write(" ".join(cmd) + "\n\n" + result.stdout)
    return result


def frame_hashes(video):
    out = run(["ffmpeg", "-v", "error", "-i", video, "-f", "framemd5", "-"]).stdout
    return [line.rsplit(",", 1)[-1].strip() for line in out.splitlines() if line and not line.startswith("#")]


def extract_frames(video, frames, png):
    """Stacks the given frame indices vertically into one PNG."""
    select = "+".join(f"eq(n\\,{n})" for n in frames)
    vf = f"select={select},tile=1x{len(frames)}:padding=6:color=0x808080"
    return run(["ffmpeg", "-v", "error", "-y", "-i", video, "-vf", vf, "-frames:v", "1", png]).returncode == 0


def diff_image(base_png, head_png, png):
    """Pixel difference between the two PNGs, brightened 4x so small changes are visible. Fails if the sizes differ."""
    graph = "[0]format=gbrp[a];[1]format=gbrp[b];[a][b]blend=all_mode=difference,lutrgb=r=val*4:g=val*4:b=val*4"
    return run(["ffmpeg", "-v", "error", "-y", "-i", base_png, "-i", head_png, "-filter_complex", graph, png]).returncode == 0


def tail(path, lines=15):
    with open(path, encoding="utf-8", errors="replace") as f:
        return "".join(f.readlines()[-lines:]).strip()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--base", required=True, help="Base-branch TwitchDownloaderCLI executable")
    parser.add_argument("--head", required=True, help="PR TwitchDownloaderCLI executable")
    parser.add_argument("--fixtures", required=True, help="TestData directory holding the sample chats")
    parser.add_argument("--out", required=True)
    parser.add_argument("--base-label", default="base")
    parser.add_argument("--head-label", default="PR")
    args = parser.parse_args()

    images, logs = os.path.join(args.out, "images"), os.path.join(args.out, "logs")
    os.makedirs(images, exist_ok=True)
    os.makedirs(logs, exist_ok=True)

    summary, details = [], []
    for name, source, description, extra in CASES:
        if isinstance(source, list):
            chat = os.path.join(tempfile.mkdtemp(), f"{name}.json")
            download = [os.path.abspath(args.head), *source, "-o", chat, "--temp-path", tempfile.mkdtemp()]
            if run(download, os.path.join(logs, f"{name}-download.txt")).returncode != 0 or not os.path.exists(chat):
                summary.append(f"| `{name}` | {description} | ⏭️ skipped: chat download failed (Twitch unavailable?) |")
                continue
            fixture = " ".join(source)
        else:
            chat, fixture = os.path.join(args.fixtures, source), source

        results = {}
        for side, cli in (("base", args.base), ("head", args.head)):
            video = os.path.join(tempfile.mkdtemp(), f"{name}-{side}.mkv")
            log = os.path.join(logs, f"{name}-{side}.txt")
            cmd = [os.path.abspath(cli), "chatrender", "-i", chat, "-o", video,
                   "--temp-path", tempfile.mkdtemp(), *COMMON_ARGS, *extra]
            ok = run(cmd, log).returncode == 0 and os.path.exists(video)
            results[side] = (ok, video, log, frame_hashes(video) if ok else [])

        (base_ok, base_video, base_log, base_hashes), (head_ok, head_video, head_log, head_hashes) = results["base"], results["head"]
        rows = []
        if base_ok and head_ok:
            count = min(len(base_hashes), len(head_hashes))
            differing = [i for i in range(count) if base_hashes[i] != head_hashes[i]]
            changed = bool(differing) or len(base_hashes) != len(head_hashes)
            if not changed:
                status = "✅ identical"
            else:
                status = f"⚠️ {len(differing)}/{count} frames differ"
                status += f" (first at {differing[0] / FPS:.1f}s)" if differing else ""
                status += f", frame count {len(base_hashes)} → {len(head_hashes)}" if len(base_hashes) != len(head_hashes) else ""
            # First differing frame (or the middle one) plus the final frame
            first = differing[0] if differing else count // 2
            frames = sorted({first, count - 1})
            have_base = extract_frames(base_video, frames, os.path.join(images, f"{name}-base.png"))
            have_head = extract_frames(head_video, frames, os.path.join(images, f"{name}-head.png"))
            have_diff = changed and have_base and have_head and diff_image(
                os.path.join(images, f"{name}-base.png"), os.path.join(images, f"{name}-head.png"), os.path.join(images, f"{name}-diff.png"))
            caption = " and ".join(f"{n / FPS:.1f}s" for n in frames)
            header = f"| {args.base_label} | {args.head_label} |" + (" difference (×4) |" if have_diff else "")
            rows += [f"Frames at {caption}.", "", header, "|---|---|" + ("---|" if have_diff else ""),
                     f'| <img src="{{BASE_URL}}/{name}-base.png" width="300"> | <img src="{{BASE_URL}}/{name}-head.png" width="300"> |'
                     + (f' <img src="{{BASE_URL}}/{name}-diff.png" width="300"> |' if have_diff else "")]
        else:
            changed = True
            status = "❌ " + " and ".join(label for label, ok in ((args.base_label, base_ok), (args.head_label, head_ok)) if not ok) + " failed to render"
            for label, ok, log in ((args.base_label, base_ok, base_log), (args.head_label, head_ok, head_log)):
                if not ok:
                    rows += [f"**{label}** output:", "", "```", tail(log), "```", ""]
            for label, ok, video, side in ((args.base_label, base_ok, base_video, "base"), (args.head_label, head_ok, head_video, "head")):
                if ok:
                    count = len(results[side][3])
                    if extract_frames(video, [count - 1], os.path.join(images, f"{name}-{side}.png")):
                        rows += [f"**{label}** final frame:", "", f'<img src="{{BASE_URL}}/{name}-{side}.png" width="300">', ""]

        summary.append(f"| `{name}` | {description} | {status} |")
        command = " ".join(["chatrender", "-i", f"<{fixture}>" if isinstance(source, list) else fixture, *COMMON_ARGS, *extra])
        details += ["", f"<details><summary>{status.split(' ')[0]} <b>{name}</b>: {description}</summary>", "",
                    *rows, "", f"`{command}`", "", "</details>"]

    comment = ["## Chat render preview", "",
               f"Sample chats rendered by **{args.base_label}** and **{args.head_label}** from the same input. "
               "Expand a case to compare frames side by side.", "",
               "| Case | Options | Result |", "|---|---|---|", *summary, *details, "",
               "<sub>Comment <code>/render-preview</code> to re-render.</sub>"]
    with open(os.path.join(args.out, "comment.md"), "w", encoding="utf-8") as f:
        f.write("\n".join(comment) + "\n")


if __name__ == "__main__":
    main()
