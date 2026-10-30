## Version 1.0.3 Velopack edition — Velopack-integrated 1.0.3 build only, NO Setup.exe yet (2026-10-30) — NOT published to GitHub, repo visibility left Public, existing public v1.0.3 single-exe release NOT deleted

Output: publish-win-x64/Journal.exe — 92,695,460 bytes (~89 MB),
single-file, self-contained, win-x64, PE32+ GUI x86-64, 10 sections,
verified with `file`. Version string: 1.0.3 (App.Version in App.cs,
matching <Version> in Journal.csproj, shown in Settings). Also copied
as publish-win-x64/Journal-1.0.3-velopack.exe (same build, SHA-256
12d670b2216d8f161ccb92fd5b06c02616ad446468cccb3b500c8279a284eedc).
This is the 1.1.0 Velopack codebase with ONLY the version rolled back
to 1.0.3 — Velopack 0.0.942, VelopackUpdate.cs, Program.cs VelopackApp
handling, App.cs Velopack-first update check, Settings feed URL and
Ask-first checkboxes are all kept. Feature set is the 1.0.3 set:
PNG pasted screenshots, thumbnail chips with click preview, custom-tag
sync, icon buttons, arrow-only save, file chips, voice recording,
drag-and-drop, clipboard paste, Notion file_uploads, clean names,
no hotkey details on dialog. The plain single-file
publish-win-x64/Journal-1.0.3.exe (92,186,717 bytes, SHA-256
afeae453af79f89759204ea0844bcc6eea33436779eef199c90f0b76fdf64a26)
was left intact and was NOT overwritten. No Windows Setup.exe was
produced on this Linux VM (vpk on Linux packs Linux AppImage only);
use velopack-output/pack-windows.bat on a Windows PC with
--packId JournalQuickCapture --packVersion 1.0.3. Repo visibility was
NOT changed (already Public) and the existing public v1.0.3 release
was NOT deleted. Future updates need a version higher than 1.0.3.
Build success only — install, shortcut, uninstall, update check,
and settings-kept behaviour still need a real Windows PC test.

## Version 1.1.0 — Velopack auto update, integrated build only, NO Setup.exe yet (2026-10-30) — NOT published to GitHub, NO repo made public

Output: publish-win-x64/Journal.exe — ~89 MB, single-file,
self-contained, win-x64, PE32+ GUI x86-64, 10 sections,
verified with `file`. Version string: 1.1.0 (App.Version in
App.cs, matching <Version> in Journal.csproj, shown in
Settings). Also copied as publish-win-x64/Journal-1.1.0.exe
(same build). Velopack library 0.0.942 (Journal.csproj).
NOT published to GitHub, no release created, no repo made
public, per instruction. Parent handles delivery; the user
has NOT yet chosen a public feed location.

WHAT WAS BUILT (VelopackUpdate.cs, Program.cs, App.cs,
Models.cs, SettingsWindow.cs):
- VelopackApp.Build().SetAutoApplyOnStartup(false).Run() at
  the start of Program.Main, in try/catch so a non-Velopack
  run never fails to start. AutoApplyOnStartup=false is
  deliberate: no silent install.
- VelopackUpdate.CreateManager: if the feed URL in settings
  looks like github.com/owner/repo (two path segments),
  GithubSource(feed, "", false) — token empty, prerelease
  false, i.e. stable only, never a secret. Otherwise the URL
  is passed to UpdateManager as a plain HTTP Velopack feed
  folder (RELEASES file). Default feed in settings:
  https://github.com/khanzaid1122445-netizen/Journal-Windows
  (configurable in Settings, never hard-coded with a secret).
- App.CheckUpdatesAsync: if VelopackUpdate.IsInstalled is
  true, run CheckAndOfferAsync; otherwise fall back to the
  old UpdateChecker GitHub latest-release check, which only
  opens the release page. Velopack 0.0.942 has no
  NotInstalledException type, so IsInstalled is the gate.
  Any Velopack failure is quiet — an update check never
  breaks startup, especially while the feed is private.
- CheckAndOfferAsync, ask-first at both steps, matching
  Settings (CheckForUpdates, UpdateDownloadAskFirst,
  UpdateInstallAskFirst, all default On/true):
  CheckForUpdatesAsync -> ask "Download update?" ->
  DownloadUpdatesAsync -> ask "Install and restart?" ->
  ApplyUpdatesAndRestart(TargetFullRelease). If either ask
  is declined, nothing is downloaded/installed.
- Settings: update checkbox relabelled to Velopack/stable/
  ask-first, plus a feed URL textbox and separate
  Ask-before-download / Ask-before-install checkboxes,
  saved to settings.json alongside the existing fields.

BLOCKER — NO REAL WINDOWS Setup.exe WAS PRODUCED:
vpk 0.0.942 was installed on this Linux VM
(DOTNET_ROOT=~/workspace/.dotnet, tool at
~/.dotnet-tools-home/.dotnet/tools/vpk), but `vpk pack
--help` on Linux says "Create a Linux .AppImage bundle"
and offers no Windows target flag. Packing win-x64 files
on Linux would produce the wrong artifact, so no Setup.exe
was faked and vpk pack was NOT run on the published files.
A real JournalQuickCapture-Setup.exe can only be packed
on a Windows PC. Exact command, script, and upload steps
are in velopack-output/README-VELOPACK-WINDOWS.md and
velopack-output/pack-windows.bat:
  vpk pack --packId JournalQuickCapture --packVersion 1.1.0
    --packDir publish-win-x64 --mainExe Journal.exe
    --packTitle "Journal - Quick Capture" --packAuthors
    "Journal" --icon icon.ico --outputDir velopack-output
Expected Windows output: JournalQuickCapture-Setup.exe,
JournalQuickCapture-1.1.0-full.nupkg (+ deltas), RELEASES.

FEED DECISION STILL NEEDED FROM THE USER (auto update is
INACTIVE until this is done):
Option A: make Journal-Windows public (no secret is in this
code; token lives only in DPAPI settings on the PC), then
`vpk upload github --repoUrl https://github.com/
khanzaid1122445-netizen/Journal-Windows --publish
--releaseName "Journal 1.1.0"`.
Option B: host the velopack-output files publicly elsewhere
and set Settings > Update feed to that folder URL.
Until one is done, the installed app's check fails quietly
(private feed cannot be downloaded anonymously) and the app
continues normally, never downloading/installing anything.

SETTINGS PERSISTENCE ON SWITCHING TO VELOPACK:
Unchanged. Both single-file and Velopack-installed apps
read/write %AppData%\QuickCaptureJournaling\settings.json
(DPAPI token, database ID, shortcuts, feed, ask-first
flags) and the same captures/evening/morning/attachments
files. Installing 1.1.0 keeps token/database/shortcuts;
nothing needs re-entering. Do not move that folder.

All 1.0.3 features kept. No token was added, searched for,
or used; grep found no token-style string in .cs/.csproj;
grep of CaptureWindow.cs for "could not be registered"
returned 0. Build success only — NOT tested on a real
Windows PC: install, Start Menu shortcut, uninstall,
update check, update offer, download, install and restart,
and a public feed all still need a real-PC test, one item
at a time. Do NOT claim auto update works until that test
and a public feed are done.

## Version 1.0.3 — pasted screenshots to PNG + thumbnail / click preview fix (2026-10-29) — NOT published to GitHub

Output: publish-win-x64/Journal.exe — 92,186,717 bytes (~88 MB),
single-file, self-contained, win-x64, PE32+ GUI x86-64,
10 sections, verified with `file`. SHA-256 differs from 1.0.2
(the identical byte size is publish padding). Version string:
1.0.3 (App.Version in App.cs, matching <Version> in
Journal.csproj, shown in Settings). Also copied as
publish-win-x64/Journal-1.0.3.exe (same build).
NOT published to GitHub and no release created, per instruction.

BUG FIXED (ClipboardFiles.cs WritePng, replacing WriteBmp):
v1.0.2 wrote clipboard DIB/DIBV5 data out as a .bmp file
(pasted-image.bmp). That BMP — especially a DIBV5 screenshot
with a 124-byte header — did not reliably decode into an
Avalonia Bitmap, so the file chip fell back to a generic icon
and showed no photo thumbnail, while the other app (which
encodes the same paste as PNG) did show one. v1.0.3 decodes
the DIB pixels itself (24/32bpp, BI_RGB and BI_BITFIELDS,
bottom-up and top-down, DIB and DIBV5 headers) and writes a
dependency-free standard PNG (8-bit RGB, filter 0,
zlib-compressed, CRC32) named pasted-image.png (_1, _2 on
collision). Alpha is dropped on purpose: 32bpp clipboard
bitmaps often carry alpha 0 for opaque pixels, which would
make a PNG thumbnail look blank. Pasted PNG files from
Explorer keep their own .png name, unchanged. The PNG
encoder was tested on Linux in isolation with crafted
24bpp bottom-up and 32bpp DIBV5 top-down DIBs: PNG chunk
CRCs validated and pixel values matched exactly. Notion
upload now also sends image/png for pasted screenshots.
Old .bmp files from earlier builds are not converted
retroactively; their chips still try to load and fall back
to an icon if they cannot decode.

CLICK PREVIEW AUDIT (CaptureWindow.cs RefreshFileChips):
the chip button already opened PreviewWindow on Click, but
its thumbnail Image and name TextBlock could swallow the
click. Both are now IsHitTestVisible=false, so a click on
the thumbnail OR the name reaches the chip button and opens
the full preview (images in PreviewWindow, voice Play/Stop,
others via default app), while only the separate red ×
removes the file.

All 1.0.2 features kept. No token was added, searched for,
or used; token handling is untouched (DPAPI settings only).
Build success only — NOT tested on a real Windows PC.

## Version 1.0.2 — thumbnail file chips + clean file names (2026-10-27) — NOT published to GitHub

Output: publish-win-x64/Journal.exe — 88 MB, single-file,
self-contained, win-x64, PE32+ GUI x86-64, 10 sections, verified
with `file`. Version string: 1.0.2 (App.Version in App.cs,
matching <Version> in Journal.csproj, shown in Settings).
Also copied as publish-win-x64/Journal-1.0.2.exe (same build).
NOT published to GitHub and no release created, per instruction.

1. THUMBNAIL FILE CHIPS (CaptureWindow.cs RefreshFileChips):
Image attachments (png/jpg/jpeg/bmp/gif/webp) show a 36x36
thumbnail on the left of the chip, then the clean file name,
then a red ×. Clicking thumbnail or name still opens the full
PreviewWindow; × only removes. Non-image files keep a 📎 icon +
name chip, no thumbnail. Voice notes keep Play/Stop preview.
Chip height is 40px so the dialog stays compact.

2. CLEAN FILE NAMES (Storage.cs, ClipboardFiles.cs,
CaptureWindow.cs, NotionClient.cs): root cause was
Storage.CopyAttachment prefixing every file with a GUID, and
that GUID name was what Notion received. CopyAttachment now
uses the original clean name, appending _1, _2 only on a
collision in the attachments folder, never a GUID. Voice notes
are created as voice_yyyy-MM-dd_HH-mm.wav, clipboard
screenshots as pasted-image.bmp (BMP data keeps .bmp; a pasted
PNG file keeps its .png name). NotionClient.UploadFileAsync
and the upload-failure paragraph also strip any old GUID
prefix via Storage.CleanDisplayName, so new captures upload
as e.g. adv-06-2026.pdf. Applies to new captures only; old
Notion pages are not renamed.

All 1.0.1 features kept. No token was added, searched for, or
used; token handling is untouched (DPAPI settings only).
Build success only — NOT tested on a real Windows PC.

## Version 1.0.1 — clipboard paste fix + true Notion file upload (2026-10-27) — NOT published to GitHub

Output: publish-win-x64/Journal.exe — 92,186,717 bytes (~88 MB),
single-file, self-contained, win-x64, PE32+ GUI x86-64, 10 sections,
verified with `file`. Version string: 1.0.1 (App.Version in App.cs,
matching <Version> in Journal.csproj, shown in Settings).
Also copied as publish-win-x64/Journal-1.0.1.exe (same build).
NOT published to GitHub and no release created, per instruction.

FIX 1 — Ctrl+V did nothing (ClipboardFiles.cs, CaptureWindow.cs):
Root causes found: (a) the old paste path was deferred via
Task.Run + Dispatcher and routed through Window.OnKeyDown, which
the focused TextBox's own Ctrl+V handling can swallow first;
(b) CF_DIB handling rejected DIBV5 screenshots (124-byte header),
which is what many screenshot tools put on the clipboard;
(c) OpenClipboard was tried once only and failed silently if
another app briefly held the clipboard. Fix: paste is checked
SYNCHRONOUSLY on the UI thread in a window-level KeyDown handler
registered with handledEventsToo, so it runs even when the
TextBox handled Ctrl+V first. If the clipboard holds files/images
they are attached and the key is swallowed; if it holds text only
the key is left alone so the TextBox pastes text normally.
ClipboardFiles now also accepts CF_DIBV5, retries OpenClipboard
5x, uses a 1024-char DragQueryFile buffer, and
HasFileOrImage() distinguishes "no files" from "paste failed"
(brief status line only, no permanent detail line). A visible
"📋 Paste" button was added as a guaranteed paste path in case
key routing is still swallowed on some PC. Explorer files
(CF_HDROP) and copied images (CF_DIB/CF_DIBV5 -> .bmp) both
become the same removable file chips as picker/drag-drop.

FIX 2 — True Notion file upload (NotionClient.cs, CaptureWindow.cs):
New CreateCapturePageWithUploadsAsync + UploadFileAsync implement
Notion /v1/file_uploads: POST file_uploads {mode, filename,
content_type} -> id (+upload_url), POST send multipart "file"
(+ part_number for multi_part), POST complete (multi_part only),
GET file_uploads/{id} poll until "uploaded"/"failed". Files
<= 20 MB use single_part; larger files are split into 10 MB
parts, sent as multi_part, then completed. Uploaded files are
attached as REAL blocks in the existing order: images first as
image blocks, then other files (including .wav voice notes) as
file blocks, then the note text paragraph. CaptureWindow.SaveAsync
now uses this path and resolves bare attachment filenames via
the local attachments folder (Storage.CopyAttachment returns a
filename, not a full path). If a file upload fails, the page is
still created, the failed file is named in a paragraph, the
local copy is kept, and the status reads "Saved to Notion, file
upload failed — files kept locally" (page failure still reads
"Saved locally"). Text-only captures and Evening Entry section
order are unchanged; the old naming-only CreatePageAsync path is
kept for Evening/Morning, which pass no attachments.
No token was searched for, embedded, or committed; grep found no
token-style string in .cs/.csproj; token stays DPAPI-protected
in settings.json, read at runtime, settings persistence kept.
Still untested on a real Windows PC (Linux VM cannot run a
Windows exe): Ctrl+V from Explorer, Ctrl+V from a screenshot,
voice .wav upload to Notion, image/file order on the Notion
page, and whether Notion accepts audio/wav uploads — if a
specific type is rejected by Notion, that file falls back to
the filename paragraph + "file upload failed" status, never a
lost capture.

---

## Custom tags + attachments + voice + Settings version build (2026-10-27) — NOT published to GitHub

Output: publish-win-x64/Journal.exe — 92,166,237 bytes (~88 MB),
single-file, self-contained, win-x64, PE32+ GUI x86-64, 10 sections,
verified with `file`. Version string: 1.0.0 (App.Version in App.cs,
matching <Version> in Journal.csproj). Built with
~/workspace/.dotnet/dotnet publish, same command as before.
NOT published to GitHub and no release created, per instruction.

- Version in Settings: SettingsWindow.cs adds a clearly visible
  "Version 1.0.0" line (teal, semi-bold, 14px) directly under the
  Settings title, from App.Version.
- Custom tags, map to a section: CaptureWindow.cs fetches Notion Tags
  options on dialog open and on the "↻ Tags" button via
  NotionClient.GetDatabaseTagOptionsAsync (GET databases/{id}); chips
  are rebuilt from base six + fetched options. New tag row: name +
  section dropdown (six base sections) + Add tag; mapping saved
  locally in TagStore.cs (%AppData%\QuickCaptureJournaling\tag-mappings.json,
  never in code). Add also PATCHes the database via
  NotionClient.PatchDatabaseAddTagAsync; if PATCH fails, page creation
  with the tag is the fallback path that creates the option in Notion.
  EveningWindow.cs groups a capture under its custom tag's mapped
  section and under any base tags it carries; unmapped tags arriving
  from Notion get a prompt row in Evening Review (tag + section
  dropdown + Save mapping) and are listed, never lost.
- Three icon buttons on the dialog: 🌙 Evening review, 🌅 Morning
  intention, ⚙ Settings, with tooltips, opening the same windows as
  the tray menu (CaptureWindow.cs IconButton / App.ShowEvening etc.).
- Save button arrow only: teal button content is "➤", no "Add" word.
- Multi-file picker + removable chips: + opens the picker with
  AllowMultiple=true; each file shows as a chip (name + ×) in a
  WrapPanel; × removes only that file from this capture.
- Notion page order for captures (NotionClient.cs, sections==null
  path only; Evening Entry section order unchanged): images first
  (🖼 filename paragraphs), then other files (📎 filename
  paragraphs), then the note text paragraph. LIMITATION: binary
  upload via /v1/file_uploads is NOT implemented — attachments are
  named in order on the page and kept locally in the attachments
  folder, as in previous builds. The order itself is implemented.
- Drag-and-drop + clipboard paste: DragDrop.AllowDrop + DropEvent
  (DragEventArgs.Data, obsolete warning only) attaches dropped files
  as chips. Ctrl+V paste uses Win32 ClipboardFiles.cs (CF_HDROP files
  and CF_DIB images -> .bmp), because Avalonia 11.3's IClipboard has
  no GetFilesAsync (verified by compile error, then removed). Pasted
  images become file chips the same way.
- Voice recording: 🎙 Record button (CaptureWindow.cs) + VoiceRecorder.cs
  (Win32 MCI winmm.dll, no NuGet package, .wav output). Timer shows
  ● mm:ss on the dialog while recording; tapping again stops and
  attaches the .wav as a file chip; it is a non-image file in the
  Notion order above. Recording stops on save/close/Esc.
- Preview file chips: clicking a chip's name (not ×) opens
  PreviewWindow.cs — images in-app large, .wav with Play/Stop (MCI),
  other audio/other files via the system default app. × only removes.
- Kept: minimal light theme (#FDF8F0 / #1F2937 / teal #0F766E,
  forced light), J icon, tray, Ctrl+Alt+Q/J, tags on dialog, NO
  hotkey error text on the dialog (grep count 0 in CaptureWindow.cs
  for "could not be registered"/HotkeyStatus/Notion:), hotkey
  status/fix in Settings only, local-first save, Morning, Evening,
  Settings persistence (new exe reads existing settings.json, does
  not reset it). No token was searched for, embedded, or committed;
  grep found no token-style value in .cs/.csproj; token is still
  only DPAPI-protected settings read at runtime.
- Still untested on a real Windows PC (Linux VM cannot run a Windows
  exe): dialog rendering, chip taps, custom-tag fetch/PATCH, drag-drop,
  clipboard paste (Win32 clipboard needs a real Windows session),
  microphone recording + timer, preview, Notion live save, DPAPI.
  The user must test all of these on their PC, especially recording
  (needs microphone permission) and paste (needs a Windows clipboard).

---

## Minimal capture dialog build (2026-10-27) — NOT published to GitHub

Output: publish-win-x64/Journal.exe — 92,141,661 bytes (~88 MB),
single-file, self-contained, win-x64, PE32+ GUI x86-64, verified with `file`.
Built with ~/workspace/.dotnet/dotnet publish, same command as before.
Assembly name Journal, so the output is Journal.exe. NOT published to
GitHub and no release created, per the parent's instruction.

Changes, all in CaptureWindow.cs unless stated:
- Minimal dialog: 640x210 (was 640x280). Layout: heading + hint,
  one 56px input row with teal Add button, one 30px tag chip row, one
  40px bottom row (+ for files, Settings, brief save result). Content
  budget: margins 24 + head ~22 + input 56 + chips 30 + bottom 40 +
  spacing 24 = ~196px < 210px window, so nothing clips.
- Tags visible on the dialog: six chips built from TagsWindow.AllTags
  (Event, Grateful, Improvement, Sin, Obstacle, TIL), tapping toggles,
  selected = teal background/white text, unselected = white/grey border.
  Alt+1..6 still toggles the same tags. Captures keep their tag list,
  so Evening Review still groups a capture under each selected section.
  The + button now opens the file picker directly (files only); the
  separate TagsWindow is no longer used by Capture, but is kept in the
  project (it is still a valid window class; no other window uses it).
- Red hotkey details removed from the dialog: the _notionStatus and
  _hotkeyStatus TextBlocks, HotkeyStatusText(), NotionStatusText() and
  RefreshStatuses() were deleted from CaptureWindow.cs. Grep-verified:
  no "HotkeyStatus", "NotionStatus", "could not be registered" or
  "Notion:" text remains in CaptureWindow.cs. The dialog now shows
  only a brief save result after saving ("Saved to Notion",
  "Saved locally", or "Nothing to save…"); attachment count appears
  only while files are attached. Hotkey failure reporting stays in
  Settings ONLY: SettingsWindow still shows per-shortcut failure
  (CaptureError / EveningError), allows changing Ctrl+Alt+Q / Ctrl+Alt+J,
  and mentions tray fallback there. HotkeyService itself is unchanged.
- Settings persistence verified, no code change needed: startup
  (App.OnFrameworkInitializationCompleted) only calls
  Storage.LoadSettings() and never saves defaults. Every SaveSettings
  call in the codebase first loads the existing settings file and
  modifies only its own fields, so a new exe replacing the old one
  keeps the existing %AppData%\QuickCaptureJournaling\settings.json
  token (DPAPI-encrypted), database ID and shortcuts. The user does
  not need to re-enter them after an update. No credential is
  hard-coded to achieve this.
- Security: NO token value was searched for, copied, embedded, or
  committed in this build. Grep of all .cs/.csproj files found no
  token-style value and no hard-coded encrypted token. The token is
  still only read at runtime via SecureToken.Unprotect() from the
  DPAPI-protected settings file, exactly as before, and is only
  written from SettingsWindow when the user types it there.
- Unchanged otherwise: forced light theme, J icon (window, tray, and
  exe file via <ApplicationIcon>icon.ico</ApplicationIcon>), tray,
  Ctrl+Alt+Q/J, local-first save, Morning, Evening, Settings.
- First publish attempt failed on WrapPanel.HorizontalSpacing (not a
  property in Avalonia 11.3); chips were changed to a horizontal
  StackPanel with Spacing 8, which fits all six chips in one row
  (approx. 500px < 600px inner width). Only pre-existing obsolete
  Checked/Unchecked warnings in TagsWindow remain.

Still untested on a real Windows PC (Linux VM cannot run a Windows
exe, and no headless screenshot was run): dialog rendering, chip
tap behaviour, file picker from +, hotkeys, tray, Notion live save,
DPAPI storage. The user must visually confirm on a PC that the
dialog is short, all six chips are visible on it, and no hotkey
error text appears on it.

---

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

## Fix 2 (2026-10-27) — theme consistency + Explorer file icon
- Theme: App.Initialize now forces RequestedThemeVariant = ThemeVariant.Light,
  regardless of Windows dark mode. Previously Fluent followed dark mode while
  windows hard-coded light paper, so labels/buttons rendered white-on-paper
  (invisible) and a focused TextBox went dark while its explicit foreground
  stayed dark ink (black while typing).
- Global light styles in App.Initialize cover every TextBlock, Button,
  CheckBox and TextBox (ink foreground, white background, grey border,
  teal-tint selection); local values (teal buttons, status colours) win.
- Ui.TextBox now also sets SelectionBrush/SelectionForegroundBrush and
  re-asserts white background + ink foreground + teal focus border on
  GotFocus, grey border on LostFocus, so typing can never turn a box black.
- Explicit ink foreground on all Settings labels, the Test Notion
  connection button (white background, ink text, grey border), the update
  checkbox, and the Capture "+" and "Settings" buttons.
- Explorer icon: Journal.csproj now has <ApplicationIcon>icon.ico</ApplicationIcon>
  (icon.ico kept as EmbeddedResource too, for window/tray). Previously the
  exe file in Explorer had no J icon, only the window/tray did.
- Notion logic unchanged: the reported 401 in the screenshot is expected
  when the database ID field contains a placeholder like "sQQ" and the
  token box is empty.
