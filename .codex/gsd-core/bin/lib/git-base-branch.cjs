"use strict";
/**
 * Git Base-Branch Resolver — issue #1146.
 *
 * Single source of truth for detecting the repository's default branch.
 * Replaces the duplicated per-workflow bash detection that only consulted
 * `refs/remotes/origin/HEAD` then hardcoded `:-main`, which silently
 * returned "main" for repos whose default branch is "master" whenever
 * origin/HEAD was unset (git init + remote add / fetch without set-head /
 * most CI checkouts / many worktrees).
 *
 * Precedence ladder (highest to lowest):
 *   1. `git.base_branch` config override from .planning/config.json
 *   2. `git symbolic-ref --short refs/remotes/origin/HEAD`  (fast, no network)
 *   3. `git remote show origin` HEAD branch  ← AUTHORITATIVE; works when #2 unset
 *   4. Local branch existence: "master" present + "main" absent → "master";
 *      "main" present → "main"
 *   5. "main"  (last-resort default)
 *
 * Every git subprocess is bounded with a timeout (≤ 30 s); on timeout/error
 * the resolver degrades gracefully to the next tier — it never throws. Tier 5
 * is reachable two ways that `resolveBaseBranch()` alone cannot tell apart: a
 * repository that genuinely has no candidate branch (every git query on tiers
 * 2-4 completed and cleanly answered "nothing"), or a total resolution
 * failure (some query timed out / could not run). `resolveBaseBranchDiagnostics()`
 * distinguishes the two via `verified`; `cmdGitBaseBranch` surfaces the
 * unverified case as a stderr diagnostic without changing its stdout contract
 * (#3057 B4).
 *
 * Pure/testable: all I/O is injectable via the `deps` argument so unit
 * tests can run without touching the real filesystem or spawning real git.
 */
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.readConfigBaseBranch = readConfigBaseBranch;
exports.trySymbolicRef = trySymbolicRef;
exports.tryRemoteShow = tryRemoteShow;
exports.tryLocalBranch = tryLocalBranch;
exports.resolveBaseBranchDiagnostics = resolveBaseBranchDiagnostics;
exports.resolveBaseBranch = resolveBaseBranch;
exports.gitWorktreeInfoInternal = gitWorktreeInfoInternal;
exports.cmdGitBaseBranch = cmdGitBaseBranch;
const node_fs_1 = __importDefault(require("node:fs"));
const node_path_1 = __importDefault(require("node:path"));
const shell_command_projection_cjs_1 = require("./shell-command-projection.cjs");
// ─── Helpers ──────────────────────────────────────────────────────────────────
/**
 * Safely look up `git.base_branch` from the project's config.json.
 * Returns the configured value (a non-empty, non-null string) or null.
 */
function readConfigBaseBranch(planningDir, deps) {
    const readFile = deps?.readFile ??
        ((p) => { try {
            return node_fs_1.default.readFileSync(p, 'utf8');
        }
        catch {
            return null;
        } });
    const configPath = node_path_1.default.join(planningDir, 'config.json');
    const raw = readFile(configPath);
    if (!raw)
        return null;
    let cfg;
    try {
        cfg = JSON.parse(raw);
    }
    catch {
        return null;
    }
    if (!cfg || typeof cfg !== 'object' || Array.isArray(cfg))
        return null;
    const top = cfg;
    // Support both "git.base_branch" (nested) and "base_branch" (flat legacy)
    const gitSection = top.git;
    if (gitSection && typeof gitSection === 'object' && !Array.isArray(gitSection)) {
        const nested = gitSection.base_branch;
        if (typeof nested === 'string' && nested.trim())
            return nested.trim();
    }
    const flat = top.base_branch;
    if (typeof flat === 'string' && flat.trim())
        return flat.trim();
    return null;
}
/**
 * Try `git symbolic-ref --short refs/remotes/origin/HEAD` (no network).
 * Strips the `origin/` prefix to return just the branch name.
 * Returns null if unset or on error/timeout.
 */
function trySymbolicRef(cwd, execGit) {
    try {
        const r = execGit(['symbolic-ref', '--quiet', '--short', 'refs/remotes/origin/HEAD'], { cwd, timeout: 5_000 });
        if (r.exitCode !== 0 || !r.stdout)
            return null;
        // Output is e.g. "origin/main" — strip the prefix
        const branch = r.stdout.trim().replace(/^origin\//, '');
        return branch || null;
    }
    catch {
        return null;
    }
}
/**
 * Try `git remote show origin` to read the HEAD branch.
 * This is authoritative when origin/HEAD is unset locally.
 * Requires network access but succeeds in the common CI case where
 * origin/HEAD was never set after `git init && git remote add origin`.
 *
 * Parses the line:  `HEAD branch: <name>`
 * Returns null on error, timeout, or if the output is malformed.
 */
function tryRemoteShow(cwd, execGit) {
    try {
        const r = execGit(['remote', 'show', 'origin'], { cwd, timeout: 15_000 });
        if (r.exitCode !== 0 || !r.stdout)
            return null;
        // The line looks like: "  HEAD branch: master"
        const m = r.stdout.match(/^\s*HEAD branch:\s*(\S+)\s*$/m);
        if (!m)
            return null;
        const branch = m[1];
        // git emits "(unknown)" when the remote is offline but the local cache
        // resolved it; treat that as non-authoritative and fall through.
        // No `!branch ||` guard: m[1] comes from the `(\S+)` capture group above, so it is never empty.
        if (branch === '(unknown)')
            return null;
        return branch;
    }
    catch {
        return null;
    }
}
/**
 * Detect local branch existence as a tie-breaker when no remote info is available.
 *
 * Rules:
 *   - "master" present AND "main" absent → "master"
 *   - "main" present → "main"
 *   - Neither → null (fall through to default)
 *
 * Returns null on error/timeout.
 */
function tryLocalBranch(cwd, execGit) {
    try {
        const r = execGit(['branch', '--list', 'main', 'master'], { cwd, timeout: 5_000 });
        if (r.exitCode !== 0 || !r.stdout)
            return null;
        // `git branch --list main master` outputs one line per matching branch
        const lines = r.stdout.split('\n').map(l => l.trim().replace(/^\*\s*/, ''));
        const hasMain = lines.includes('main');
        const hasMaster = lines.includes('master');
        if (hasMaster && !hasMain)
            return 'master';
        if (hasMain)
            return 'main';
        return null;
    }
    catch {
        return null;
    }
}
/**
 * Resolve the default/base branch for the repository at `cwd`, along with
 * whether the tier-5 last-resort default (if reached) was verified.
 *
 * Consults the full precedence ladder and always returns a non-empty string.
 * Never throws.
 */
function resolveBaseBranchDiagnostics(cwd, deps) {
    const rawExecGit = deps?.execGit ?? shell_command_projection_cjs_1.execGit;
    // A genuine execGit failure (timeout, or the call could not even spawn —
    // e.g. git missing, surfaced as exitCode 127 with `error` set) is distinct
    // from git completing and cleanly reporting a negative answer (non-zero
    // exit with no useful output, or exit 0 with empty stdout). Only the former
    // means a tier's answer was never actually obtained. Wrapping execGit here
    // observes every tier's calls uniformly without changing trySymbolicRef /
    // tryRemoteShow / tryLocalBranch's own return contracts.
    let anyGitFailure = false;
    const execGit = (args, opts) => {
        const r = rawExecGit(args, opts);
        if (r.timedOut || r.error)
            anyGitFailure = true;
        return r;
    };
    // Derive .planning dir relative to cwd (mirrors planningDir() in planning-workspace.cjs)
    const planningDir = node_path_1.default.join(cwd, '.planning');
    // 1. Config override
    const configured = readConfigBaseBranch(planningDir, deps);
    if (configured)
        return { branch: configured, verified: true };
    // 2. symbolic-ref (fast, no network)
    const symref = trySymbolicRef(cwd, execGit);
    if (symref)
        return { branch: symref, verified: true };
    // 3. git remote show origin (authoritative when origin/HEAD unset)
    const remoteShow = tryRemoteShow(cwd, execGit);
    if (remoteShow)
        return { branch: remoteShow, verified: true };
    // 4. Local branch existence
    const local = tryLocalBranch(cwd, execGit);
    if (local)
        return { branch: local, verified: true };
    // 5. Last-resort default. `verified:false` when at least one tier-2/3/4
    // execGit call timed out or failed to run — the default was never actually
    // checked against this repository, it is just what's left after git could
    // not answer (#3057 B4).
    return { branch: 'main', verified: !anyGitFailure };
}
/**
 * Resolve the default/base branch for the repository at `cwd`.
 *
 * Consults the full precedence ladder and always returns a non-empty string.
 * Never throws. See {@link resolveBaseBranchDiagnostics} for a caller that
 * needs to distinguish a verified answer from an unverified fallback.
 */
function resolveBaseBranch(cwd, deps) {
    return resolveBaseBranchDiagnostics(cwd, deps).branch;
}
/**
 * Detect whether `cwd` sits inside a git worktree, and if so, return the
 * absolute path of the worktree root.
 */
function gitWorktreeInfoInternal(cwd, deps) {
    const execGit = deps?.execGit ?? shell_command_projection_cjs_1.execGit;
    try {
        const insideResult = execGit(['rev-parse', '--is-inside-work-tree'], { cwd, timeout: 5000 });
        if (insideResult.exitCode !== 0) {
            return { inside: false, worktreeRoot: null };
        }
        const insideStdout = String(insideResult.stdout || '').trim();
        if (insideStdout !== 'true') {
            return { inside: false, worktreeRoot: null };
        }
        const rootResult = execGit(['rev-parse', '--show-toplevel'], { cwd, timeout: 5000 });
        if (rootResult.exitCode !== 0) {
            return { inside: true, worktreeRoot: null };
        }
        const root = String(rootResult.stdout || '').trim();
        return { inside: true, worktreeRoot: root || null };
    }
    catch {
        return { inside: false, worktreeRoot: null };
    }
}
// ─── CLI entry point ──────────────────────────────────────────────────────────
/**
 * CLI command: `gsd-tools git base-branch`
 * Resolves the default branch and writes it to stdout (raw string, newline-terminated).
 * Called by workflows via `gsd_run query git.base-branch`.
 */
function cmdGitBaseBranch(cwd, _args, deps) {
    const { branch, verified } = resolveBaseBranchDiagnostics(cwd, deps);
    if (!verified) {
        const writeDiagnostic = deps?.writeDiagnostic ?? ((s) => process.stderr.write(s));
        writeDiagnostic(`⚠ git-base-branch: defaulted to 'main' WITHOUT verifying against this repository — ` +
            `a git query timed out or could not run. See #3057.\n`);
    }
    const write = deps?.write ?? ((s) => process.stdout.write(s));
    write(branch + '\n');
    return branch;
}
