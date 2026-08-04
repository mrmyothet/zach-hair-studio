---
phase: 260801-irn
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - API/ZachHairStudio.Api/ZachHairStudio.Api.csproj
  - API/ZachHairStudio.Api/Mcp/ScheduleTools.cs
  - API/ZachHairStudio.Api/Program.cs
autonomous: true
requirements: [QUICK-260801-irn]

must_haves:
  truths:
    - "An MCP client connected to the API can call a tool named get_appointment_slots and receive the open appointment start times for a service on a given date."
    - "Omitting stylistId returns the any-stylist union view; supplying it returns that stylist's slots — results identical to GET /api/appointments/slots."
    - "A malformed date argument returns a structured JSON error payload, not an unhandled exception."
    - "The existing REST availability endpoint, its service, and its DTO are unchanged — this is purely additive."
  artifacts:
    - "API/ZachHairStudio.Api/Mcp/ScheduleTools.cs — non-empty, carries [McpServerToolType]"
    - "API/ZachHairStudio.Api/ZachHairStudio.Api.csproj — PackageReference ModelContextProtocol.AspNetCore 2.0.0"
    - "API/ZachHairStudio.Api/Program.cs — AddMcpServer registration plus MapMcp(\"/mcp\") endpoint"
  key_links:
    - "ScheduleTools.GetAppointmentSlots -> SlotService.GetOpenSlotsAsync — SlotService arrives by DI from the per-request scope; if it is not resolvable the SDK treats it as a schema parameter and the tool signature silently changes shape."
    - "Program.cs WithTools<ScheduleTools>() -> ScheduleTools type — without this link the tool never registers and an MCP client sees an empty tool list even though the code compiles."
    - "app.MapMcp(\"/mcp\") -> HTTP route — no endpoint means no transport, even with the tool correctly registered."
---

<objective>
Expose the existing appointment-slot availability lookup as a read-only MCP tool named `get_appointment_slots`, so MCP-capable clients can query open booking slots without going through the REST API.

Purpose: The salon's core value is effortless booking. Availability is the one read an assistant needs before it can help anyone book. This wires the existing, already-correct `SlotService` grid math to a second protocol surface without duplicating or altering it.

Output: MCP SDK package reference, an MCP server registered on the API at `/mcp`, and a populated `Mcp/ScheduleTools.cs` (currently an empty scaffold).
</objective>

<execution_context>
@C:/repos/vct/zach-hair-studio/.claude/gsd-core/workflows/execute-plan.md
@C:/repos/vct/zach-hair-studio/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@.claude/CLAUDE.md

@API/ZachHairStudio.Api/Program.cs
@API/ZachHairStudio.Api/ZachHairStudio.Api.csproj
@API/ZachHairStudio.Shared/Features/Availability/SlotService.cs
@API/ZachHairStudio.Shared/Features/Availability/OpenSlotDto.cs
</context>

<verified_facts>
Facts established during planning against nuget.org and the official C# SDK docs. Do NOT re-derive these — they are load-bearing for the tasks below.

- **Package:** `ModelContextProtocol.AspNetCore`, latest stable `2.0.0`. Authors `ModelContextProtocol`, projectUrl `https://csharp.sdk.modelcontextprotocol.io/`. It ships a `net10.0` target group and transitively pins the core `ModelContextProtocol [2.0.0, 2.0.0]` — so only the AspNetCore package needs an explicit reference.
- **Server registration API in 2.0.0:** `builder.Services.AddMcpServer().WithHttpTransport(options => { options.Stateless = true; }).WithTools<TToolType>()`, then `app.MapMcp("/mcp")`.
- **Route overload:** `MapMcp(string pattern)` exists. Streamable-HTTP clients connect to `/mcp`; SSE clients to `/mcp/sse`.
- **DI scoping:** In **stateless** HTTP mode the SDK shares the existing ASP.NET Core HTTP request scope. That is exactly what a scoped `SlotService` (holding a scoped `BookingDbContext`) requires. Stateful mode would create its own per-invocation scope; stateless is both simpler and correct here.
- **`McpServerToolAttribute` exposes `ReadOnly`** (bool, defaults false), alongside `Title`, `Destructive`, `Idempotent`, `OpenWorld` — so `[McpServerTool(Name = "...", ReadOnly = true)]` from the reference pattern is valid.
- **`WithTools<T>()` cannot take a `static class`** — C# forbids static classes as generic type arguments. The tool class must be a plain `public class` whose *methods* are static (this is the shape the official docs use).
- **`SlotService` is already registered** at `Program.cs:54` (`AddScoped<SlotService>()`), and `SalonOptions` is already bridged for it at line 53. No new DI registration is needed for the tool's dependency.
- **`GetSlots` is anonymous today** — `AppointmentsController` carries no `[Authorize]`, so exposing the same data over MCP grants no new access.
- **`GetOpenSlotsAsync` takes no `CancellationToken`**, so the reference pattern's trailing `CancellationToken` parameter has nothing to flow into and is omitted.
- **Solution file:** `API/ZachHairStudio.slnx` (also contains `ZachHairStudio.Admin` and `ZachHairStudio.Api.Tests`).
</verified_facts>

<tasks>

<task type="auto">
  <name>Task 1: Reference the official MCP ASP.NET Core SDK</name>
  <files>API/ZachHairStudio.Api/ZachHairStudio.Api.csproj</files>
  <action>
Add a single `PackageReference` for `ModelContextProtocol.AspNetCore` pinned to version `2.0.0` into the existing `ItemGroup` that already holds the other package references, keeping that group's alphabetical-ish ordering (it belongs before the `Microsoft.*` entries). Do NOT add a separate reference for the core `ModelContextProtocol` package — the AspNetCore package pins it transitively to the exact same version, and a second explicit reference only creates a version-drift hazard.

Also drop the now-obsolete empty-directory `ItemGroup` at the bottom of the file — the one whose sole child declares the `Mcp\` directory. That entry existed only so an empty scaffold folder would show in the IDE; Task 2 puts a real compiled file there, which the Web SDK globs automatically.

Then run restore so the package is materialized before the next task compiles against it.
  </action>
  <verify>
    <automated>grep -c 'ModelContextProtocol.AspNetCore" Version="2.0.0"' API/ZachHairStudio.Api/ZachHairStudio.Api.csproj  # expect 1, then: dotnet restore API/ZachHairStudio.slnx  # expect exit 0</automated>
  </verify>
  <done>The csproj declares `ModelContextProtocol.AspNetCore` at `2.0.0` exactly once, the empty-directory item group is gone, and `dotnet restore` completes with exit code 0.</done>
</task>

<task type="auto">
  <name>Task 2: Implement the get_appointment_slots MCP tool</name>
  <files>API/ZachHairStudio.Api/Mcp/ScheduleTools.cs</files>
  <action>
Populate the currently-empty scaffold `API/ZachHairStudio.Api/Mcp/ScheduleTools.cs` with a file-scoped namespace `ZachHairStudio.Api.Mcp` (per CLAUDE.md C# conventions: file-scoped namespaces, nullable enabled, minimal comments).

Declare `public class ScheduleTools` decorated with `[McpServerToolType]` from the `ModelContextProtocol.Server` namespace. It must be a **plain class, not a static class** — Task 3 passes it as a generic type argument to `WithTools<T>()`, and C# rejects static classes in that position. Its members are still static, matching the reference pattern. A single brief comment explaining the non-static class is warranted here since the reason is non-obvious.

Add a `private static readonly JsonSerializerOptions` field configured with `JsonNamingPolicy.CamelCase` for property naming, so the serialized `OpenSlotDto` fields come out as `startsAt` / `stylistId` / `stylistName` — matching the casing the REST endpoint already returns and keeping one wire shape across both protocols.

Define one public static async method returning `Task<string>`, annotated with `[McpServerTool(Name = "get_appointment_slots", ReadOnly = true)]` plus a `[Description]` explaining that it lists open appointment start times for a service on a given date and that omitting the stylist argument yields the any-stylist view. Parameters, in this order:

1. `SlotService slotService` — no attribute. The SDK resolves registered service types from the request's DI scope and excludes them from the generated tool schema.
2. `int serviceId` with a `[Description]` naming it as the service catalog id to check availability for.
3. `string date` with a `[Description]` stating the expected `yyyy-MM-dd` format and that it is interpreted in salon local time.
4. `int? stylistId = null` with a `[Description]` marking it optional and explaining that omitting it returns the union of all active stylists' openings.

`date` is typed as a plain string parsed in-method, mirroring the reference pattern, rather than a bare `DateOnly` — it keeps the generated JSON schema an unambiguous string and lets a bad value return a clean payload instead of a binding-layer throw. Parse it with `DateOnly.TryParseExact` using `"yyyy-MM-dd"`, `CultureInfo.InvariantCulture`, and `DateTimeStyles.None`. On a parse failure, return early with a serialized object carrying an `error` field that names the rejected input and restates the expected format (mitigates T-Q06).

On success, await `slotService.GetOpenSlotsAsync(serviceId, stylistId, parsedDate)` and return `JsonSerializer.Serialize` of an anonymous object shaped `{ date, serviceId, stylistId, count, slots }`, where `date` is the round-tripped parsed value re-formatted as `yyyy-MM-dd`, `count` is the returned list's `Count`, and `slots` is the list itself.

Do NOT open, edit, or re-shape `AppointmentsController`, `SlotService`, or `OpenSlotDto`. This task consumes the existing service exactly as it stands; every grid, time-zone, and DST rule already lives in `SlotService` and is not to be reimplemented or wrapped in new logic here.

If — and only if — the build reveals that the SDK did not bind the `SlotService` parameter from DI (it would surface as that parameter appearing in the tool's input schema, or as a build/runtime binding complaint), do not restructure the service: switch the class to constructor injection with static-to-instance method conversion, which the SDK also supports for `[McpServerToolType]` classes.
  </action>
  <verify>
    <automated>grep -c 'get_appointment_slots' API/ZachHairStudio.Api/Mcp/ScheduleTools.cs  # expect 1, and: grep -v '^\s*//' API/ZachHairStudio.Api/Mcp/ScheduleTools.cs | grep -cE 'McpServerToolType|GetOpenSlotsAsync|TryParseExact'  # expect 3</automated>
  </verify>
  <done>`ScheduleTools.cs` is non-empty, declares a non-static `[McpServerToolType]` class in namespace `ZachHairStudio.Api.Mcp`, and exposes exactly one `ReadOnly` tool named `get_appointment_slots` that delegates to `SlotService.GetOpenSlotsAsync` and returns a camelCase JSON string. The Shared project has no working-tree changes.</done>
</task>

<task type="auto">
  <name>Task 3: Register the MCP server in Program.cs and verify the build</name>
  <files>API/ZachHairStudio.Api/Program.cs</files>
  <action>
Register the MCP server in the service-configuration block. Place it immediately after the existing `AddScoped<SlotService>()` line so the tool sits next to the dependency it consumes. Chain, in one statement: `builder.Services.AddMcpServer()`, then `.WithHttpTransport(...)` with a braced lambda setting `options.Stateless = true`, then `.WithTools<ScheduleTools>()`.

Stateless mode is deliberate and load-bearing, not a default — it makes the SDK share the ASP.NET Core per-HTTP-request DI scope, which is what lets the scoped `SlotService` and its `BookingDbContext` resolve correctly per tool call. Add a short comment recording that rationale.

Use `WithTools<ScheduleTools>()` rather than assembly-wide tool discovery: discovery would auto-register any future tool type added anywhere in this assembly, including a write-capable one, onto an unauthenticated endpoint. Explicit registration keeps the exposed surface exactly one read-only tool (mitigates T-Q04).

Add the matching `using ZachHairStudio.Api.Mcp;` to the existing using block, in alphabetical position among the `ZachHairStudio.*` imports. The `AddMcpServer`, `WithHttpTransport`, and `MapMcp` extension methods themselves need no new using — they live in namespaces the Web SDK's implicit usings already cover.

Map the endpoint with `app.MapMcp("/mcp");` placed directly after the existing `app.MapControllers();` line, so it sits after `UseAuthentication()`/`UseAuthorization()` in the pipeline. Use the explicit `"/mcp"` route argument — the parameterless overload would mount the transport at the application root.

Change nothing else in `Program.cs`: the DbContext setup, CORS policy, JWT options validation, Identity wiring, migration/seed block, and Swagger gating all stay exactly as they are.

Finally, build the whole solution to confirm the three edits compile together.
  </action>
  <verify>
    <automated>grep -v '^\s*//' API/ZachHairStudio.Api/Program.cs | grep -cE 'AddMcpServer\(\)|MapMcp\("/mcp"\)'  # expect 2, then: dotnet build API/ZachHairStudio.slnx  # expect exit 0, 0 errors</automated>
  </verify>
  <done>`dotnet build API/ZachHairStudio.slnx` succeeds with zero errors. `Program.cs` registers the MCP server with stateless HTTP transport and the explicitly-typed tool, and maps it at `/mcp`. `git status --porcelain -- API/ZachHairStudio.Shared/ API/ZachHairStudio.Api/Controllers/` returns no lines.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| MCP client -> `/mcp` endpoint | Untrusted JSON-RPC tool arguments (`serviceId`, `date`, `stylistId`) cross into the API and reach EF Core queries. |
| NuGet registry -> build | A new third-party package enters the API's dependency closure and runs in-process. |
| `/mcp` -> `BookingDbContext` | Tool invocation triggers database reads on the shared salon schema. |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-Q01 | Information Disclosure | `get_appointment_slots` response | low | accept | Returns only open start times plus stylist display names — the exact payload `GET /api/appointments/slots` already serves anonymously. No client PII, no appointment holder data. No new exposure created. |
| T-Q02 | Denial of Service | `/mcp` tool invocation | medium | accept | Each call runs the same bounded single-day queries as the REST endpoint; there is no caller-controlled result limit or unbounded loop. Rate limiting is deferred to Phase 8 (LAUNCH-02) alongside the CORS lockdown, consistent with the existing REST surface. |
| T-Q03 | Tampering | NuGet install of `ModelContextProtocol.AspNetCore` | high | mitigate | **VERIFIED against nuget.org during planning** — package id `ModelContextProtocol.AspNetCore`, authors `ModelContextProtocol`, projectUrl `https://csharp.sdk.modelcontextprotocol.io/`, stable `2.0.0` carrying a real `net10.0` target group and pinning core `ModelContextProtocol [2.0.0, 2.0.0]`. This is the official SDK named by the user, not an assumed name. Pin the exact version (Task 1); no explicit core-package reference, so no drift window. Not `[ASSUMED]` — no blocking human legitimacy checkpoint required. |
| T-Q04 | Elevation of Privilege | Tool registration surface | medium | mitigate | Register with `WithTools<ScheduleTools>()`, never assembly-wide discovery (Task 3) — discovery would silently expose any future write-capable tool type on this unauthenticated endpoint. Mount at explicit `/mcp`, after `UseAuthentication`/`UseAuthorization`, never at the application root. The one registered tool is marked `ReadOnly = true`. |
| T-Q05 | Information Disclosure | CORS default policy now covers `/mcp` | medium | accept | `AllowAnyOrigin` already applies to every endpoint; the data behind this one is public availability with no credentialed access. Production origin lockdown is tracked at Phase 8 (LAUNCH-02) and will cover `/mcp` with the rest. |
| T-Q06 | Tampering | `date` string argument | low | mitigate | Parse with `DateOnly.TryParseExact` under `CultureInfo.InvariantCulture` and `DateTimeStyles.None` (Task 2); a malformed value returns a structured JSON `error` payload rather than throwing. `serviceId`/`stylistId` are typed integers parameterized through EF Core — no string concatenation reaches SQL. |
</threat_model>

<verification>
1. `dotnet restore API/ZachHairStudio.slnx` — exit 0.
2. `dotnet build API/ZachHairStudio.slnx` — exit 0, zero errors. This is the required gate from the task brief.
3. `git status --porcelain -- API/ZachHairStudio.Shared/ API/ZachHairStudio.Api/Controllers/` — returns nothing, proving the additive-only constraint held.
4. `git status --porcelain -- API/` lists exactly the three planned files and nothing else.

Non-blocking notes for the executor:
- The existing `ZachHairStudio.Api.Tests` suite requires `RESEND_API_KEY` via user-secrets to run (D-12/D-13). Build success is the gate for this task; if the key is already configured locally, running the suite is a useful regression signal that the new startup registrations did not disturb `WebApplicationFactory` boot.
- End-to-end confirmation with a live MCP client (connect to `http://localhost:5236/mcp`, list tools, call `get_appointment_slots`) requires a running API and a configured client, so it is out of scope for the automated gate. Use the `dev` project skill to launch the stack if manual confirmation is wanted.
</verification>

<success_criteria>
- `ModelContextProtocol.AspNetCore` 2.0.0 is referenced exactly once in the API csproj; the obsolete empty-folder item group is removed.
- `Mcp/ScheduleTools.cs` is no longer an empty scaffold — it declares a `[McpServerToolType]` class exposing a single `ReadOnly` tool named `get_appointment_slots` that delegates to `SlotService.GetOpenSlotsAsync` and serializes `{ date, serviceId, stylistId, count, slots }`.
- An invalid `date` argument yields a JSON `error` payload rather than an unhandled exception.
- `Program.cs` registers the MCP server with stateless HTTP transport plus explicit `WithTools<ScheduleTools>()`, and maps it at `/mcp`.
- `dotnet build API/ZachHairStudio.slnx` succeeds with zero errors.
- `AppointmentsController.cs`, `SlotService.cs`, and `OpenSlotDto.cs` have zero working-tree changes.
- Every threat in the register carries a severity and a disposition; T-Q03/T-Q04/T-Q06 mitigations are implemented by the tasks above.
</success_criteria>

<output>
Create `.planning/quick/260801-irn-add-an-mcp-tool-exposing-appointment-slo/260801-irn-SUMMARY.md` when done.
</output>
