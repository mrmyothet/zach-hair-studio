"use strict";
/**
 * Verification Status — single queryable home for verification-status routing.
 *
 * Issue #651: consolidate the pass/gaps_found/human_needed routing that was
 * previously scattered across ship.md and execute-phase.md into a single
 * tested module. Both workflow files will later consume this module's routing
 * table as the single source of truth.
 *
 * ADR-457 build-at-publish: source in src/verification.cts, compiled to
 * gsd-core/bin/lib/verification.cjs (gitignored).
 *
 * DEFECT.FRONTMATTER-SCALAR-BROAD-GREP fix: status extraction is scoped to
 * the leading YAML frontmatter block only. A `status:` line in the body (e.g.
 * inside a fenced code block) is ignored — this is the exact failure mode that
 * issue #586 / PR #650 identified. The shared extractFrontmatter parser anchors
 * its regex at byte 0 of the document, which provides this guarantee.
 *
 * #2348 staleness signal: whether a *-VERIFICATION.md is stale (a summary newer
 * than it) is decided from git commit time when a file is committed AND clean,
 * and from filesystem mtime otherwise. mtimes are assigned at checkout time and
 * are not preserved by `git clone` / `cp -R`, and any unrelated `touch` /
 * reformat / editor-save re-stales a valid report — so a committed phase could
 * read `passed` on one machine and `stale` on a fresh clone purely from checkout
 * order. Git commit time is content-tied and clone-stable; mtime is retained
 * only for uncommitted or working-tree-dirty files, where it is the true
 * last-changed signal. Both are real wall-clock change times, so the comparison
 * is sound even when one file uses each.
 */
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
const node_fs_1 = __importDefault(require("node:fs"));
const node_path_1 = __importDefault(require("node:path"));
// eslint-disable-next-line @typescript-eslint/no-require-imports -- io.cjs is an export= CommonJS module
const io = require("./io.cjs");
// eslint-disable-next-line @typescript-eslint/no-require-imports -- phase-id.cjs is an export= CommonJS module
const phaseId = require("./phase-id.cjs");
// eslint-disable-next-line @typescript-eslint/no-require-imports -- frontmatter.cjs is an export= CommonJS module
const frontmatterMod = require("./frontmatter.cjs");
// eslint-disable-next-line @typescript-eslint/no-require-imports -- plan-scan.cjs is an export= CommonJS module
const scanPhasePlans = require("./plan-scan.cjs");
const shell_command_projection_cjs_1 = require("./shell-command-projection.cjs");
const runtime_slash_cjs_1 = require("./runtime-slash.cjs");
const { output, error } = io;
const { extractPhaseToken } = phaseId;
const { extractFrontmatter } = frontmatterMod;
// ─── Constants ────────────────────────────────────────────────────────────────
/** The set of status values that the gsd-verifier agent emits. */
const VERIFIER_STATUSES = ['passed', 'gaps_found', 'human_needed'];
/**
 * Canonical routing table for verification statuses.
 *
 * This is the single source of truth — ship.md and execute-phase.md will
 * later import from here instead of embedding their own message strings.
 *
 * INTERNAL SENTINELS: 'missing' and 'unknown' are operational states constructed
 * internally — the verifier (gsd-verifier.md) never emits them. The verifier only
 * emits values in VERIFIER_STATUSES (passed|gaps_found|human_needed). The guard in
 * readVerificationStatus excludes 'missing' and 'unknown' from raw-status table
 * lookup so they can only be reached via internal construction paths.
 *
 * For 'gaps_found', next_command is built at call time in readVerificationStatus
 * by substituting the phase number — it is NOT stored as a function in the table.
 *
 * #2617: `next_command` here holds a BARE command name (`execute-phase`), never a
 * prefixed one. Every return path projects it through `formatGsdSlash` with the
 * caller's runtime, so Codex sees `$gsd-execute-phase` and slash-hyphen runtimes
 * see `/gsd-execute-phase`. Storing a prefixed literal is what leaked the
 * hard-coded (and deprecated) `/gsd:` colon form to every runtime.
 */
const VERIFICATION_ROUTING_TABLE = {
    passed: {
        status: 'passed',
        next_action: 'Verification passed — continue.',
        next_command: '',
    },
    gaps_found: {
        status: 'gaps_found',
        next_action: 'Gaps found. Plan the fixes, then re-run execute-phase before shipping.',
        // next_command is computed at call time; this entry is never returned directly.
        next_command: '',
    },
    human_needed: {
        status: 'human_needed',
        next_action: "Human verification required. Complete the manual tests in the phase's *-UAT.md, then re-run the verify step until status is passed.",
        // #2617: was '' — next_action told the user to "re-run the verify step" but
        // named no command, while init.cts's parallel projector emitted
        // `verify-work <N>` for this same state. The two surfaces disagreed on
        // whether a next command existed at all; init's answer was the useful one,
        // and init now delegates here rather than re-deriving it.
        next_command: 'verify-work',
    },
    stale: {
        status: 'stale',
        next_action: 'Verification is stale. Re-run verify-work before transition.',
        next_command: '',
    },
    // INTERNAL SENTINEL: constructed when no *-VERIFICATION.md file exists or when
    // the file has no parseable frontmatter status. Never emitted by the verifier.
    missing: {
        status: 'missing',
        next_action: 'No verification report found — the verify step never completed. Re-run execute-phase.',
        next_command: 'execute-phase',
    },
    // INTERNAL SENTINEL: constructed when the file has a status value not in
    // VERIFIER_STATUSES. Never emitted by the verifier.
    unknown: {
        status: 'unknown',
        next_action: '', // filled in dynamically with the raw value
        next_command: 'execute-phase',
    },
};
/**
 * Project a BARE command name (plus optional argument tail) into the surface the
 * given runtime actually installs (#2617).
 *
 * `formatGsdSlash` owns the per-runtime shape (`$gsd-<cmd>` for shell-var
 * runtimes like Codex, `/gsd-<cmd>` otherwise) and is idempotent, so passing an
 * already-prefixed string is safe. An empty command stays empty — "no next
 * command" must not become a bare prefix.
 */
function projectNextCommand(bare, runtime, tail = '') {
    if (!bare)
        return '';
    return `${(0, runtime_slash_cjs_1.formatGsdSlash)(bare, runtime)}${tail}`;
}
/** Normalize separators to posix (git emits `/`; callers may pass `\` on Windows). */
function toPosix(p) {
    return p.replace(/\\/g, '/');
}
/**
 * Match a git-emitted (repo-root-relative) path back to the caller's
 * phaseDir-relative request by exact match or `/`-bounded suffix — precise
 * enough that a root file and a nested `plans/` file can never collide (a plain
 * basename match could). Returns the original caller-form file string, or null.
 */
function matchRequestedFile(gitPath, requested, requestedPosix) {
    const g = toPosix(gitPath);
    for (let i = 0; i < requested.length; i++) {
        const want = requestedPosix[i];
        if (g === want || g.endsWith('/' + want))
            return requested[i];
    }
    return null;
}
/**
 * Parse `git log --format=%ct --name-only` output into file → most-recent commit
 * time (ms). Output is reverse-chronological, so a file's FIRST appearance
 * top-down is its latest commit. `%ct` headers are pure digits; path lines
 * contain a `.` (the `.md` extension) — so the two are unambiguous.
 */
function parseCommitTimes(stdout, requested, requestedPosix) {
    const out = new Map();
    let currentCt = null;
    for (const line of stdout.split('\n')) {
        if (line.length === 0)
            continue;
        if (/^\d+$/.test(line)) {
            currentCt = Number.parseInt(line, 10);
            continue;
        }
        if (currentCt === null)
            continue;
        const rel = matchRequestedFile(line, requested, requestedPosix);
        if (rel !== null && !out.has(rel))
            out.set(rel, currentCt * 1000);
    }
    return out;
}
function defaultPhaseCleanCommitTimesMs(phaseDir, files, execGitFn = shell_command_projection_cjs_1.execGit) {
    if (files.length === 0)
        return new Map();
    const requestedPosix = files.map(toPosix);
    const logRes = execGitFn(['log', '--first-parent', '--format=%ct', '--name-only', '--', ...files], {
        cwd: phaseDir,
    });
    if (logRes.error || logRes.exitCode !== 0 || logRes.stdout.length === 0)
        return new Map();
    const commitTimes = parseCommitTimes(logRes.stdout, files, requestedPosix);
    if (commitTimes.size === 0)
        return commitTimes;
    // Drop dirty files (working tree ≠ HEAD) so their mtime is used instead. If the
    // dirty-check itself is INCONCLUSIVE (git diff errored / non-zero — as opposed
    // to "ran and reported no dirty files"), we cannot prove any file is clean, so
    // fail SAFE: discard the commit times and let every file fall back to mtime,
    // the same direction as a git-log failure. Trusting possibly-stale commit times
    // here would silently mask a real edit (false "not stale"). (#2348)
    const diffRes = execGitFn(['diff', '--name-only', 'HEAD', '--', ...files], { cwd: phaseDir });
    if (diffRes.error || diffRes.exitCode !== 0)
        return new Map();
    for (const line of diffRes.stdout.split('\n')) {
        if (line.length === 0)
            continue;
        const rel = matchRequestedFile(line, files, requestedPosix);
        if (rel !== null)
            commitTimes.delete(rel);
    }
    return commitTimes;
}
/**
 * Build a 'missing' result from the routing table.
 * Used for two early-return paths: no *-VERIFICATION.md file found, and
 * file present but no parseable frontmatter status.
 */
function missingResult(runtime, phaseArg) {
    const route = VERIFICATION_ROUTING_TABLE['missing'];
    return {
        status: route.status,
        next_action: route.next_action,
        next_command: projectNextCommand(route.next_command, runtime, phaseArg),
    };
}
function findStaleVerificationSummary(phaseDir, fsImpl = node_fs_1.default, phaseCleanCommitTimesMs = defaultPhaseCleanCommitTimesMs) {
    // FS errors (TOCTOU: a SUMMARY listed by scanPhasePlans then removed before statSync;
    // unreadable dir; broken symlink; file->dir swap) must degrade rather than throw
    // uncaught into callers that are NOT under the planning lock (init.manager /
    // init.progress / uat-predicate). Mirrors readVerificationStatus's no-throw
    // contract; `fsImpl` threads the same injectable-fs seam for parity/testing.
    // (Review B1 on #1548.) The degraded result is `{determined:false}`, NOT the
    // same value as a completed "nothing is stale" check — see StaleCheckResult
    // doc and #3057 B3. The caller decides how to route an indeterminate result;
    // this function only reports what it actually knows.
    try {
        const phaseFiles = fsImpl.readdirSync(phaseDir);
        const verificationFile = phaseFiles.filter((f) => f.endsWith('-VERIFICATION.md')).sort()[0];
        if (!verificationFile)
            return { determined: true, stale: false };
        const summaryFiles = scanPhasePlans(phaseDir).summaryFiles
            .slice()
            .sort();
        // No summary can be newer than the verification → never stale. Return before
        // touching git so a phase with no summaries costs zero subprocesses. (#2348)
        if (summaryFiles.length === 0)
            return { determined: true, stale: false };
        // Each file's effective "last changed" time = its commit time when committed
        // AND clean (content-tied and clone-stable), else its filesystem mtime (the
        // uncommitted working-tree edit). Both are real wall-clock change times, so
        // comparing a clean file's commit time against a dirty file's mtime is sound.
        // One resolver call = two git subprocesses for the whole phase. (#2348)
        const cleanCommitMs = phaseCleanCommitTimesMs(phaseDir, [verificationFile, ...summaryFiles]);
        const effectiveTimeMs = (file) => cleanCommitMs.has(file)
            ? cleanCommitMs.get(file)
            : fsImpl.statSync(node_path_1.default.join(phaseDir, file)).mtimeMs;
        const verificationTimeMs = effectiveTimeMs(verificationFile);
        for (const summaryFile of summaryFiles) {
            // The caller only needs whether the phase is stale, not which summary —
            // the first stale summary (in sorted order) is enough. Short-circuit.
            if (effectiveTimeMs(summaryFile) > verificationTimeMs) {
                return { determined: true, stale: true, verificationFile, summaryFile };
            }
        }
        return { determined: true, stale: false };
    }
    catch {
        return { determined: false };
    }
}
/**
 * Read the verification status from the first `*-VERIFICATION.md` file in
 * phaseDir and return the routing result.
 *
 * Behavior:
 * 1. Find the first file matching `*-VERIFICATION.md` (sorted, take first).
 *    If none → status 'missing'.
 * 2. Extract `status` from FRONTMATTER ONLY via the shared extractFrontmatter
 *    parser (DEFECT.FRONTMATTER-SCALAR-BROAD-GREP fix — parser anchors at byte 0).
 *    If no frontmatter block or no `status` key → status 'missing'.
 * 3. Map to routing table. Unknown non-empty value → status 'unknown'.
 *
 * The internal staleness check can itself fail (fs / scanPhasePlans / clock
 * error); when it does, `status` is routed as if nothing were stale (the
 * pre-existing no-throw fail-open contract — unchanged), but the returned
 * result carries `staleCheckIndeterminate: true` so a caller can distinguish
 * "checked; nothing is stale" from "could not check" (#3057 B3).
 *
 * @param phaseDir - Absolute path to the phase directory.
 * @param opts     - Options. `opts.fs` allows test injection (defaults to node:fs).
 *                   `opts.runtime` selects the command surface `next_command` is
 *                   projected into (#2617).
 */
function readVerificationStatus(phaseDir, opts = {}) {
    const fsImpl = opts.fs ?? node_fs_1.default;
    const phaseCleanCommitTimesMs = opts.phaseCleanCommitTimesMs ?? defaultPhaseCleanCommitTimesMs;
    const runtime = opts.runtime ?? 'claude';
    // Phase token for the gaps_found command
    const baseName = node_path_1.default.basename(phaseDir);
    const phaseToken = extractPhaseToken(baseName);
    const derivedPhaseNumber = phaseToken.length > 0 ? phaseToken : baseName;
    // #2617: the phase number becomes a COMMAND ARGUMENT, so it is appended only
    // when it is unambiguously one. extractPhaseToken also returns project-code
    // forms (`PROJ-07`), which are indistinguishable by shape from an ordinary
    // directory name — `gsd-651-parent` yields `gsd-651` — and emitting
    // `execute-phase gsd-651` is worse than emitting no argument at all. Callers
    // that already know the number (init) pass it explicitly and always get it.
    const phaseArgSource = opts.phaseNumber ?? (/^\d+(\.\d+)*$/.test(derivedPhaseNumber) ? derivedPhaseNumber : '');
    const phaseArg = phaseArgSource ? ` ${phaseArgSource}` : '';
    // 1. Find *-VERIFICATION.md
    let verificationFile = null;
    try {
        const entries = fsImpl.readdirSync(phaseDir);
        const candidates = entries.filter((f) => f.endsWith('-VERIFICATION.md')).sort();
        verificationFile = candidates.length > 0 ? candidates[0] : null;
    }
    catch {
        // Directory unreadable → treat as missing
        verificationFile = null;
    }
    if (!verificationFile) {
        return missingResult(runtime, phaseArg);
    }
    // 2. Read and parse frontmatter using the shared parser.
    // extractFrontmatter anchors at byte 0, so body `status:` lines are ignored.
    const filePath = node_path_1.default.join(phaseDir, verificationFile);
    let rawStatus = null;
    try {
        const content = fsImpl.readFileSync(filePath, 'utf-8');
        const fm = extractFrontmatter(content, filePath);
        const statusVal = fm['status'];
        // status is always a scalar string in a well-formed VERIFICATION.md frontmatter;
        // only accept string values — arrays and objects are not valid status values.
        if (typeof statusVal === 'string') {
            const trimmed = statusVal.trim();
            rawStatus = trimmed.length > 0 ? trimmed : null;
        }
    }
    catch {
        rawStatus = null;
    }
    if (!rawStatus) {
        return missingResult(runtime, phaseArg);
    }
    // gaps_found takes priority over stale — gap closure is the correct next
    // step regardless of whether summaries are newer than the verification file.
    if (rawStatus === 'gaps_found') {
        const entry = VERIFICATION_ROUTING_TABLE['gaps_found'];
        return {
            status: entry.status,
            next_action: entry.next_action,
            next_command: projectNextCommand('plan-phase', runtime, `${phaseArg} --gaps`),
        };
    }
    const staleCheck = findStaleVerificationSummary(phaseDir, fsImpl, phaseCleanCommitTimesMs);
    if (staleCheck.determined && staleCheck.stale) {
        const entry = VERIFICATION_ROUTING_TABLE['stale'];
        return {
            status: entry.status,
            next_action: entry.next_action,
            next_command: projectNextCommand('verify-work', runtime, phaseArg),
        };
    }
    // staleCheck is either {determined:true, stale:false} (checked; nothing
    // stale) or {determined:false} (could not check — fs/scan/clock failure).
    // Both fall through to normal routing below (the pre-existing no-throw
    // fail-open contract is unchanged), but the indeterminate case is flagged
    // on the returned result so a caller can tell the two apart (#3057 B3).
    const staleCheckIndeterminate = !staleCheck.determined;
    // 3. Route — exclude internal sentinels from raw-file lookup (they are
    // constructed internally above, never written by the verifier).
    if (rawStatus in VERIFICATION_ROUTING_TABLE &&
        rawStatus !== 'missing' &&
        rawStatus !== 'unknown' &&
        rawStatus !== 'stale' &&
        rawStatus !== 'gaps_found') {
        const entry = VERIFICATION_ROUTING_TABLE[rawStatus];
        return {
            status: entry.status,
            next_action: entry.next_action,
            next_command: projectNextCommand(entry.next_command, runtime, phaseArg),
            ...(staleCheckIndeterminate ? { staleCheckIndeterminate: true } : {}),
        };
    }
    // Unknown value
    const unknownRoute = VERIFICATION_ROUTING_TABLE['unknown'];
    return {
        status: unknownRoute.status,
        next_action: `Unexpected verification status '${rawStatus}'. Re-run execute-phase verification.`,
        next_command: projectNextCommand(unknownRoute.next_command, runtime, phaseArg),
        ...(staleCheckIndeterminate ? { staleCheckIndeterminate: true } : {}),
    };
}
/**
 * CLI command handler: resolve phaseDir against cwd, call readVerificationStatus,
 * emit via io.output().
 *
 * @param cwd         - Current working directory (used to resolve phaseDirArg).
 * @param phaseDirArg - Phase directory path (absolute or relative to cwd).
 * @param raw         - Whether to emit raw (non-JSON) output.
 */
function cmdVerificationStatus(cwd, phaseDirArg, raw) {
    if (!phaseDirArg) {
        error('phase directory required for verification.status');
        return;
    }
    const phaseDir = node_path_1.default.resolve(cwd, phaseDirArg);
    const result = readVerificationStatus(phaseDir, { runtime: (0, runtime_slash_cjs_1.resolveRuntime)(cwd) });
    output(result, raw);
}
module.exports = {
    VERIFIER_STATUSES,
    VERIFICATION_ROUTING_TABLE,
    defaultPhaseCleanCommitTimesMs,
    findStaleVerificationSummary,
    readVerificationStatus,
    cmdVerificationStatus,
};
