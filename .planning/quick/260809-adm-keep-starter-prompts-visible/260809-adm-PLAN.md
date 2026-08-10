---
phase: 260809-adm
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - dashboard/components/AdminChatWidget.tsx
autonomous: true
requirements: [QUICK-260809-adm]

must_haves:
  truths:
    - "STARTER_PROMPTS quick-reply buttons remain visible after the first chat message is sent, not just in the empty state."
    - "`npx tsc --noEmit -p .` (run from dashboard/) reports no errors."
  artifacts:
    - "dashboard/components/AdminChatWidget.tsx — starter-prompt button row rendered unconditionally (own row above the input form), not nested inside `{messages.length === 0 && ...}`"
  key_links:
    - "AdminChatWidget.tsx renders dashboard/lib/adminChat.ts's STARTER_PROMPTS constant — button labels must keep matching that source array."
---

<objective>
Staff reported that the AdminChat widget's common-question quick-reply buttons ("Who's booked today?", "List services and prices", etc.) vanish permanently after the first message is sent, because they were rendered only inside the `messages.length === 0` empty-state block. Move them to a persistent row so staff can reuse them throughout the conversation.
</objective>

<context>
@dashboard/components/AdminChatWidget.tsx
@dashboard/lib/adminChat.ts
</context>

<tasks>

<task type="auto">
  <name>Task 1: Make the starter-prompt row persistent</name>
  <files>dashboard/components/AdminChatWidget.tsx</files>
  <action>
Split the empty-state block: keep only the placeholder paragraph ("Ask about the day's bookings...") conditional on `messages.length === 0`. Move the `STARTER_PROMPTS.map(...)` button row out of that conditional into its own always-rendered `<div className="border-t border-border px-4 py-2.5 flex flex-wrap gap-2">`, positioned between the scrolling message list and the input `<form>`. Add `disabled={isTyping}` to each button (matching the send button's existing disabled pattern) so a starter prompt can't be clicked while a reply is in flight.
  </action>
  <verify>
    <automated>cd dashboard && npx tsc --noEmit -p .  # expect no output / exit 0</automated>
  </verify>
  <done>Starter-prompt buttons render in their own row regardless of message count; disabled while isTyping; tsc reports no errors.</done>
</task>

</tasks>

<verification>
1. `npx tsc --noEmit -p .` (from `dashboard/`) — no errors.
2. Manual: buttons visible before AND after sending a message (visual check deferred to staff running `npm run dev -- -p 3001`).
</verification>

<success_criteria>
- No conditional in AdminChatWidget.tsx hides the starter-prompt row after the first message.
- Buttons disable while `isTyping`, consistent with the existing send-button UX.
- Type-check passes.
</success_criteria>

<output>
Create `.planning/quick/260809-adm-keep-starter-prompts-visible/260809-adm-SUMMARY.md` when done.
</output>
