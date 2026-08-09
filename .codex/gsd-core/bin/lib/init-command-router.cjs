"use strict";
/**
 * Manifest-backed init subcommand router.
 * Keeps gsd-tools.cjs thin while preserving existing command semantics.
 *
 * Phase 6: all init.* subcommands have SDK equivalents and are dispatched
 * via executeForCjs (the sync bridge). CJS fallback retained when:
 * - GSD_WORKSTREAM is active (workstream-scoped requests fall through to CJS).
 * - SDK is unavailable (build not present).
 *
 * CJS-only subcommands: none.
 * SDK-only (unsupported in CJS router): none.
 *
 * ADR-457 build-at-publish: the hand-written bin/lib/init-command-router.cjs
 * collapsed to a TypeScript source of truth. Behaviour is preserved byte-for-behaviour
 * from the prior hand-written .cjs; only types are added.
 */
const command_aliases_cjs_1 = require("./command-aliases.cjs");
// eslint-disable-next-line @typescript-eslint/no-require-imports
const cjsCommandRouterAdapter = require("./cjs-command-router-adapter.cjs");
const { routeCjsCommandFamily } = cjsCommandRouterAdapter;
const command_arg_projection_cjs_1 = require("./command-arg-projection.cjs");
// ─── Implementation ───────────────────────────────────────────────────────────
function routeInitCommand({ init, args, cwd, raw, error }) {
    routeCjsCommandFamily({
        args,
        subcommands: command_aliases_cjs_1.INIT_SUBCOMMANDS,
        unsupported: {},
        error,
        unknownMessage: (_subcommand, available) => `Unknown init workflow: ${_subcommand}\nAvailable: ${available.join(', ')}`,
        handlers: {
            // #2932/#2992: `parseNamedArgs` never yields `undefined` for an absent
            // flag (value-flags default to `null`, booleanFlags default to `false`);
            // `buildSectionManifestField`'s flags-Set builder (src/init.cts) is the
            // single source of truth for flag ABSENCE and gates on value truthiness,
            // so `namedArgs` is passed through here uncoerced.
            'execute-phase': () => {
                const namedArgs = (0, command_arg_projection_cjs_1.parseNamedArgs)(args, [], ['validate', 'tdd', 'wave']);
                init.cmdInitExecutePhase(cwd, args[2], raw, {
                    validate: namedArgs['validate'],
                    tdd: namedArgs['tdd'],
                    wave: namedArgs['wave'],
                });
            },
            'plan-phase': () => {
                const namedArgs = (0, command_arg_projection_cjs_1.parseNamedArgs)(args, ['granularity', 'prd', 'ingest', 'research-phase'], ['validate', 'tdd', 'reviews', 'chunked']);
                init.cmdInitPlanPhase(cwd, args[2], raw, {
                    validate: namedArgs['validate'],
                    tdd: namedArgs['tdd'],
                    granularity: namedArgs['granularity'],
                    prd: namedArgs['prd'],
                    ingest: namedArgs['ingest'],
                    'research-phase': namedArgs['research-phase'],
                    reviews: namedArgs['reviews'],
                    chunked: namedArgs['chunked'],
                });
            },
            'new-project': () => {
                const namedArgs = (0, command_arg_projection_cjs_1.parseNamedArgs)(args, [], ['auto']);
                init.cmdInitNewProject(cwd, raw, { auto: namedArgs['auto'] });
            },
            'new-milestone': () => {
                const namedArgs = (0, command_arg_projection_cjs_1.parseNamedArgs)(args, [], ['reset-phase-numbers']);
                init.cmdInitNewMilestone(cwd, raw, {
                    'reset-phase-numbers': namedArgs['reset-phase-numbers'],
                });
            },
            onboard: () => {
                const namedArgs = (0, command_arg_projection_cjs_1.parseNamedArgs)(args, [], ['fast', 'text']);
                init.cmdInitOnboard(cwd, raw, { fast: namedArgs['fast'], text: namedArgs['text'] });
            },
            quick: () => {
                const namedArgs = (0, command_arg_projection_cjs_1.parseNamedArgs)(args, [], ['discuss', 'research', 'validate', 'full']);
                // #2994: `args.slice(2)` is the free-text description, but section-manifest
                // gating (buildSectionManifestField, src/init.cts) now requires forwarding
                // --discuss/--research/--validate/--full alongside it — a plain `.join(' ')`
                // would otherwise fold those recognized flag tokens straight into the
                // description text. Strip them before joining so the description stays
                // exactly what it was before this workflow started forwarding flags.
                const quickFlagTokens = new Set(['--discuss', '--research', '--validate', '--full']);
                const description = args
                    .slice(2)
                    .filter((token) => !quickFlagTokens.has(token))
                    .join(' ');
                init.cmdInitQuick(cwd, description, raw, {
                    discuss: namedArgs['discuss'],
                    research: namedArgs['research'],
                    validate: namedArgs['validate'],
                    full: namedArgs['full'],
                });
            },
            'ingest-docs': () => init.cmdInitIngestDocs(cwd, raw),
            resume: () => init.cmdInitResume(cwd, raw),
            'verify-work': () => init.cmdInitVerifyWork(cwd, args[2], raw),
            'phase-op': () => init.cmdInitPhaseOp(cwd, args[2], raw),
            'code-review': () => {
                const namedArgs = (0, command_arg_projection_cjs_1.parseNamedArgs)(args, [], ['fix']);
                init.cmdInitCodeReview(cwd, args[2], raw, { fix: namedArgs['fix'] });
            },
            review: () => init.cmdInitReview(cwd, args[2], raw, {}),
            'discuss-phase-assumptions': () => {
                const namedArgs = (0, command_arg_projection_cjs_1.parseNamedArgs)(args, [], ['auto']);
                init.cmdInitDiscussPhaseAssumptions(cwd, args[2], raw, { auto: namedArgs['auto'] });
            },
            todos: () => init.cmdInitTodos(cwd, args[2], raw),
            'milestone-op': () => init.cmdInitMilestoneOp(cwd, raw),
            'map-codebase': () => init.cmdInitMapCodebase(cwd, raw),
            progress: () => {
                const namedArgs = (0, command_arg_projection_cjs_1.parseNamedArgs)(args, [], ['forensic']);
                init.cmdInitProgress(cwd, raw, { forensic: namedArgs['forensic'] });
            },
            // Keep manager on CJS for now so runtime-specific command rendering
            // (e.g. $gsd-* for codex) stays consistent with runtime-slash helpers.
            manager: () => init.cmdInitManager(cwd, raw),
            'complete-milestone': () => init.cmdInitCompleteMilestone(cwd, raw),
            autonomous: () => {
                const namedArgs = (0, command_arg_projection_cjs_1.parseNamedArgs)(args, [], ['converge', 'cross-ai']);
                init.cmdInitAutonomous(cwd, raw, {
                    converge: namedArgs['converge'],
                    'cross-ai': namedArgs['cross-ai'],
                });
            },
            'docs-update': () => init.cmdInitDocsUpdate(cwd, raw, {}),
            update: () => {
                const namedArgs = (0, command_arg_projection_cjs_1.parseNamedArgs)(args, [], ['next', 'rc']);
                init.cmdInitUpdate(cwd, raw, { next: namedArgs['next'], rc: namedArgs['rc'] });
            },
            transition: () => init.cmdInitTransition(cwd, raw, {}),
            debug: () => {
                const namedArgs = (0, command_arg_projection_cjs_1.parseNamedArgs)(args, [], ['diagnose']);
                init.cmdInitDebug(cwd, raw, { diagnose: namedArgs['diagnose'] });
            },
            'new-workspace': () => init.cmdInitNewWorkspace(cwd, raw),
            'list-workspaces': () => init.cmdInitListWorkspaces(cwd, raw),
            'remove-workspace': () => init.cmdInitRemoveWorkspace(cwd, args[2], raw),
        },
    });
}
module.exports = {
    routeInitCommand,
};
