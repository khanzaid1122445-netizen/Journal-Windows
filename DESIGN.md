# QuickCapture Journaling — Windows

Windows version of the journaling quick-capture app. No app code yet — this
commit records the agreed design, so the build can start from it.

## Purpose

Do not waste past experiences; use them to make tomorrow better.
Journaling should help prevent mistakes from repeating.

## Journaling structure

Six sections, in order: Events, Grateful / Went Well, Improvement Actions,
Sins, Obstacles, TIL. Sins and mistakes are distinct categories.
About 5 minutes per evening entry. Review the previous entry first,
especially whether its one improvement action worked. Carry forward only
one unresolved improvement action. End with exactly one concrete change
for tomorrow. A pattern is named only when it appears in at least two
entries. A full draft is shown and saved to Notion only after approval;
empty sections stay empty.

## Screens (Figma: QuickCapture Journaling)

- Windows Capture Popup — minimal dialog, see windows-capture-minimal.md
- Windows Morning Intention — optional, one thought for the day ahead
- Windows Evening Review — about 5 minutes, yesterday's change first,
  then the six sections, one change for tomorrow, draft approval, then
  save to Notion
- Settings — Notion connection, shortcuts, updates, app options

## Shortcuts

- Ctrl+Alt+Q: open capture (Q for QuickCapture)
- Ctrl+Alt+J: open evening review (J for Journal)
- Settings must allow changing these keys later, with a warning if a
  new key is already used by another app.
- Do NOT use Ctrl+Shift+Space or Ctrl+Shift+E — the user reported both
  already reserved on their PC. Ctrl+Shift+Esc is also reserved
  (Windows Task Manager).

## Notion

- New database "QuickCapture Journaling" (created 2026-10-27, starts empty).
  The old "Entries-Journaling" database stays untouched as an archive.
- Types: Capture (raw daytime note, many per day, Status Pending, then
  Reviewed), Evening Entry (one structured entry per day, Status Saved,
  carries Tomorrow's Action), Morning Intention (optional, one per day).
- Tags are multi-select: Event, Grateful, Improvement, Sin, Obstacle, TIL.
  One capture can carry several tags and appears under each selected
  section in the evening review.
- Notion token is stored in secure platform storage, never in plain
  config.json. No token or database ID is stored in this repo.

## Updates and output

- Updates via GitHub Releases: check on startup, stable channel only,
  ask first before download and install, verify the download hash.
- Output for a quick test: single self-contained .exe.
- Output for version 1 with auto-update: Velopack Setup, using GitHub
  Releases as its feed. Microsoft Store / MSIX is a later step, not v1.
