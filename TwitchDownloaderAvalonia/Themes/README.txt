Custom theme packs
==================

Place JSON files in this folder (next to the Twitch Downloader executable).
New packs appear in Settings under Light themes or Dark themes
(from the pack's isDark value). Reopen Settings after adding a file.

Do not name a pack System, Light, or Dark — those names are reserved.

Schema
------
{
  "author": "Your name",
  "isDark": true,
  "colors": {
    "WindowBackgroundBrush": "#1C1C21",
    "AccentBrush": "#9146FF"
  }
}

- isDark is required. Use true for dark control chrome, false for light.
- Colors are #RRGGBB or #AARRGGBB.
- Missing colors fall back to the built-in Light or Dark palette (from isDark).
- Unknown color keys are ignored.

WPF Themes/*.xaml packs are not loaded. Convert colors into this JSON format.

Samples
-------
Copy "Light Pink.json" or "Dark Pink.json",
rename the file, edit colors, then select it under Light themes or Dark themes.
