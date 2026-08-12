---
phase: 260810-w7m
plan: 01
subsystem: full-stack
tags: [ai, chat, openai, hugging-face, dashboard]

provides:
  - authenticated multi-turn salon chat endpoint
  - bounded OpenAI tool-call loop over shared read-only salon operations
  - dashboard chat using one typed API call instead of keyword routing

affects: [api, mcp, dashboard-admin-chat]

tech-stack:
  added: [OpenAI 2.12.0]
  patterns:
    - "Thin IChatCompletionClient seam keeps provider calls deterministic in tests."
    - "SalonChatTools is the in-process implementation shared by the LLM agent and anonymous availability-only MCP wrapper."

key-files:
  created:
    - API/ZachHairStudio.Api/Features/Chat/HuggingFaceOptions.cs
    - API/ZachHairStudio.Api/Features/Chat/ChatContracts.cs
    - API/ZachHairStudio.Api/Features/Chat/SalonChatTools.cs
    - API/ZachHairStudio.Api/Features/Chat/SalonChatAgent.cs
    - API/ZachHairStudio.Api/Features/Chat/ChatController.cs
    - API/ZachHairStudio.Api.Tests/Features/Chat/SalonChatToolsTests.cs
    - API/ZachHairStudio.Api.Tests/Features/Chat/SalonChatAgentTests.cs
    - API/ZachHairStudio.Api.Tests/Features/Chat/ChatControllerTests.cs
  modified:
    - API/ZachHairStudio.Api/Program.cs
    - API/ZachHairStudio.Api/Mcp/ScheduleTools.cs
    - API/ZachHairStudio.Api/ZachHairStudio.Api.csproj
    - API/ZachHairStudio.Api/appsettings.json
    - dashboard/lib/adminChat.ts
    - dashboard/components/AdminChatWidget.tsx
    - dashboard/lib/api/schema.d.ts

key-decisions:
  - "Default model is Qwen/Qwen2.5-7B-Instruct through Hugging Face's configurable OpenAI-compatible router."
  - "Staff booking data is available only through authenticated /api/chat; anonymous /mcp remains limited to appointment slots."
  - "Booking tool payload excludes email and phone before data reaches the provider."
  - "Provider secrets stay in user-secrets or environment variables, never tracked configuration."

requirements-completed: [QUICK-260810-w7m]

coverage:
  - id: D1
    description: "Agent executes correlated multi-tool calls and stops at a configured bound"
    requirement: "QUICK-260810-w7m"
    verification:
      - kind: test
        ref: "SalonChatAgentTests"
        status: blocked
  - id: D2
    description: "Tool output is validated and booking contact details are excluded"
    requirement: "QUICK-260810-w7m"
    verification:
      - kind: test
        ref: "SalonChatToolsTests"
        status: blocked
  - id: D3
    description: "Chat is staff-authenticated and maps failures to controlled responses"
    requirement: "QUICK-260810-w7m"
    verification:
      - kind: test
        ref: "ChatControllerTests"
        status: blocked
  - id: D4
    description: "Dashboard uses generated typed /api/chat client with complete in-memory history"
    requirement: "QUICK-260810-w7m"
    verification:
      - kind: build
        ref: "dashboard npm run build"
        status: blocked

completed: 2026-08-10
status: incomplete
---

# Quick Task 260810-w7m: Real AI Salon Assistant Summary

Implemented the backend LLM agent and replaced the dashboard's keyword router with authenticated, multi-turn `/api/chat` communication.

## Accomplishments

- Configured the official OpenAI .NET SDK for Hugging Face's OpenAI-compatible router without committing a token.
- Added strict read-only tools for services, stylists, bookings, and slots; reused existing domain services in-process and retained the existing availability-only MCP endpoint.
- Added a bounded tool-call loop, server-owned system instructions, salon-local relative-date context, controlled provider errors, timeout handling, and staff authorization.
- Removed client-side intent regexes and slot-filling state; the dashboard now sends the ordered transcript through the generated OpenAPI client.
- Added deterministic tool, agent-loop, and controller tests with no Hugging Face network calls.

## Verification Status

The solution compiled successfully once before the final tests were added. Final `dotnet build`, focused/full `dotnet test`, and dashboard `npm run build` could not be executed because the session safety classifier repeatedly rejected these PowerShell/Bash executions as temporarily unavailable. `git diff --check` did run successfully and found no whitespace errors. The added tests therefore remain unverified and this summary is intentionally marked `incomplete`, not `complete`.

## User Setup Required

Set the provider token outside source control before running the API:

`dotnet user-secrets set "HuggingFace:ApiKey" "<HF token>" --project API/ZachHairStudio.Api`

Existing `RESEND_API_KEY` and `Jwt:SigningKey` setup remains required.

## Remaining Work

Run the backend build/tests and dashboard production build when command execution is available; fix any compile/test failures, then change this summary's coverage statuses and frontmatter status to `complete`.
