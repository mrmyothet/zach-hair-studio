---
phase: 260809-gpw
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - dashboard/lib/adminChat.ts
  - dashboard/lib/adminChat.test.mjs
autonomous: true
requirements: [QUICK-260809-gpw]

must_haves:
  truths:
    - "semgrep scan --config=auto --error reports 0 findings on dashboard/lib/adminChat.ts and dashboard/lib/adminChat.test.mjs (was 2 blocking findings, matching the CI security/sast job failure on PR #39)."
    - "resolveDate's named-weekday matching behavior is unchanged — WEEKDAYS.findIndex still returns the correct index for a weekday word present in the input text."
  artifacts:
    - "dashboard/lib/adminChat.ts — no `new RegExp(` call built from a variable"
    - "dashboard/lib/adminChat.test.mjs — mirrors adminChat.ts's fix (file's own header comment requires the two stay in lockstep)"
  key_links:
    - "adminChat.ts resolveDate -> adminChat.test.mjs's copy of resolveDate — the test file is a hand-maintained mirror, not generated; changing one without the other reintroduces drift."
---

<objective>
Clear the `security / sast (semgrep)` CI failure on PR #39: `javascript.lang.security.audit.detect-non-literal-regexp` fired twice (source + its hand-mirrored test file) because `resolveDate` built a `RegExp` from a loop variable. `day` only ever comes from the fixed `WEEKDAYS` const, so this isn't exploitable today — but semgrep can't prove that statically, and the safer fix is also simpler: don't construct a `RegExp` per candidate at all.
</objective>

<context>
@dashboard/lib/adminChat.ts
@dashboard/lib/adminChat.test.mjs
</context>

<tasks>

<task type="auto">
  <name>Task 1: Replace per-weekday RegExp construction with a word-split membership check</name>
  <files>dashboard/lib/adminChat.ts, dashboard/lib/adminChat.test.mjs</files>
  <action>
In both files, replace `WEEKDAYS.findIndex((day) => new RegExp(`\\b${day}\\b`).test(t))` with a plain word-split + `Array.includes` check: split `t` on `/\W+/` into `words`, then `WEEKDAYS.findIndex((day) => words.includes(day))`. This removes the only non-literal `RegExp()` call semgrep flagged, in both the source and its hand-maintained mirror (adminChat.test.mjs's header comment: "if you change the regexes ... here, change them here too").
  </action>
  <verify>
    <automated>node dashboard/lib/adminChat.test.mjs  # expect "all assertions passed"; then: semgrep scan --config=auto --error --skip-unknown-extensions dashboard/lib/adminChat.ts dashboard/lib/adminChat.test.mjs  # expect 0 findings</automated>
  </verify>
  <done>Both files use the word-split check instead of `new RegExp(...)`; the self-check test passes; semgrep reports 0 findings on both files.</done>
</task>

</tasks>

<verification>
1. `node dashboard/lib/adminChat.test.mjs` — "adminChat self-check: all assertions passed".
2. `semgrep scan --config=auto --error --skip-unknown-extensions dashboard/lib/adminChat.ts dashboard/lib/adminChat.test.mjs` — 0 findings (was 2 blocking).
</verification>

<success_criteria>
- No `new RegExp(` call remains in either file.
- Weekday-name date resolution behaves identically (self-check test passes).
- semgrep's `sast` job would pass locally against these two files.
</success_criteria>

<output>
Create `.planning/quick/260809-gpw-fix-semgrep-sast-finding-detect-non-lite/260809-gpw-SUMMARY.md` when done.
</output>
