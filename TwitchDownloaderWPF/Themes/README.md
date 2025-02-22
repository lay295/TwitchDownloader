This app supports user-created themes!

To get started, simply duplicate one of the included themes and open it with your favorite text editor.

HandyControl elements do not provide full theming support, but they include a dark variant, hence the dedicated Boolean keys.

The Boolean keys control:

- Title bar theme
- HandyControl element theme
- Etc.

The SolidColorBrush keys control color properties:

- App background
- Text color
- Border color
- Etc.

The Inner keys are used to add visual depth to double-recursive elements.
The following diagram illustrates the hierarchy:

```Hierarchy diagram
+----------------------------[-][#][x]-+
|             AppBackground            |
| +----------------------------------+ |
| |       AppElementBackground       | |
| | +------------------------------+ | |
| | |   AppInnerElementBackground  | | |
| | +----------------------------- + | |
| +----------------------------------+ |
+--------------------------------------+
```  

Usage examples:

- `AppElementBackground` is used by frame elements
- `AppInnerElementBackground` is used by bordered labels, blank image backgrounds, etc.

If you create a theme and want it included in official releases, submit a GitHub pull request:
<https://github.com/lay295/TwitchDownloader/pulls>

Important Notes:  

1. `Dark.xaml` and `Light.xaml` will always be overwritten on application launch.
2. Filenames are case-insensitive (e.g., `Dark.xaml` ≡ `dark.xaml`).
3. Edit the author comment at the top of the theme file!
