# Journal.exe — fixed build notes (2026-10-27, second build)

Output: publish-win-x64/Journal.exe — 92,112,989 bytes (~88 MB), single-file,
self-contained, win-x64, PE32+ GUI x86-64, verified with `file`.
Built with ~/workspace/.dotnet/dotnet publish (see task brief for the exact
command). Assembly name Journal, so the output is Journal.exe.
NOT published to GitHub and no release created, per the parent's instruction.

## Bugs fixed in this build

1. Invisible text fields (Capture AND Settings, and by extension every
   window). Root cause: no Avalonia theme was ever loaded
   (App.Initialize was empty) and TextBoxes set only a white Background,
   so they rendered as invisible gaps. Fix, two layers:
   - App.Initialize now loads the Fluent theme (Styles.Add(new FluentTheme())).
   - New shared helper Ui.cs: Ui.TextBox() — explicit white background,
     #D1D5DB border, 1.5px, dark ink #1F2937 foreground, padding,
     minimum height that cannot collapse, visible watermark where used.
     It is now the ONLY place a TextBox is constructed (grep-verified);
     all 9 fields use it: Capture input (56px, "capture a thought…"),
     Settings token / database ID / capture shortcut / evening shortcut
     (40px each), Morning intention (90px) + why (56px), Evening
     "one change for tomorrow" (56px), Draft read-only draft (480px).
     TagsWindow has no text field, so nothing to fix there.
   - Capture height raised 230 -> 280; layout budget 248px, so nothing
     clips. Settings buttons pin Height 44 + VerticalAlignment Top so
     "Save settings" cannot stretch. Settings is inside a ScrollViewer,
     so overflow scrolls instead of clipping.
   - Light theme unchanged: #FDF8F0 / #1F2937 / teal #0F766E.

2. Hotkey error overwrote the Notion status. Fix: CaptureWindow now has
   three separate lines — save feedback, Notion status, hotkey status —
   so one can never hide another. HotkeyService reports per shortcut
   (CaptureError / EveningError, naming the shortcut, e.g. Capture
   Ctrl+Alt+Q vs Evening review Ctrl+Alt+J). A failed hotkey never
   blocks the app: it still works fully from the tray.
   Root-cause guard: named Mutex + named-pipe listener, so a second
   launched copy signals the first copy to come forward and exits,
   instead of stealing/failing the first copy's hotkeys. Settings
   re-registers shortcuts immediately via HotkeyService.Restart()
   (no app restart needed); banned Ctrl+Shift+Space, Ctrl+Shift+E,
   Ctrl+Shift+Esc are still rejected. Registration now waits for the
   hotkey thread to finish registering (was a blind 200 ms sleep).

3. "Notion not configured" ambiguity. Fix: capture status now says
   exactly which state applies — (a) "Notion: not set up — open
   Settings…" when no token/database is entered, (b) "Notion: test
   failed — <reason>" when a test failed, (c) "Notion: connected"
   only after a successful test, plus a distinct "token saved, not
   tested yet" state. Settings "Test Notion connection" now returns
   the real reason (401 invalid/shared, 403 access, 404 database ID,
   network/timeout) and stores the last test outcome, so the capture
   window can show it. The Settings button is in the capture window
   bottom row, so the fix is one tap away. Captures still save
   locally first in every case and are never lost over Notion.

## App / tray icon (new in this build)

Bold white J on teal #0F766E rounded square, generated as icon.png
(256x256) and icon.ico (16/32/48/64/128/256, so it stays readable at
16x16 tray size), embedded in the project, used for both the main
window icon and the tray icon (App.LoadIcon prefers the .ico, falls
back to the .png). No other app's logo is used.

## Testing actually done (Linux VM — cannot run Windows)

- dotnet publish succeeded (only pre-existing obsolete Checked/Unchecked
  warnings in TagsWindow); `file` confirms PE32+ GUI x86-64.
- Grep audit: only Ui.cs constructs a TextBox; 9 Ui.TextBox call sites
  listed above; no hardcoded token/database ID in source.
- Layout review (line-by-line, not a visual run): Capture height
  budget 248px < 280px window; every TextBox has explicit border,
  foreground, padding, MinHeight; Settings buttons pinned 44px.
- Headless screenshot was NOT run: the project targets net8.0-windows
  and this VM has no Windows runtime/display, and a headless harness
  would need a separate app lifetime and would not exercise Win32
  tray/hotkeys anyway, so it would not prove Windows rendering.
  The user must visually confirm on a real PC: every field shows a
  white box with a grey border, the capture placeholder is visible,
  the tray shows the J icon, and Settings fields are visible.

## Still untested on a real Windows PC

Exe launching, tray behaviour, global hotkeys, window rendering,
Notion live save, DPAPI token storage, update check, file attach.
Previously known limits unchanged: Ctrl+V paste and drag-and-drop
not implemented, "Start with Windows" not implemented.

---

# Journal.exe — test build notes (2026-10-27)

Output: publish-win-x64/Journal.exe — single-file, self-contained, win-x64.
Built on Linux with .NET 8 SDK 8.0.427 (local install in ~/workspace/.dotnet).
Avalonia 11.3 (floating 11.3.* resolved at restore time), target net8.0-windows.

What is included:
- Tray app, close hides to tray, tray menu: New capture / Evening review /
  Morning intention / Settings / Quit.
- Global hotkeys via Win32 RegisterHotKey: Ctrl+Alt+Q capture, Ctrl+Alt+J
  evening review (changeable in Settings; banned Ctrl+Shift+Space,
  Ctrl+Shift+E, Ctrl+Shift+Esc are rejected).
- Minimal capture dialog, light theme #FDF8F0 / #1F2937 / teal #0F766E,
  640x230, input + teal Add button, "+" for multi-select tags and files,
  Enter save / Shift+Enter newline / Esc dismiss, Alt+1..6 toggles tags.
- Local-first save: %AppData%\QuickCaptureJournaling\captures.json,
  evening.json, morning.json, settings.json, attachments\.
- Morning Intention window, Evening Review window (yesterday's change
  Yes/No first, six sections grouped from today's captures, one change
  for tomorrow, Show full draft, Save to Notion after approval).
- Settings: Notion token + database ID entry, shortcut change, update
  check on/off. Token stored with Windows DPAPI only, never plain text.
  If Notion is not configured, captures still save locally with a clear
  "Notion not configured" status.

NOT tested on Windows (cannot run a Windows exe on this Linux VM):
tray behaviour, global hotkeys, window layout/rendering, Notion live save,
DPAPI token storage, update check, file attach/paste, and the .exe actually
launching on a real PC. The user must test all of these on their PC.

Known limitations / stubs in this test build:
- Ctrl+V paste of files is not implemented (attach via "+" file picker,
  and drag-and-drop was not wired in the code-only UI).
- "Start with Windows" is not implemented (Settings does not include it
  in this build; Hide to tray is the close behaviour).
- New global shortcuts take effect after an app restart (Settings says so).
- Tags dialog closes when a file is attached; tags chosen before that
  are kept, but tags cannot be added after attaching in the same dialog
  open (reopen "+" to add more).
- Update check only works if the GitHub repo/releases are public; the
  Journal-Windows repo is currently private, so the check will silently
  do nothing until releases are public.
- No GitHub release was created and nothing was pushed to GitHub from
  this build, per the brief. No Notion token or database ID is in the
  source or the exe.
