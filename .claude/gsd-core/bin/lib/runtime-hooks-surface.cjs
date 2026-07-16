'use strict';
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
/**
 * Runtime Hooks Surface Module — hook-surface writer functions extracted from
 * bin/install.js (ADR-857 phase 5f-1).
 *
 * Owns the lifecycle writer functions for hook surfaces managed by GSD on four
 * runtimes:
 *   Cline:   writeClineArtifacts + supporting helpers/constants
 *   Cursor:  buildCursorHookEntry, isManagedCursorHookEntry,
 *            reconcileCursorHooksJson, writeCursorHooksJson, removeCursorHooksJson
 *   Copilot: buildCopilotHookConfig, writeCopilotHookConfig
 *   Codex hooks.json: ensureCodexHooksJsonSessionStart, ensureCodexHooksJsonEvent,
 *            reconcileCodexHooksJsonEvent, reconcileCodexHooksJsonSessionStart,
 *            removeCodexHooksJsonEvent, removeCodexHooksJsonSessionStart,
 *            buildCodexHookWindowsShimIR, buildCodexHookBlock, rewriteLegacyCodexHookBlock
 *   Shared:  buildHookCommand, rewriteLegacyManagedNodeHookCommands
 *
 * BEHAVIOR-PRESERVING RELOCATION: all logic is copied verbatim from
 * bin/install.js. No behavior change, no descriptor reads, no new IO.
 *
 * bin/install.js re-exports every symbol from this module so existing
 * consumers that do require('../bin/install.js').writeCursorHooksJson
 * (etc.) continue to work unchanged.
 */
const node_fs_1 = __importDefault(require("node:fs"));
const node_path_1 = __importDefault(require("node:path"));
const node_os_1 = __importDefault(require("node:os"));
const imperative_hook_bus_cjs_1 = require("./host-integration-adapters/imperative-hook-bus.cjs");
// eslint-disable-next-line @typescript-eslint/no-require-imports
const shellCmdProjection = require("./shell-command-projection.cjs");
const { isManagedHookBasename, isManagedHookCommand, projectLegacySettingsHookCommand, projectManagedHookCommand, projectPortableHookBaseDir, projectCodexHookTomlCommand, shellHookOmitsBashRunner, escapeTomlDoubleQuotedString, } = shellCmdProjection;
// ---------------------------------------------------------------------------
// Terminal color constants (mirrors install.js for console output parity)
// ---------------------------------------------------------------------------
const green = '\x1b[32m';
const yellow = '\x1b[33m';
const reset = '\x1b[0m';
// ---------------------------------------------------------------------------
// Codex config.toml constants (subset needed by this module)
// ---------------------------------------------------------------------------
// ---------------------------------------------------------------------------
// Copilot hook constants
// ---------------------------------------------------------------------------
const GSD_COPILOT_HOOK_FILE = 'gsd-session.json';
const GSD_COPILOT_SESSION_MSG_PRESENT = 'GSD: .planning/STATE.md present - review the current phase and any blockers before acting.';
const GSD_COPILOT_SESSION_MSG_ABSENT = 'GSD: no .planning/ workflow found - run /gsd-new-project to start a tracked workflow.';
const GSD_COPILOT_SESSION_HOOK_BASH = 'if [ -f .planning/STATE.md ]; then ' +
    `printf '%s' '{"additionalContext":"${GSD_COPILOT_SESSION_MSG_PRESENT}"}'; else ` +
    `printf '%s' '{"additionalContext":"${GSD_COPILOT_SESSION_MSG_ABSENT}"}'; fi`;
const GSD_COPILOT_SESSION_HOOK_PWSH = 'if (Test-Path .planning/STATE.md) ' +
    `{ '{"additionalContext":"${GSD_COPILOT_SESSION_MSG_PRESENT}"}' } ` +
    `else { '{"additionalContext":"${GSD_COPILOT_SESSION_MSG_ABSENT}"}' }`;
// #2099 UPGRADE 1: multi-event hook bus. Each additional event is a static,
// deterministic advisory (no branching/no-op-style, matching sessionStart's
// tone) so the emitted hooks/gsd-session.json stays golden-trackable — no
// node-runner invocation, no filesystem probing beyond what sessionStart
// already does.
const GSD_COPILOT_PRE_TOOL_MSG = 'GSD: confirm this tool use is in scope for the active phase before proceeding.';
const GSD_COPILOT_PRE_TOOL_HOOK_BASH = `printf '%s' '{"additionalContext":"${GSD_COPILOT_PRE_TOOL_MSG}"}'`;
const GSD_COPILOT_PRE_TOOL_HOOK_PWSH = `'{"additionalContext":"${GSD_COPILOT_PRE_TOOL_MSG}"}'`;
const GSD_COPILOT_POST_TOOL_MSG = 'GSD: review the tool result against the active phase before continuing.';
const GSD_COPILOT_POST_TOOL_HOOK_BASH = `printf '%s' '{"additionalContext":"${GSD_COPILOT_POST_TOOL_MSG}"}'`;
const GSD_COPILOT_POST_TOOL_HOOK_PWSH = `'{"additionalContext":"${GSD_COPILOT_POST_TOOL_MSG}"}'`;
const GSD_COPILOT_PROMPT_SUBMIT_MSG = 'GSD: check this request against .planning/STATE.md scope before acting.';
const GSD_COPILOT_PROMPT_SUBMIT_HOOK_BASH = `printf '%s' '{"additionalContext":"${GSD_COPILOT_PROMPT_SUBMIT_MSG}"}'`;
const GSD_COPILOT_PROMPT_SUBMIT_HOOK_PWSH = `'{"additionalContext":"${GSD_COPILOT_PROMPT_SUBMIT_MSG}"}'`;
const GSD_COPILOT_SESSION_END_MSG = 'GSD: update .planning/STATE.md with the session outcome before ending.';
const GSD_COPILOT_SESSION_END_HOOK_BASH = `printf '%s' '{"additionalContext":"${GSD_COPILOT_SESSION_END_MSG}"}'`;
const GSD_COPILOT_SESSION_END_HOOK_PWSH = `'{"additionalContext":"${GSD_COPILOT_SESSION_END_MSG}"}'`;
// ---------------------------------------------------------------------------
// Cursor hook constants
// ---------------------------------------------------------------------------
const GSD_CURSOR_SESSION_HOOK_SCRIPT = 'gsd-cursor-session-start.js';
const GSD_CURSOR_POST_TOOL_HOOK_SCRIPT = 'gsd-cursor-post-tool.js';
const GSD_CURSOR_PRE_TOOL_HOOK_SCRIPT = 'gsd-cursor-pre-tool.js';
const GSD_CURSOR_STOP_HOOK_SCRIPT = 'gsd-cursor-stop.js';
const GSD_CURSOR_SUBAGENT_START_HOOK_SCRIPT = 'gsd-cursor-subagent-start.js';
const GSD_CURSOR_SUBAGENT_STOP_HOOK_SCRIPT = 'gsd-cursor-subagent-stop.js';
const GSD_CURSOR_HOOK_MARKER = 'gsd-managed';
// The full set of Cursor hook events GSD manages — sourced from the adapter
// (src/host-integration-adapters/imperative-hook-bus.cts) so the vocabulary
// stays closed and first-party. Used by reconcileCursorHooksJson (the
// reconciliation scope is always the full set). The install path
// (writeCursorHooksJson) resolves a descriptor-driven subset via
// resolveManagedHookEvents(opts.managedHookEvents).
const CURSOR_MANAGED_EVENTS = imperative_hook_bus_cjs_1.CURSOR_HOOK_EVENTS;
// ---------------------------------------------------------------------------
// Cline / AGENTS.md constants
// ---------------------------------------------------------------------------
const GSD_AGENTS_MD_MARKER = '<!-- GSD Configuration — managed by gsd-core installer -->';
const GSD_AGENTS_MD_CLOSE_MARKER = '<!-- End GSD Configuration -->';
// ---------------------------------------------------------------------------
// Descriptor-driven runtime title lookup (ADR-1239 / #2092)
// ---------------------------------------------------------------------------
/**
 * Console-log label for a runtime, sourced from the capability registry's
 * `title` field (capabilities/<runtime>/capability.json). Folded from a
 * hardcoded `runtime === 'qwen' ? 'Qwen Code' : runtime === 'claude' ?
 * 'Claude Code' : runtime` ternary — cosmetic (log text) only, but resolves
 * to the same 'Qwen Code' / 'Claude Code' values for those two runtimes.
 * Falls back to the raw runtime id if the registry can't be loaded or the
 * runtime has no title.
 */
function _capabilityTitle(runtime) {
    try {
        // eslint-disable-next-line @typescript-eslint/no-require-imports
        const reg = require('./capability-registry.cjs');
        return reg?.runtimes?.[runtime]?.title || runtime;
    }
    catch {
        return runtime;
    }
}
// ---------------------------------------------------------------------------
// atomicWriteFileSync — shared canonical implementation.
//
// __atomicWrittenTmps is exported so bin/install.js can merge it into its
// _cleanTmpFiles() scan, ensuring that atomic writes performed by this
// module (Cursor hooks.json, Codex hooks.json shims) participate in the
// same temp-file cleanup as writes performed directly by install.js.
//
// Every temp path written is recorded in the Set so _cleanTmpFiles() can
// scope cleanup to files this installer process actually created, avoiding
// accidental deletion of unrelated tools' temp files.
// ---------------------------------------------------------------------------
let __atomicWriteCounter = 0;
// Set<string> — absolute paths of .tmp-<pid>-<n> files this process created.
const __atomicWrittenTmps = new Set();
function atomicWriteFileSync(target, data, options) {
    __atomicWriteCounter += 1;
    const tmp = `${target}.tmp-${process.pid}-${__atomicWriteCounter}`;
    __atomicWrittenTmps.add(tmp);
    try {
        node_fs_1.default.writeFileSync(tmp, data, options);
        shellCmdProjection.retryRenameSync(tmp, target);
        // Successful rename: the tmp path no longer exists, but leave it in the
        // Set so _cleanTmpFiles can recognise it as installer-owned if it somehow
        // lingers (e.g. a rename succeeded but left a stale entry on some FS).
    }
    catch (e) {
        try {
            node_fs_1.default.rmSync(tmp, { force: true });
        }
        catch { /* ignore */ }
        throw e;
    }
}
// ---------------------------------------------------------------------------
// parseTomlValue + findMultilineBasicStringClose
// (needed by rewriteLegacyCodexHookBlock — pure TOML helpers, no state)
// ---------------------------------------------------------------------------
function findMultilineBasicStringClose(line, startIndex) {
    let i = startIndex;
    while (i < line.length) {
        if (line.startsWith('"""', i) && (i === 0 || line[i - 1] !== '\\')) {
            return i;
        }
        i += 1;
    }
    return -1;
}
function parseTomlValue(text, i) {
    // Skip leading whitespace.
    while (i < text.length && (text[i] === ' ' || text[i] === '\t')) {
        i += 1;
    }
    if (i >= text.length) {
        throw new Error('expected value, got end of input');
    }
    const ch = text[i];
    // Basic string
    if (ch === '"') {
        if (text.startsWith('"""', i)) {
            const close = findMultilineBasicStringClose(text, i + 3);
            if (close === -1) {
                throw new Error('unterminated multi-line basic string');
            }
            const raw = text.slice(i + 3, close);
            return { value: raw.replace(/^\r?\n/, ''), end: close + 3 };
        }
        let j = i + 1;
        let out = '';
        while (j < text.length) {
            const c = text[j];
            if (c === '\\') {
                const next = text[j + 1];
                if (next === 'n') {
                    out += '\n';
                    j += 2;
                    continue;
                }
                if (next === 't') {
                    out += '\t';
                    j += 2;
                    continue;
                }
                if (next === 'r') {
                    out += '\r';
                    j += 2;
                    continue;
                }
                if (next === '\\') {
                    out += '\\';
                    j += 2;
                    continue;
                }
                if (next === '"') {
                    out += '"';
                    j += 2;
                    continue;
                }
                if (next === '/') {
                    out += '/';
                    j += 2;
                    continue;
                }
                out += next === undefined ? '' : next;
                j += 2;
                continue;
            }
            if (c === '"') {
                return { value: out, end: j + 1 };
            }
            out += c;
            j += 1;
        }
        throw new Error('unterminated basic string');
    }
    // Literal string
    if (ch === '\'') {
        if (text.startsWith("'''", i)) {
            const close = text.indexOf("'''", i + 3);
            if (close === -1)
                throw new Error('unterminated multi-line literal string');
            return { value: text.slice(i + 3, close).replace(/^\r?\n/, ''), end: close + 3 };
        }
        const close = text.indexOf('\'', i + 1);
        if (close === -1)
            throw new Error('unterminated literal string');
        return { value: text.slice(i + 1, close), end: close + 1 };
    }
    // Boolean
    if (text.startsWith('true', i))
        return { value: true, end: i + 4 };
    if (text.startsWith('false', i))
        return { value: false, end: i + 5 };
    // Number (integer or float, simplified)
    const numMatch = text.slice(i).match(/^[+-]?(?:0x[0-9a-fA-F_]+|0o[0-7_]+|0b[01_]+|[0-9][0-9_]*(?:\.[0-9_]+)?(?:[eE][+-]?[0-9_]+)?|inf|nan)/);
    if (numMatch) {
        const raw = numMatch[0];
        const cleaned = raw.replace(/_/g, '');
        const num = Number(cleaned);
        return { value: isNaN(num) ? cleaned : num, end: i + raw.length };
    }
    // Datetime (simplified passthrough)
    const dtMatch = text.slice(i).match(/^\d{4}-\d{2}-\d{2}(?:[T ]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?)?/);
    if (dtMatch) {
        return { value: dtMatch[0], end: i + dtMatch[0].length };
    }
    throw new Error(`parseTomlValue: unexpected character '${ch}' at position ${i}`);
}
function normalizeNodePath(execPath, opts) {
    if (!execPath)
        return execPath;
    const env = (opts && opts.env) || process.env;
    const existsSync = (opts && opts.existsSync) || node_fs_1.default.existsSync;
    const normalizedForMatch = shellCmdProjection.posixNormalize(execPath);
    if (/\/fnm_multishells\/[0-9]+_[0-9]+\/node(\.exe)?$/i.test(normalizedForMatch)) {
        const candidates = [];
        if (env.FNM_DIR) {
            candidates.push(`${env.FNM_DIR}/aliases/default/node.exe`);
            candidates.push(`${env.FNM_DIR}/aliases/default/bin/node`);
        }
        if (env.APPDATA) {
            candidates.push(`${env.APPDATA}/fnm/aliases/default/node.exe`);
        }
        for (const candidate of candidates) {
            if (candidate && existsSync(candidate))
                return candidate;
        }
        return execPath;
    }
    // Homebrew (macOS Intel /usr/local, Apple Silicon /opt/homebrew, Linuxbrew
    // /home/linuxbrew/.linuxbrew, and any custom HOMEBREW_PREFIX) pins node at
    // <prefix>/Cellar/node(<@ver>)?/<ver>/bin/node, then deletes prior versions on
    // `brew upgrade node`. Rewrite to the stable <prefix>/bin/node symlink, which
    // survives the upgrade. Derive <prefix> from the path itself (more reliable
    // than HOMEBREW_PREFIX env — the path IS the install location) so every layout
    // is covered by one branch instead of one per known prefix (#2185).
    const homebrewMatch = normalizedForMatch.match(/^(.+)\/Cellar\/node(@\d+)?\/[^/]+\/bin\/node(\.exe)?$/i);
    if (homebrewMatch) {
        return `${homebrewMatch[1]}/bin/node${homebrewMatch[3] || ''}`;
    }
    // mise pins a concrete node version at <data>/installs/node/<ver>/bin/node
    // (Windows: <data>/installs/node/<ver>/node.exe). Node realpaths
    // process.execPath to that versioned path, and `mise up` prunes old versions,
    // so a baked hook command 404s after any node bump — the same ephemeral-path
    // failure #977 fixed for fnm. The stable alias is the sibling shim
    // (<data>/shims/node), which always resolves to the active version, like the
    // Homebrew symlink survives `brew upgrade node`. Derive <data> from execPath
    // so a custom MISE_DATA_DIR layout still works, and only rewrite when the shim
    // exists — otherwise fall back to the raw execPath unchanged.
    const miseMatch = normalizedForMatch.match(/^(.*)\/installs\/node\/[^/]+\/(?:bin\/)?node(\.exe)?$/);
    if (miseMatch) {
        const shim = `${miseMatch[1]}/shims/node${miseMatch[2] || ''}`;
        if (existsSync(shim))
            return shim;
    }
    return execPath;
}
function resolveNodeRunner(opts) {
    const execPath = typeof process.execPath === 'string' ? process.execPath : '';
    if (!execPath)
        return null;
    const stablePath = normalizeNodePath(execPath, opts);
    return JSON.stringify(shellCmdProjection.posixNormalize(stablePath));
}
function resolveBashRunner(opts) {
    const platform = (opts && opts.platform) || process.platform;
    if (platform !== 'win32')
        return 'bash';
    const env = (opts && opts.env) || process.env;
    const exists = (opts && opts.existsSync) || node_fs_1.default.existsSync;
    const candidates = [];
    if (env.GSD_BASH_PATH)
        candidates.push(env.GSD_BASH_PATH);
    if (env.ProgramFiles)
        candidates.push(node_path_1.default.win32.join(env.ProgramFiles, 'Git', 'bin', 'bash.exe'));
    if (env['ProgramFiles(x86)'])
        candidates.push(node_path_1.default.win32.join(env['ProgramFiles(x86)'], 'Git', 'bin', 'bash.exe'));
    if (env.SystemDrive) {
        candidates.push(node_path_1.default.win32.join(env.SystemDrive, 'Program Files', 'Git', 'bin', 'bash.exe'));
        candidates.push(node_path_1.default.win32.join(env.SystemDrive, 'Program Files (x86)', 'Git', 'bin', 'bash.exe'));
    }
    for (const candidate of candidates) {
        if (candidate && exists(candidate)) {
            return JSON.stringify(shellCmdProjection.posixNormalize(candidate));
        }
    }
    return null;
}
function rewriteLegacyManagedNodeHookCommands(settings, absoluteRunner, opts) {
    if (!settings || !settings.hooks || !absoluteRunner)
        return false;
    if (!opts)
        opts = {};
    const platform = opts.platform || process.platform;
    let changed = false;
    for (const entries of Object.values(settings.hooks)) {
        if (!Array.isArray(entries))
            continue;
        for (const entry of entries) {
            if (!entry || !Array.isArray(entry.hooks))
                continue;
            for (const h of entry.hooks) {
                if (!h || typeof h.command !== 'string')
                    continue;
                if (Array.isArray(h.args) && h.args.length > 0)
                    continue;
                let trimmed = h.command.trim();
                const hadPowerShellCallOperator = platform === 'win32' && /^&\s+/.test(trimmed);
                if (hadPowerShellCallOperator) {
                    trimmed = trimmed.replace(/^&\s+/, '').trim();
                }
                const m = trimmed.match(/^node\s+("([^"]+)"|'([^']+)'|(\S+))\s*$/) ||
                    trimmed.match(/^("([^"]+)"|'([^']+)'|(\S+))\s+("([^"]+)"|'([^']+)'|(\S+))\s*$/);
                if (!m)
                    continue;
                let _runnerToken, scriptToken, scriptPath;
                if (/^node\s+/.test(trimmed)) {
                    _runnerToken = 'node';
                    scriptToken = m[1];
                    scriptPath = m[2] || m[3] || m[4] || '';
                }
                else {
                    _runnerToken = m[1];
                    const runnerPath = shellCmdProjection.posixNormalize(m[2] || m[3] || m[4] || '');
                    const stableRunner = normalizeNodePath(runnerPath);
                    if (stableRunner === runnerPath && platform !== 'win32')
                        continue;
                    scriptToken = m[5];
                    scriptPath = m[6] || m[7] || m[8] || '';
                }
                if (!isManagedHookBasename(scriptPath, { surface: 'settings-json' }))
                    continue;
                const projectedCommand = projectLegacySettingsHookCommand({
                    absoluteRunner,
                    scriptPath,
                    scriptToken,
                    runtime: opts.runtime || 'generic',
                    platform,
                });
                if (!projectedCommand)
                    continue;
                if (h.command === projectedCommand)
                    continue;
                h.command = projectedCommand;
                changed = true;
            }
        }
    }
    return changed;
}
function buildCodexHookBlock(targetDir, opts) {
    const absoluteRunner = opts && opts.absoluteRunner;
    if (!absoluteRunner)
        return null;
    const eol = (opts && opts.eol) || '\n';
    const platform = (opts && opts.platform) || process.platform;
    const updateCheckScript = node_path_1.default.resolve(targetDir, 'hooks', 'gsd-check-update.js');
    const commandValue = projectCodexHookTomlCommand({
        absoluteRunner,
        scriptPath: updateCheckScript,
        platform,
    });
    return `${eol}# GSD Hooks${eol}` +
        `[[hooks.SessionStart]]${eol}` +
        `${eol}` +
        `[[hooks.SessionStart.hooks]]${eol}` +
        `type = "command"${eol}` +
        `command = "${commandValue}"${eol}`;
}
function rewriteLegacyCodexHookBlock(content, absoluteRunner, opts) {
    if (!content || !absoluteRunner)
        return { content, changed: false };
    const platform = (opts && opts.platform) || process.platform;
    let changed = false;
    const updated = content.replace(/^(command\s*=\s*")node\s+((?:\\"[^"]+\\"|\S+))("\s*)$/gm, (full, prefix, scriptToken, suffix) => {
        const quoted = scriptToken.match(/^\\"([\s\S]+)\\"$/);
        let scriptPath = scriptToken;
        if (quoted) {
            try {
                scriptPath = String(parseTomlValue(`"${quoted[1]}"`, 0).value);
            }
            catch {
                scriptPath = quoted[1];
            }
        }
        if (!isManagedHookBasename(scriptPath, { surface: 'codex-toml' }))
            return full;
        const desiredCommand = projectCodexHookTomlCommand({
            absoluteRunner,
            scriptPath,
            platform,
        });
        const currentCommand = `${prefix}${scriptToken}${suffix}`.replace(/^(command\s*=\s*")|("\s*)$/g, '');
        if (currentCommand === desiredCommand)
            return full;
        changed = true;
        return `${prefix}${desiredCommand}${suffix}`;
    });
    return { content: updated, changed };
}
function reconcileCodexHooksJsonEvent(targetDir, eventName, opts = {}) {
    const hooksJsonPath = node_path_1.default.join(targetDir, 'hooks.json');
    const managedCommand = typeof opts.managedCommand === 'string' ? opts.managedCommand : null;
    const commandWindows = typeof opts.commandWindows === 'string' ? opts.commandWindows : null;
    const matcher = typeof opts.matcher === 'string' ? opts.matcher : undefined;
    const timeout = typeof opts.timeout === 'number' ? opts.timeout : undefined;
    let parsed = {};
    let currentContent = null;
    if (node_fs_1.default.existsSync(hooksJsonPath)) {
        const raw = node_fs_1.default.readFileSync(hooksJsonPath, 'utf8');
        currentContent = raw;
        if (raw.trim()) {
            try {
                parsed = JSON.parse(raw);
            }
            catch (err) {
                throw new Error(`hooks.json parse failed: ${err && err.message ? err.message : String(err)}`);
            }
        }
    }
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed))
        parsed = {};
    const usesNestedHooksObject = parsed['hooks'] && typeof parsed['hooks'] === 'object' && !Array.isArray(parsed['hooks']);
    // #1348: canonicalize every write to the nested { hooks: { <Event>: [...] } }
    // shape. Lift ANY top-level event array (legacy, empty, OR mixed nested+top-level)
    // into the nested table — merging when the same event exists in both — so
    // user/legacy entries are preserved under `hooks` and no stray top-level event
    // key survives (Codex deny_unknown_fields rejects them). Mirrors reconcileCursorHooksJson.
    const hookTable = usesNestedHooksObject
        ? parsed['hooks']
        : {};
    for (const key of Object.keys(parsed)) {
        if (key === 'hooks')
            continue;
        if (Array.isArray(parsed[key])) {
            const lifted = parsed[key];
            const existing = Array.isArray(hookTable[key]) ? hookTable[key] : [];
            hookTable[key] = [...lifted, ...existing];
            delete parsed[key];
        }
    }
    parsed['hooks'] = hookTable;
    const eventEntries = Array.isArray(hookTable[eventName]) ? hookTable[eventName] : [];
    let removedLegacy = false;
    const sanitizedEntries = [];
    for (const entry of eventEntries) {
        if (!entry || typeof entry !== 'object' || Array.isArray(entry))
            continue;
        const entryObj = entry;
        const originalHooks = Array.isArray(entryObj['hooks']) ? entryObj['hooks'] : [];
        if (originalHooks.length === 0) {
            sanitizedEntries.push(entry);
            continue;
        }
        const keptHooks = originalHooks.filter((hook) => {
            const cmd = hook && typeof hook === 'object' ? hook['command'] : null;
            const managed = isManagedHookCommand(cmd, {
                surface: 'codex-hooks-json',
                includeLegacyAliases: true,
                configDir: targetDir,
            });
            if (managed)
                removedLegacy = true;
            return !managed;
        });
        if (keptHooks.length === 0)
            continue;
        const nextEntry = { ...entryObj, hooks: keptHooks };
        sanitizedEntries.push(nextEntry);
    }
    if (managedCommand) {
        const hookEntry = { type: 'command', command: managedCommand };
        if (commandWindows)
            hookEntry['commandWindows'] = commandWindows;
        if (timeout !== undefined)
            hookEntry['timeout'] = timeout;
        const newEntry = { hooks: [hookEntry] };
        if (matcher !== undefined)
            newEntry['matcher'] = matcher;
        sanitizedEntries.push(newEntry);
    }
    if (sanitizedEntries.length > 0) {
        hookTable[eventName] = sanitizedEntries;
    }
    else {
        delete hookTable[eventName];
    }
    // Avoid writing an empty `{ "hooks": {} }` artifact (e.g. removal on an absent
    // file): collapse an empty hook table back to `{}` so the existing
    // shouldWrite/no-write-on-empty behavior is preserved.
    if (Object.keys(hookTable).length === 0)
        delete parsed['hooks'];
    const nextContent = `${JSON.stringify(parsed, null, 2)}\n`;
    const changed = currentContent !== nextContent;
    const shouldWrite = changed && (currentContent !== null || Object.keys(parsed).length > 0);
    if (shouldWrite) {
        atomicWriteFileSync(hooksJsonPath, nextContent, 'utf8');
    }
    return { changed: changed || removedLegacy, wrote: shouldWrite, path: hooksJsonPath };
}
function reconcileCodexHooksJsonSessionStart(targetDir, opts = {}) {
    return reconcileCodexHooksJsonEvent(targetDir, 'SessionStart', opts);
}
function buildCodexHookWindowsShimIR(scriptAbsPath, absoluteRunnerToken) {
    if (!absoluteRunnerToken)
        return null;
    let interpreter;
    try {
        interpreter = JSON.parse(absoluteRunnerToken);
    }
    catch {
        interpreter = absoluteRunnerToken;
    }
    const targetAbs = shellCmdProjection.posixNormalize(scriptAbsPath);
    const scriptQuoted = JSON.stringify(targetAbs);
    const cmdPath = scriptAbsPath.replace(/\.js$/, '.cmd');
    const hookCommand = JSON.stringify(shellCmdProjection.posixNormalize(cmdPath));
    const runnerQuoted = JSON.stringify(interpreter);
    return {
        invocation: { interpreter, target: scriptAbsPath },
        cmdPath,
        hookCommand,
        eol: { cmd: '\r\n' },
        passthroughArgs: true,
        render: {
            cmd: () => `@ECHO OFF\r\n@SETLOCAL\r\n@${runnerQuoted} ${scriptQuoted} %*\r\n`,
        },
    };
}
function ensureCodexHooksJsonSessionStart(targetDir, opts = {}) {
    const platform = opts.platform || process.platform;
    const absoluteRunner = opts.absoluteRunner || null;
    const hooksJsonPath = node_path_1.default.join(targetDir, 'hooks.json');
    if (!absoluteRunner)
        return { changed: false, wrote: false, path: hooksJsonPath };
    const scriptPath = shellCmdProjection.posixNormalize(node_path_1.default.resolve(targetDir, 'hooks', 'gsd-check-update.js'));
    const cmdShimPath = scriptPath.replace(/\.js$/, '.cmd');
    let managedCommand;
    if (platform === 'win32') {
        const shimIR = buildCodexHookWindowsShimIR(scriptPath, absoluteRunner);
        if (!shimIR)
            return { changed: false, wrote: false, path: hooksJsonPath };
        try {
            atomicWriteFileSync(shimIR.cmdPath, shimIR.render.cmd(), 'utf8');
        }
        catch (shimWriteErr) {
            const reason = shimWriteErr && shimWriteErr.message ? shimWriteErr.message : String(shimWriteErr);
            console.warn(`  ${yellow}⚠${reset}  Codex Windows hook NOT installed — .cmd shim write failed: ${reason}. ` +
                `Fix the write error (permissions? disk full?) and re-run the installer. ` +
                `Do NOT use the legacy node.exe command path — it triggers the #3426 bash.exe POSIX-exec failure.`);
            return { changed: false, wrote: false, path: hooksJsonPath };
        }
        managedCommand = shimIR.hookCommand;
    }
    else {
        managedCommand = projectManagedHookCommand({
            absoluteRunner,
            scriptPath,
            runtime: 'codex',
            platform,
        }) ?? undefined;
    }
    if (!managedCommand)
        return { changed: false, wrote: false, path: hooksJsonPath };
    const commandWindows = platform === 'win32'
        ? JSON.stringify(shellCmdProjection.posixNormalize(cmdShimPath))
        : undefined;
    return reconcileCodexHooksJsonSessionStart(targetDir, { managedCommand, commandWindows });
}
function ensureCodexHooksJsonEvent(targetDir, eventName, opts = {}) {
    const platform = opts.platform || process.platform;
    const absoluteRunner = opts.absoluteRunner || null;
    const hooksJsonPath = node_path_1.default.join(targetDir, 'hooks.json');
    if (!absoluteRunner)
        return { changed: false, wrote: false, path: hooksJsonPath };
    const scriptPath = shellCmdProjection.posixNormalize(node_path_1.default.resolve(targetDir, 'hooks', 'gsd-context-monitor.js'));
    let managedCommand;
    if (platform === 'win32') {
        const shimIR = buildCodexHookWindowsShimIR(scriptPath, absoluteRunner);
        if (!shimIR)
            return { changed: false, wrote: false, path: hooksJsonPath };
        try {
            atomicWriteFileSync(shimIR.cmdPath, shimIR.render.cmd(), 'utf8');
        }
        catch (shimWriteErr) {
            const reason = shimWriteErr && shimWriteErr.message ? shimWriteErr.message : String(shimWriteErr);
            console.warn(`  ${yellow}⚠${reset}  Codex Windows hook NOT installed — .cmd shim write failed for ${eventName}: ${reason}. ` +
                `Fix the write error (permissions? disk full?) and re-run the installer.`);
            return { changed: false, wrote: false, path: hooksJsonPath };
        }
        managedCommand = shimIR.hookCommand;
    }
    else {
        managedCommand = projectManagedHookCommand({
            absoluteRunner,
            scriptPath,
            runtime: 'codex',
            platform,
        }) ?? undefined;
    }
    if (!managedCommand)
        return { changed: false, wrote: false, path: hooksJsonPath };
    return reconcileCodexHooksJsonEvent(targetDir, eventName, { managedCommand, timeout: 10 });
}
// ---------------------------------------------------------------------------
// removeCodexHooksJsonEvent / removeCodexHooksJsonSessionStart
// ---------------------------------------------------------------------------
function removeCodexHooksJsonEvent(targetDir, eventName) {
    return reconcileCodexHooksJsonEvent(targetDir, eventName, { managedCommand: null });
}
function removeCodexHooksJsonSessionStart(targetDir) {
    return reconcileCodexHooksJsonSessionStart(targetDir, { managedCommand: null });
}
function buildHookCommand(configDir, hookName, opts) {
    if (!opts)
        opts = {};
    const platform = opts.platform || process.platform;
    const runtime = opts.runtime || 'generic';
    const hookShell = opts.hookShell;
    const isShellHook = hookName.endsWith('.sh');
    if (shellHookOmitsBashRunner({ platform, runtime, isShellHook })) {
        if (opts.portableHooks) {
            const portableBaseDir = projectPortableHookBaseDir({
                configDir,
                homeDir: node_os_1.default.homedir(),
            });
            return JSON.stringify(`${portableBaseDir}/hooks/${hookName}`);
        }
        return JSON.stringify(shellCmdProjection.posixNormalize(configDir) + '/hooks/' + hookName);
    }
    const nodeRunner = resolveNodeRunner();
    const runner = isShellHook ? resolveBashRunner(opts) : nodeRunner;
    if (runner === null)
        return null;
    if (opts.portableHooks) {
        const portableBaseDir = projectPortableHookBaseDir({
            configDir,
            homeDir: node_os_1.default.homedir(),
        });
        return projectManagedHookCommand({
            absoluteRunner: runner,
            scriptPath: `${portableBaseDir}/hooks/${hookName}`,
            runtime: opts.runtime || 'generic',
            platform,
            hookShell,
        });
    }
    const hooksPath = shellCmdProjection.posixNormalize(configDir) + '/hooks/' + hookName;
    return projectManagedHookCommand({
        absoluteRunner: runner,
        scriptPath: hooksPath,
        runtime,
        platform,
        hookShell,
    });
}
// ---------------------------------------------------------------------------
// Cline helpers
// ---------------------------------------------------------------------------
function buildClineRulesBody() {
    return [
        '# GSD Core — Git. Ship. Done.',
        '',
        '- GSD workflows live in `gsd-core/workflows/`. Load the relevant workflow when',
        '  the user runs a `/gsd-*` command.',
        '- GSD agents live in `agents/`. Use the matching agent when spawning subagents.',
        '- GSD tools are at `gsd-core/bin/gsd-tools.cjs`. Run with `node`.',
        '- Planning artifacts live in `.planning/`. Never edit them outside a GSD workflow.',
        '- Do not apply GSD workflows unless the user explicitly asks for them.',
        '- When a GSD command triggers a deliverable (feature, fix, docs), offer the next',
        '  step to the user using Cline\'s ask_user tool after completing it.',
    ].join('\n') + '\n';
}
function buildClineAgentsMdBody() {
    return buildClineRulesBody();
}
function buildClinePreToolUseHook() {
    return `#!/usr/bin/env node
'use strict';
/* GSD-managed Cline PreToolUse hook — gsd-core issue #787.
 * Protocol: JSON on stdin -> JSON decision on stdout.
 * Honored fields: { cancel, errorMessage, contextModification }.
 * Fails open: any error allows the operation. */
let raw = '';
process.stdin.setEncoding('utf8');
process.stdin.on('data', (c) => { raw += c; });
process.stdin.on('end', () => {
  const allow = () => process.stdout.write(JSON.stringify({ cancel: false }));
  let input;
  try { input = JSON.parse(raw || '{}'); } catch { return allow(); }
  try {
    const tool = String(
      input.toolName || input.tool_name || input.tool ||
      (input.toolInput && input.toolInput.name) || (input.tool_input && input.tool_input.name) || ''
    ).toLowerCase();
    const isWrite = /write|edit|replace|create|delete|remove|append|apply|patch|insert|mkdir/.test(tool);
    // Collect only PATH-bearing field values (not free-form content), so a doc
    // that merely mentions ".planning/" in its body is never falsely blocked.
    const paths = [];
    const PATH_KEY = /^(path|file|file_?path|filepath|target_?path|target|dir|directory|uri|filename)$/i;
    const walk = (v, depth) => {
      if (depth > 5 || paths.length > 64) return;
      if (Array.isArray(v)) { for (const x of v) walk(x, depth + 1); return; }
      if (v && typeof v === 'object') {
        for (const k of Object.keys(v)) {
          const val = v[k];
          if (typeof val === 'string' && PATH_KEY.test(k)) paths.push(val);
          else walk(val, depth + 1);
        }
      }
    };
    walk(input, 0);
    const isPlanningPath = (s) => /(^|[\\\\/])\\.planning([\\\\/]|$)/.test(s);
    if (isWrite && paths.some(isPlanningPath)) {
      return process.stdout.write(JSON.stringify({
        cancel: true,
        errorMessage:
          'GSD: .planning/ artifacts are managed by GSD workflows. Edit them only through a /gsd-* command, not directly.',
      }));
    }
  } catch { /* fall through to allow */ }
  return allow();
});
`;
}
function mergeGsdAgentsMd(filePath, gsdContent) {
    const gsdBlock = GSD_AGENTS_MD_MARKER + '\n' + gsdContent.trim() + '\n' + GSD_AGENTS_MD_CLOSE_MARKER;
    if (!node_fs_1.default.existsSync(filePath)) {
        node_fs_1.default.mkdirSync(node_path_1.default.dirname(filePath), { recursive: true });
        node_fs_1.default.writeFileSync(filePath, gsdBlock + '\n');
        return;
    }
    const existing = node_fs_1.default.readFileSync(filePath, 'utf8');
    const openIndex = existing.indexOf(GSD_AGENTS_MD_MARKER);
    const closeIndex = existing.indexOf(GSD_AGENTS_MD_CLOSE_MARKER);
    if (openIndex !== -1 && closeIndex !== -1) {
        const before = existing.substring(0, openIndex).trimEnd();
        const after = existing.substring(closeIndex + GSD_AGENTS_MD_CLOSE_MARKER.length).trimStart();
        let newContent = '';
        if (before)
            newContent += before + '\n\n';
        newContent += gsdBlock;
        if (after)
            newContent += '\n\n' + after;
        newContent += '\n';
        node_fs_1.default.writeFileSync(filePath, newContent);
        return;
    }
    node_fs_1.default.writeFileSync(filePath, existing.trimEnd() + '\n\n' + gsdBlock + '\n');
}
// ---------------------------------------------------------------------------
// writeClineArtifacts
// ---------------------------------------------------------------------------
function writeClineArtifacts(targetDir, isGlobalInstall) {
    const written = [];
    const clinerulesDir = node_path_1.default.join(targetDir, '.clinerules');
    try {
        if (node_fs_1.default.existsSync(clinerulesDir)) {
            const st = node_fs_1.default.lstatSync(clinerulesDir);
            if (st.isFile() || st.isSymbolicLink()) {
                node_fs_1.default.unlinkSync(clinerulesDir);
                console.log(`  ${green}✓${reset} Migrated legacy .clinerules to directory form`);
            }
        }
    }
    catch { /* best-effort migration */ }
    node_fs_1.default.mkdirSync(clinerulesDir, { recursive: true });
    node_fs_1.default.writeFileSync(node_path_1.default.join(clinerulesDir, 'gsd.md'), buildClineRulesBody());
    written.push('.clinerules/gsd.md');
    console.log(`  ${green}✓${reset} Wrote .clinerules/gsd.md`);
    const hooksDir = node_path_1.default.join(clinerulesDir, 'hooks');
    node_fs_1.default.mkdirSync(hooksDir, { recursive: true });
    const hookPath = node_path_1.default.join(hooksDir, 'PreToolUse');
    node_fs_1.default.writeFileSync(hookPath, buildClinePreToolUseHook());
    try {
        node_fs_1.default.chmodSync(hookPath, 0o755);
    }
    catch { /* Windows: hooks unsupported anyway */ }
    written.push('.clinerules/hooks/PreToolUse');
    console.log(`  ${green}✓${reset} Wrote .clinerules/hooks/PreToolUse`);
    if (isGlobalInstall) {
        try {
            const agentsPath = node_path_1.default.join(node_os_1.default.homedir(), '.agents', 'AGENTS.md');
            mergeGsdAgentsMd(agentsPath, buildClineAgentsMdBody());
            console.log(`  ${green}✓${reset} Merged GSD instructions into ~/.agents/AGENTS.md`);
        }
        catch (err) {
            console.warn(`  ${yellow}⚠${reset} Could not write ~/.agents/AGENTS.md: ${err.message}`);
        }
    }
    return written;
}
// ---------------------------------------------------------------------------
// Cursor hook functions
// ---------------------------------------------------------------------------
function buildCursorHookEntry(scriptPath) {
    return {
        type: 'command',
        command: shellCmdProjection.posixNormalize(scriptPath),
        [GSD_CURSOR_HOOK_MARKER]: true,
    };
}
function isManagedCursorHookEntry(entry) {
    return Boolean(entry && typeof entry === 'object' && entry[GSD_CURSOR_HOOK_MARKER]);
}
function reconcileCursorHooksJson(hooksJsonPath, managedEntries) {
    let parsed = {};
    let currentContent = null;
    if (node_fs_1.default.existsSync(hooksJsonPath)) {
        const raw = node_fs_1.default.readFileSync(hooksJsonPath, 'utf8');
        currentContent = raw;
        if (raw.trim()) {
            try {
                parsed = JSON.parse(raw);
            }
            catch (err) {
                throw new Error(`Cursor hooks.json parse failed: ${err && err.message ? err.message : String(err)}`);
            }
        }
    }
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed))
        parsed = {};
    const hasNestedHooksObject = parsed['hooks'] && typeof parsed['hooks'] === 'object' && !Array.isArray(parsed['hooks']);
    if (!hasNestedHooksObject) {
        const lifted = {};
        for (const k of CURSOR_MANAGED_EVENTS) {
            if (Array.isArray(parsed[k])) {
                lifted[k] = parsed[k];
                delete parsed[k];
            }
        }
        parsed['hooks'] = lifted;
    }
    if (!parsed['version'])
        parsed['version'] = 1;
    const hookTable = parsed['hooks'];
    const entries = managedEntries || {};
    for (const event of CURSOR_MANAGED_EVENTS) {
        const existing = Array.isArray(hookTable[event]) ? hookTable[event] : [];
        const userOwned = existing.filter((e) => !isManagedCursorHookEntry(e));
        const newEntry = entries[event] || null;
        if (newEntry) {
            hookTable[event] = [...userOwned, newEntry];
        }
        else {
            if (userOwned.length > 0) {
                hookTable[event] = userOwned;
            }
            else {
                delete hookTable[event];
            }
        }
    }
    const nextContent = `${JSON.stringify(parsed, null, 2)}\n`;
    const changed = currentContent !== nextContent;
    const shouldWrite = changed && (currentContent !== null || Object.keys(parsed).length > 0);
    if (shouldWrite) {
        atomicWriteFileSync(hooksJsonPath, nextContent, 'utf8');
    }
    return { changed: changed, wrote: shouldWrite, path: hooksJsonPath };
}
function writeCursorHooksJson(targetDir, src, opts) {
    opts = opts || {};
    const hooksDir = node_path_1.default.join(targetDir, 'hooks');
    node_fs_1.default.mkdirSync(hooksDir, { recursive: true });
    // Descriptor-driven event resolution (#2089): the managed event set comes
    // from the host descriptor's hostBehaviors.managedHookEvents via the pure
    // adapter (resolveManagedHookEvents), NOT a hardcoded constant.
    const events = (0, imperative_hook_bus_cjs_1.resolveManagedHookEvents)(opts.managedHookEvents);
    const hookScripts = (0, imperative_hook_bus_cjs_1.resolveHookScripts)(events);
    const srcHooksDir = node_path_1.default.join(src, 'hooks');
    const installedScripts = new Set();
    for (const script of hookScripts) {
        const srcPath = node_path_1.default.join(srcHooksDir, script);
        const destPath = node_path_1.default.join(hooksDir, script);
        if (node_fs_1.default.existsSync(srcPath)) {
            let content = node_fs_1.default.readFileSync(srcPath, 'utf8');
            content = content.replace(/gsd:/gi, 'gsd-');
            node_fs_1.default.writeFileSync(destPath, content);
            try {
                node_fs_1.default.chmodSync(destPath, 0o755);
            }
            catch { /* Windows: ignore chmod */ }
            installedScripts.add(script);
        }
    }
    const hookOpts = { runtime: 'cursor', platform: opts.platform || process.platform };
    const commands = {};
    for (const ev of events) {
        const script = imperative_hook_bus_cjs_1.CURSOR_EVENT_SCRIPT_MAP[ev];
        if (script && installedScripts.has(script)) {
            commands[ev] = buildHookCommand(targetDir, script, hookOpts);
        }
        else {
            commands[ev] = null;
        }
    }
    const managedEntries = (0, imperative_hook_bus_cjs_1.buildHookBusEntries)(events, commands);
    const hooksJsonPath = node_path_1.default.join(targetDir, 'hooks.json');
    const result = reconcileCursorHooksJson(hooksJsonPath, managedEntries);
    return { hooksJsonPath, changed: result.changed };
}
function removeCursorHooksJson(targetDir) {
    const hooksJsonPath = node_path_1.default.join(targetDir, 'hooks.json');
    if (!node_fs_1.default.existsSync(hooksJsonPath))
        return { changed: false };
    const result = reconcileCursorHooksJson(hooksJsonPath, null);
    if (result.changed) {
        try {
            const contentRaw = node_fs_1.default.readFileSync(hooksJsonPath, 'utf8');
            const parsed = JSON.parse(contentRaw);
            const hookTable = (parsed['hooks'] && typeof parsed['hooks'] === 'object' && !Array.isArray(parsed['hooks']))
                ? parsed['hooks']
                : {};
            const hasAnyEvents = Object.keys(hookTable).some((k) => Array.isArray(hookTable[k]) && hookTable[k].length > 0);
            if (!hasAnyEvents) {
                node_fs_1.default.unlinkSync(hooksJsonPath);
                return { changed: true };
            }
        }
        catch { /* best-effort: leave the file */ }
    }
    return { changed: result.changed };
}
// ---------------------------------------------------------------------------
// Windsurf/Cascade hook functions (ADR-1239 / #2100 Stage 2 — HOOK-BRIDGE)
//
// Cascade (Windsurf's agent) hooks.json format is DISTINCT from Cursor's:
//   { "hooks": { "<event>": [ { "command": "<shell cmd>", ... } ] } }
// Each entry carries a bare `command` STRING (a shell command line) — not
// Cursor's `{ type: 'command', command: <cmd> }` wrapper — and there is no
// top-level `version` field. Docs (reference): https://docs.windsurf.com/llms-full.txt ,
// https://docs.devin.ai/desktop/cascade/hooks
//
// Cascade blocks via EXIT CODE 2 (+ a stderr reason), not Cursor's stdout-JSON
// `{ block: true, reason }` form — so the two hook scripts installed here
// (hooks/gsd-windsurf-pre-write.js, hooks/gsd-windsurf-pre-command.js) speak a
// different protocol than the Cursor scripts, even though the surrounding
// install/reconcile infra mirrors writeCursorHooksJson/removeCursorHooksJson.
//
// Only 2 of GSD's 6 Cursor-parity hook events have a Cascade counterpart with
// BLOCKING semantics: pre_write_code and pre_run_command. Cascade has no
// context-injection channel (no `additional_context`-style advisory
// response), so the 4 advisory events GSD registers on Cursor (sessionStart,
// postToolUse, stop, subagentStart/subagentStop) are deliberately NOT ported.
// ---------------------------------------------------------------------------
const GSD_WINDSURF_PRE_WRITE_HOOK_SCRIPT = 'gsd-windsurf-pre-write.js';
const GSD_WINDSURF_PRE_COMMAND_HOOK_SCRIPT = 'gsd-windsurf-pre-command.js';
const GSD_WINDSURF_HOOK_MARKER = 'gsd-managed';
/** The 2 Cascade hook events GSD wires with blocking (exit-code-2) guards. */
const WINDSURF_HOOK_EVENTS = Object.freeze(['pre_write_code', 'pre_run_command']);
/** Event → hook-script mapping (mirrors CURSOR_EVENT_SCRIPT_MAP's convention). */
const WINDSURF_EVENT_SCRIPT_MAP = Object.freeze({
    pre_write_code: GSD_WINDSURF_PRE_WRITE_HOOK_SCRIPT,
    pre_run_command: GSD_WINDSURF_PRE_COMMAND_HOOK_SCRIPT,
});
/** All GSD-managed Windsurf hook scripts (used by uninstall cleanup). */
const GSD_WINDSURF_HOOK_SCRIPTS = [
    GSD_WINDSURF_PRE_WRITE_HOOK_SCRIPT,
    GSD_WINDSURF_PRE_COMMAND_HOOK_SCRIPT,
];
/**
 * Build a single Cascade hooks.json managed entry. Cascade's entry shape has
 * no `type` field (unlike Cursor's `{ type: 'command', command }`) — just a
 * bare `command` shell string plus the GSD marker.
 */
function buildWindsurfHookEntry(command) {
    return {
        command,
        [GSD_WINDSURF_HOOK_MARKER]: true,
    };
}
function isManagedWindsurfHookEntry(entry) {
    return Boolean(entry && typeof entry === 'object' && entry[GSD_WINDSURF_HOOK_MARKER]);
}
/**
 * Reconcile GSD's managed Cascade hook entries into `<targetDir>/hooks.json`,
 * preserving any user-owned entries. Mirrors reconcileCursorHooksJson's
 * merge/no-write-when-unchanged semantics, adapted to Cascade's flatter
 * `{ hooks: { <event>: [...] } }` shape (no `version` field, no legacy
 * top-level-array lift — Cascade's hooks.json is a brand-new surface with no
 * prior shape to migrate from).
 */
function reconcileWindsurfHooksJson(hooksJsonPath, managedEntries) {
    let parsed = {};
    let currentContent = null;
    if (node_fs_1.default.existsSync(hooksJsonPath)) {
        const raw = node_fs_1.default.readFileSync(hooksJsonPath, 'utf8');
        currentContent = raw;
        if (raw.trim()) {
            try {
                parsed = JSON.parse(raw);
            }
            catch (err) {
                throw new Error(`Windsurf hooks.json parse failed: ${err && err.message ? err.message : String(err)}`);
            }
        }
    }
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed))
        parsed = {};
    const hasNestedHooksObject = parsed['hooks'] && typeof parsed['hooks'] === 'object' && !Array.isArray(parsed['hooks']);
    if (!hasNestedHooksObject)
        parsed['hooks'] = {};
    const hookTable = parsed['hooks'];
    const entries = managedEntries || {};
    for (const event of WINDSURF_HOOK_EVENTS) {
        const existing = Array.isArray(hookTable[event]) ? hookTable[event] : [];
        const userOwned = existing.filter((e) => !isManagedWindsurfHookEntry(e));
        const newEntry = entries[event] || null;
        if (newEntry) {
            hookTable[event] = [...userOwned, newEntry];
        }
        else if (userOwned.length > 0) {
            hookTable[event] = userOwned;
        }
        else {
            delete hookTable[event];
        }
    }
    // Avoid writing an empty `{ "hooks": {} }` artifact.
    if (Object.keys(hookTable).length === 0)
        delete parsed['hooks'];
    const nextContent = `${JSON.stringify(parsed, null, 2)}\n`;
    const changed = currentContent !== nextContent;
    const shouldWrite = changed && (currentContent !== null || Object.keys(parsed).length > 0);
    if (shouldWrite) {
        atomicWriteFileSync(hooksJsonPath, nextContent, 'utf8');
    }
    return { changed: changed, wrote: shouldWrite, path: hooksJsonPath };
}
/**
 * Write GSD-managed Cascade lifecycle hooks into `<targetDir>/hooks.json`.
 * Both managed hook scripts (gsd-windsurf-pre-write.js,
 * gsd-windsurf-pre-command.js) are copied from the GSD hooks/ source to
 * `<targetDir>/hooks/` first, so the hooks.json entries never reference a
 * script that wasn't installed. Mirrors writeCursorHooksJson's structure;
 * `buildHookCommand` is runtime-agnostic (it already returns a plain shell
 * command string), so it is reused as-is with `runtime: 'windsurf'` — only
 * the hooks.json ENTRY shape (buildWindsurfHookEntry) and the reconcile
 * function differ from Cursor's.
 *
 * @param targetDir - The Windsurf config dir (global: ~/.codeium/windsurf; local: .windsurf)
 * @param src       - The GSD install source root (for copying hook scripts)
 * @param opts      - `{ platform? }`
 * @returns `{ hooksJsonPath, changed }`
 */
function writeWindsurfHooksJson(targetDir, src, opts) {
    opts = opts || {};
    const hooksDir = node_path_1.default.join(targetDir, 'hooks');
    node_fs_1.default.mkdirSync(hooksDir, { recursive: true });
    const srcHooksDir = node_path_1.default.join(src, 'hooks');
    const installedScripts = new Set();
    for (const script of GSD_WINDSURF_HOOK_SCRIPTS) {
        const srcPath = node_path_1.default.join(srcHooksDir, script);
        const destPath = node_path_1.default.join(hooksDir, script);
        if (node_fs_1.default.existsSync(srcPath)) {
            let content = node_fs_1.default.readFileSync(srcPath, 'utf8');
            content = content.replace(/gsd:/gi, 'gsd-');
            node_fs_1.default.writeFileSync(destPath, content);
            try {
                node_fs_1.default.chmodSync(destPath, 0o755);
            }
            catch { /* Windows: ignore chmod */ }
            installedScripts.add(script);
        }
    }
    const hookOpts = { runtime: 'windsurf', platform: opts.platform || process.platform };
    const commands = {};
    for (const ev of WINDSURF_HOOK_EVENTS) {
        const script = WINDSURF_EVENT_SCRIPT_MAP[ev];
        commands[ev] = (script && installedScripts.has(script)) ? buildHookCommand(targetDir, script, hookOpts) : null;
    }
    const managedEntries = {};
    for (const ev of WINDSURF_HOOK_EVENTS) {
        const cmd = commands[ev];
        if (cmd)
            managedEntries[ev] = buildWindsurfHookEntry(cmd);
    }
    const hooksJsonPath = node_path_1.default.join(targetDir, 'hooks.json');
    const result = reconcileWindsurfHooksJson(hooksJsonPath, managedEntries);
    return { hooksJsonPath, changed: result.changed };
}
/**
 * Remove all GSD-managed Cascade hook entries from hooks.json. User-owned
 * entries are preserved. If the file becomes empty, it is removed.
 *
 * @param targetDir - The Windsurf config dir
 * @returns `{ changed }`
 */
function removeWindsurfHooksJson(targetDir) {
    const hooksJsonPath = node_path_1.default.join(targetDir, 'hooks.json');
    if (!node_fs_1.default.existsSync(hooksJsonPath))
        return { changed: false };
    const result = reconcileWindsurfHooksJson(hooksJsonPath, null);
    if (result.changed) {
        try {
            const contentRaw = node_fs_1.default.readFileSync(hooksJsonPath, 'utf8');
            const parsed = JSON.parse(contentRaw);
            const hookTable = (parsed['hooks'] && typeof parsed['hooks'] === 'object' && !Array.isArray(parsed['hooks']))
                ? parsed['hooks']
                : {};
            const hasAnyEvents = Object.keys(hookTable).some((k) => Array.isArray(hookTable[k]) && hookTable[k].length > 0);
            if (!hasAnyEvents) {
                node_fs_1.default.unlinkSync(hooksJsonPath);
                return { changed: true };
            }
        }
        catch { /* best-effort: leave the file */ }
    }
    return { changed: result.changed };
}
// ---------------------------------------------------------------------------
// Copilot hook functions
// ---------------------------------------------------------------------------
function buildCopilotHookConfig() {
    return {
        version: 1,
        hooks: {
            sessionStart: [
                {
                    type: 'command',
                    bash: GSD_COPILOT_SESSION_HOOK_BASH,
                    powershell: GSD_COPILOT_SESSION_HOOK_PWSH,
                    timeoutSec: 10,
                },
            ],
            // #2099 UPGRADE 1: multi-event hook bus — preToolUse (worktree/read-safety
            // advisory), postToolUse (context-monitor advisory), userPromptSubmitted
            // (prompt-guard advisory), sessionEnd (session-finalize advisory).
            preToolUse: [
                {
                    type: 'command',
                    bash: GSD_COPILOT_PRE_TOOL_HOOK_BASH,
                    powershell: GSD_COPILOT_PRE_TOOL_HOOK_PWSH,
                    timeoutSec: 10,
                },
            ],
            postToolUse: [
                {
                    type: 'command',
                    bash: GSD_COPILOT_POST_TOOL_HOOK_BASH,
                    powershell: GSD_COPILOT_POST_TOOL_HOOK_PWSH,
                    timeoutSec: 10,
                },
            ],
            userPromptSubmitted: [
                {
                    type: 'command',
                    bash: GSD_COPILOT_PROMPT_SUBMIT_HOOK_BASH,
                    powershell: GSD_COPILOT_PROMPT_SUBMIT_HOOK_PWSH,
                    timeoutSec: 10,
                },
            ],
            sessionEnd: [
                {
                    type: 'command',
                    bash: GSD_COPILOT_SESSION_END_HOOK_BASH,
                    powershell: GSD_COPILOT_SESSION_END_HOOK_PWSH,
                    timeoutSec: 10,
                },
            ],
        },
    };
}
function writeCopilotHookConfig(targetDir) {
    const hooksDir = node_path_1.default.join(targetDir, 'hooks');
    node_fs_1.default.mkdirSync(hooksDir, { recursive: true });
    const hookPath = node_path_1.default.join(hooksDir, GSD_COPILOT_HOOK_FILE);
    node_fs_1.default.writeFileSync(hookPath, JSON.stringify(buildCopilotHookConfig(), null, 2) + '\n');
    return hookPath;
}
// eslint-disable-next-line @typescript-eslint/no-explicit-any
function applySettingsJsonHooks(settings, opts) {
    /* eslint-disable @typescript-eslint/no-unsafe-member-access,
                      @typescript-eslint/no-unsafe-call,
                      @typescript-eslint/no-unsafe-assignment */
    const { runtime, isGlobal, targetDir, postToolEvent, hookEvents, extendedHookEvents, hooksSurface, updateCheckCommand, contextMonitorCommand, promptGuardCommand, readGuardCommand, readInjectionScannerCommand, configReloadCommand, hookOpts, localCmd, localShellCmd, } = opts;
    // ADR-857 phase 5f-3: extended hook events are now driven by the registry
    // descriptor field rather than hardcoded runtime-name checks.
    const extendedEvents = Array.isArray(extendedHookEvents) ? extendedHookEvents : [];
    // ADR-857 phase 5g drive 3: hook-skip guard is driven by the hooksSurface
    // descriptor field. Only runtimes with hooksSurface === 'settings-json'
    // register settings.json hooks; runtimes with hooksSurface === 'none'
    // (opencode, kilo) are skipped. Equivalence: hooksSurface !== 'none' iff
    // the old !isOpencode && !isKilo check.
    // #2095: kimi's hooksSurface is 'kimi-hooks-toml' — it registers hooks into
    // its own native config.toml via writeKimiHooksToml, not settings.json (kimi
    // never writes settings.json at all: writesSharedSettings stays false). This
    // guard must also skip kimi's surface so applySettingsJsonHooks doesn't log
    // misleading "Configured ..." console messages for a settings object that
    // finishInstall() will never persist for kimi.
    if (hooksSurface !== 'none' && hooksSurface !== 'kimi-hooks-toml') {
        if (!settings.hooks) {
            settings.hooks = {};
        }
        if (!settings.hooks.SessionStart) {
            settings.hooks.SessionStart = [];
        }
        const hasGsdUpdateHook = settings.hooks.SessionStart.some((entry) => entry.hooks && entry.hooks.some((h) => referencesHook(h, 'gsd-check-update')));
        // Guard: only register if the hook file was actually installed (#1754).
        // When hooks/dist/ is missing from the npm package (as in v1.32.0), the
        // copy step produces no files but the registration step ran unconditionally,
        // causing "hook error" on every tool invocation.
        const checkUpdateFile = node_path_1.default.join(targetDir, 'hooks', 'gsd-check-update.js');
        if (!hasGsdUpdateHook && node_fs_1.default.existsSync(checkUpdateFile) && updateCheckCommand) {
            settings.hooks.SessionStart.push({
                hooks: [
                    {
                        type: 'command',
                        command: updateCheckCommand
                    }
                ]
            });
            console.log(`  ${green}✓${reset} Configured update check hook`);
        }
        else if (!hasGsdUpdateHook && !node_fs_1.default.existsSync(checkUpdateFile)) {
            console.warn(`  ${yellow}⚠${reset}  Skipped update check hook — gsd-check-update.js not found at target`);
        }
        // Configure post-tool hook for context window monitoring
        if (!settings.hooks[postToolEvent]) {
            settings.hooks[postToolEvent] = [];
        }
        const hasContextMonitorHook = settings.hooks[postToolEvent].some((entry) => entry.hooks && entry.hooks.some((h) => referencesHook(h, 'gsd-context-monitor')));
        const contextMonitorFile = node_path_1.default.join(targetDir, 'hooks', 'gsd-context-monitor.js');
        if (!hasContextMonitorHook && node_fs_1.default.existsSync(contextMonitorFile) && contextMonitorCommand) {
            settings.hooks[postToolEvent].push({
                matcher: 'Bash|Edit|Write|MultiEdit|Agent|Task',
                hooks: [
                    {
                        type: 'command',
                        command: contextMonitorCommand,
                        timeout: 10
                    }
                ]
            });
            console.log(`  ${green}✓${reset} Configured context window monitor hook`);
        }
        else if (!hasContextMonitorHook && !node_fs_1.default.existsSync(contextMonitorFile)) {
            console.warn(`  ${yellow}⚠${reset}  Skipped context monitor hook — gsd-context-monitor.js not found at target`);
        }
        else {
            // Migrate existing context monitor hooks: add matcher and timeout if missing
            for (const entry of settings.hooks[postToolEvent]) {
                if (entry.hooks && entry.hooks.some((h) => referencesHook(h, 'gsd-context-monitor'))) {
                    let migrated = false;
                    if (!entry.matcher) {
                        entry.matcher = 'Bash|Edit|Write|MultiEdit|Agent|Task';
                        migrated = true;
                    }
                    for (const h of entry.hooks) {
                        if (referencesHook(h, 'gsd-context-monitor') && !h.timeout) {
                            h.timeout = 10;
                            migrated = true;
                        }
                    }
                    if (migrated) {
                        console.log(`  ${green}✓${reset} Updated context monitor hook (added matcher + timeout)`);
                    }
                }
            }
        }
        // Configure PreToolUse hook for prompt injection detection
        // ADR-857 phase 5f-2: drive dialect from opts.hookEvents (registry descriptor).
        // hookEvents='gemini' → BeforeTool; all others → PreToolUse.
        // Equivalence: hookEvents='gemini' iff runtime===antigravity (same as old check).
        const preToolEvent = hookEvents === 'gemini' ? 'BeforeTool' : 'PreToolUse';
        if (!settings.hooks[preToolEvent]) {
            settings.hooks[preToolEvent] = [];
        }
        const hasPromptGuardHook = settings.hooks[preToolEvent].some((entry) => entry.hooks && entry.hooks.some((h) => referencesHook(h, 'gsd-prompt-guard')));
        const promptGuardFile = node_path_1.default.join(targetDir, 'hooks', 'gsd-prompt-guard.js');
        if (!hasPromptGuardHook && node_fs_1.default.existsSync(promptGuardFile) && promptGuardCommand) {
            settings.hooks[preToolEvent].push({
                matcher: 'Write|Edit',
                hooks: [
                    {
                        type: 'command',
                        command: promptGuardCommand,
                        timeout: 5
                    }
                ]
            });
            console.log(`  ${green}✓${reset} Configured prompt injection guard hook`);
        }
        else if (!hasPromptGuardHook && !node_fs_1.default.existsSync(promptGuardFile)) {
            console.warn(`  ${yellow}⚠${reset}  Skipped prompt guard hook — gsd-prompt-guard.js not found at target`);
        }
        // Configure PreToolUse hook for read-before-edit guidance (#1628)
        // Prevents infinite retry loops when non-Claude models attempt to edit
        // files without reading them first. Advisory-only — does not block.
        const hasReadGuardHook = settings.hooks[preToolEvent].some((entry) => entry.hooks && entry.hooks.some((h) => referencesHook(h, 'gsd-read-guard')));
        const readGuardFile = node_path_1.default.join(targetDir, 'hooks', 'gsd-read-guard.js');
        if (!hasReadGuardHook && node_fs_1.default.existsSync(readGuardFile) && readGuardCommand) {
            settings.hooks[preToolEvent].push({
                matcher: 'Write|Edit',
                hooks: [
                    {
                        type: 'command',
                        command: readGuardCommand,
                        timeout: 5
                    }
                ]
            });
            console.log(`  ${green}✓${reset} Configured read-before-edit guard hook`);
        }
        else if (!hasReadGuardHook && !node_fs_1.default.existsSync(readGuardFile)) {
            console.warn(`  ${yellow}⚠${reset}  Skipped read guard hook — gsd-read-guard.js not found at target`);
        }
        // Configure PostToolUse hook for read-time prompt injection scanning (#2201)
        // Scans content returned by the Read tool for injection patterns, including
        // summarisation-specific patterns that survive context compression.
        const hasReadInjectionScannerHook = settings.hooks[postToolEvent].some((entry) => entry.hooks && entry.hooks.some((h) => referencesHook(h, 'gsd-read-injection-scanner')));
        const readInjectionScannerFile = node_path_1.default.join(targetDir, 'hooks', 'gsd-read-injection-scanner.js');
        if (!hasReadInjectionScannerHook && node_fs_1.default.existsSync(readInjectionScannerFile) && readInjectionScannerCommand) {
            settings.hooks[postToolEvent].push({
                matcher: 'Read',
                hooks: [
                    {
                        type: 'command',
                        command: readInjectionScannerCommand,
                        timeout: 5
                    }
                ]
            });
            console.log(`  ${green}✓${reset} Configured read injection scanner hook`);
        }
        else if (!hasReadInjectionScannerHook && !node_fs_1.default.existsSync(readInjectionScannerFile)) {
            console.warn(`  ${yellow}⚠${reset}  Skipped read injection scanner hook — gsd-read-injection-scanner.js not found at target`);
        }
        // Community hooks — registered on install but opt-in at runtime.
        // Each hook checks .planning/config.json for hooks.community: true
        // and exits silently (no-op) if not enabled. This lets users enable
        // them per-project by adding: "hooks": { "community": true }
        // Configure workflow guard hook (opt-in via hooks.workflow_guard: true)
        // Detects file edits outside GSD workflow context and advises using
        // /gsd-quick or /gsd-fast for state-tracked changes. Also hard-blocks
        // unsafe Bash commands that violate worktree-agent isolation.
        const workflowGuardCommand = isGlobal
            ? buildHookCommand(targetDir, 'gsd-workflow-guard.js', hookOpts)
            : localCmd('gsd-workflow-guard.js');
        const workflowGuardMatcher = 'Bash|Edit|Write|MultiEdit';
        const workflowGuardHookEntry = settings.hooks[preToolEvent].find((entry) => entry.hooks && entry.hooks.some((h) => referencesHook(h, 'gsd-workflow-guard')));
        const hasWorkflowGuardHook = Boolean(workflowGuardHookEntry);
        const workflowGuardFile = node_path_1.default.join(targetDir, 'hooks', 'gsd-workflow-guard.js');
        if (hasWorkflowGuardHook && workflowGuardHookEntry.matcher !== workflowGuardMatcher) {
            workflowGuardHookEntry.matcher = workflowGuardMatcher;
            console.log(`  ${green}✓${reset} Updated workflow guard hook matcher`);
        }
        else if (!hasWorkflowGuardHook && node_fs_1.default.existsSync(workflowGuardFile) && workflowGuardCommand) {
            settings.hooks[preToolEvent].push({
                matcher: workflowGuardMatcher,
                hooks: [
                    {
                        type: 'command',
                        command: workflowGuardCommand,
                        timeout: 5
                    }
                ]
            });
            console.log(`  ${green}✓${reset} Configured workflow guard hook (opt-in via hooks.workflow_guard)`);
        }
        else if (!hasWorkflowGuardHook && !node_fs_1.default.existsSync(workflowGuardFile)) {
            console.warn(`  ${yellow}⚠${reset}  Skipped workflow guard hook — gsd-workflow-guard.js not found at target`);
        }
        // Configure PreToolUse hook for worktree absolute-path safety (#260)
        // Hard-blocks Edit/Write/MultiEdit tool calls with absolute paths that resolve
        // outside the current worktree root. Prevents executor agents from
        // accidentally writing to the main checkout when running in isolation="worktree".
        const worktreePathGuardCommand = isGlobal
            ? buildHookCommand(targetDir, 'gsd-worktree-path-guard.js', hookOpts)
            : localCmd('gsd-worktree-path-guard.js');
        const hasWorktreePathGuardHook = settings.hooks[preToolEvent].some((entry) => entry.hooks && entry.hooks.some((h) => referencesHook(h, 'gsd-worktree-path-guard')));
        const worktreePathGuardFile = node_path_1.default.join(targetDir, 'hooks', 'gsd-worktree-path-guard.js');
        if (!hasWorktreePathGuardHook && node_fs_1.default.existsSync(worktreePathGuardFile) && worktreePathGuardCommand) {
            settings.hooks[preToolEvent].push({
                matcher: 'Write|Edit|MultiEdit',
                hooks: [
                    {
                        type: 'command',
                        command: worktreePathGuardCommand,
                        timeout: 5
                    }
                ]
            });
            console.log(`  ${green}✓${reset} Configured worktree path guard hook`);
        }
        else if (!hasWorktreePathGuardHook && !node_fs_1.default.existsSync(worktreePathGuardFile)) {
            console.warn(`  ${yellow}⚠${reset}  Skipped worktree path guard hook — gsd-worktree-path-guard.js not found at target`);
        }
        // Configure commit validation hook (Conventional Commits enforcement, opt-in)
        const validateCommitCommand = isGlobal
            ? buildHookCommand(targetDir, 'gsd-validate-commit.sh', hookOpts)
            : localShellCmd('gsd-validate-commit.sh');
        const hasValidateCommitHook = settings.hooks[preToolEvent].some((entry) => entry.hooks && entry.hooks.some((h) => referencesHook(h, 'gsd-validate-commit')));
        // Guard: only register if the .sh file was actually installed. If the npm package
        // omitted the file (as happened in v1.32.0, bug #1817), registering a missing hook
        // causes a hook error on every Bash tool invocation.
        const validateCommitFile = node_path_1.default.join(targetDir, 'hooks', 'gsd-validate-commit.sh');
        if (!hasValidateCommitHook && node_fs_1.default.existsSync(validateCommitFile) && validateCommitCommand) {
            settings.hooks[preToolEvent].push({
                matcher: 'Bash',
                hooks: [
                    {
                        type: 'command',
                        command: validateCommitCommand,
                        timeout: 5
                    }
                ]
            });
            console.log(`  ${green}✓${reset} Configured commit validation hook (opt-in via config)`);
        }
        else if (!hasValidateCommitHook && !node_fs_1.default.existsSync(validateCommitFile)) {
            console.warn(`  ${yellow}⚠${reset}  Skipped commit validation hook — gsd-validate-commit.sh not found at target`);
        }
        else if (!hasValidateCommitHook && !validateCommitCommand) {
            console.warn(`  ${yellow}⚠${reset}  Skipped commit validation hook — Bash executable path unavailable (#3393)`);
        }
        // Configure graphify auto-update hook (opt-in via graphify.auto_update; default false, #3347).
        // PostToolUse Bash matcher — fires after git commit/merge/pull/rebase --continue/cherry-pick
        // on the default branch, dispatches `graphify update .` in a detached subprocess. No-op unless
        // .planning/config.json has BOTH graphify.enabled=true AND graphify.auto_update=true.
        const graphifyUpdateCommand = isGlobal
            ? buildHookCommand(targetDir, 'gsd-graphify-update.sh', hookOpts)
            : localShellCmd('gsd-graphify-update.sh');
        const hasGraphifyUpdateHook = settings.hooks[postToolEvent].some((entry) => entry.hooks && entry.hooks.some((h) => referencesHook(h, 'gsd-graphify-update')));
        const graphifyUpdateFile = node_path_1.default.join(targetDir, 'hooks', 'gsd-graphify-update.sh');
        if (!hasGraphifyUpdateHook && node_fs_1.default.existsSync(graphifyUpdateFile) && graphifyUpdateCommand) {
            settings.hooks[postToolEvent].push({
                matcher: 'Bash',
                hooks: [
                    {
                        type: 'command',
                        command: graphifyUpdateCommand,
                        timeout: 5
                    }
                ]
            });
            console.log(`  ${green}✓${reset} Configured graphify auto-update hook (opt-in via graphify.auto_update)`);
        }
        else if (!hasGraphifyUpdateHook && !node_fs_1.default.existsSync(graphifyUpdateFile)) {
            console.warn(`  ${yellow}⚠${reset}  Skipped graphify auto-update hook — gsd-graphify-update.sh not found at target`);
        }
        else if (!hasGraphifyUpdateHook && !graphifyUpdateCommand) {
            console.warn(`  ${yellow}⚠${reset}  Skipped graphify auto-update hook — Bash executable path unavailable (#3393)`);
        }
        // Configure session state orientation hook (opt-in)
        const sessionStateCommand = isGlobal
            ? buildHookCommand(targetDir, 'gsd-session-state.sh', hookOpts)
            : localShellCmd('gsd-session-state.sh');
        const hasSessionStateHook = settings.hooks.SessionStart.some((entry) => entry.hooks && entry.hooks.some((h) => referencesHook(h, 'gsd-session-state')));
        const sessionStateFile = node_path_1.default.join(targetDir, 'hooks', 'gsd-session-state.sh');
        if (!hasSessionStateHook && node_fs_1.default.existsSync(sessionStateFile) && sessionStateCommand) {
            settings.hooks.SessionStart.push({
                hooks: [
                    {
                        type: 'command',
                        command: sessionStateCommand
                    }
                ]
            });
            console.log(`  ${green}✓${reset} Configured session state orientation hook (opt-in via config)`);
        }
        else if (!hasSessionStateHook && !node_fs_1.default.existsSync(sessionStateFile)) {
            console.warn(`  ${yellow}⚠${reset}  Skipped session state hook — gsd-session-state.sh not found at target`);
        }
        else if (!hasSessionStateHook && !sessionStateCommand) {
            console.warn(`  ${yellow}⚠${reset}  Skipped session state hook — Bash executable path unavailable (#3393)`);
        }
        // Configure phase boundary detection hook (opt-in)
        const phaseBoundaryCommand = isGlobal
            ? buildHookCommand(targetDir, 'gsd-phase-boundary.sh', hookOpts)
            : localShellCmd('gsd-phase-boundary.sh');
        const hasPhaseBoundaryHook = settings.hooks[postToolEvent].some((entry) => entry.hooks && entry.hooks.some((h) => referencesHook(h, 'gsd-phase-boundary')));
        const phaseBoundaryFile = node_path_1.default.join(targetDir, 'hooks', 'gsd-phase-boundary.sh');
        if (!hasPhaseBoundaryHook && node_fs_1.default.existsSync(phaseBoundaryFile) && phaseBoundaryCommand) {
            settings.hooks[postToolEvent].push({
                matcher: 'Write|Edit',
                hooks: [
                    {
                        type: 'command',
                        command: phaseBoundaryCommand,
                        timeout: 5
                    }
                ]
            });
            console.log(`  ${green}✓${reset} Configured phase boundary detection hook (opt-in via config)`);
        }
        else if (!hasPhaseBoundaryHook && !node_fs_1.default.existsSync(phaseBoundaryFile)) {
            console.warn(`  ${yellow}⚠${reset}  Skipped phase boundary hook — gsd-phase-boundary.sh not found at target`);
        }
        else if (!hasPhaseBoundaryHook && !phaseBoundaryCommand) {
            console.warn(`  ${yellow}⚠${reset}  Skipped phase boundary hook — Bash executable path unavailable (#3393)`);
        }
        // ── Extended hook events: SubagentStop / Stop / PreCompact / SubagentStart
        //    (#788 + #770 + #2092) ────────────────────────────────────────────────
        // Claude Code (since #770) and Qwen Code (since #788) both support the
        // SubagentStop / Stop / PreCompact lifecycle events. Qwen Code additionally
        // supports SubagentStart (#2092 Phase B, Upgrade 2). Wire gsd-context-
        // monitor so agents get context-headroom warnings at subagent start,
        // subagent completion, model stop, and pre-compaction (the most critical
        // moment to surface headroom info).
        //
        //   SubagentStart — subagent lifecycle start (context headroom tracking;
        //                   qwen-only today — no other runtime declares it in
        //                   extendedHookEvents)
        //   SubagentStop  — subagent lifecycle completion (context headroom tracking)
        //   Stop          — model stop / final-response moment (context headroom)
        //   PreCompact    — fires before conversation compaction (most critical
        //                   moment to surface context headroom warnings)
        //
        // Note: UserPromptSubmit is NOT wired here.  That event carries the raw
        // user prompt text, not a tool invocation, so gsd-prompt-guard (which
        // exits unless tool_name is Write/Edit) would be a silent no-op.  A
        // dedicated handler for UserPromptSubmit is deferred to a follow-on issue.
        // SubagentStart, SubagentStop, Stop, PreCompact — route through the context monitor.
        // Guard is descriptor-driven: only events present in extendedEvents are wired,
        // so this loop is a no-op for every runtime that doesn't list SubagentStart.
        {
            // Descriptor-driven (ADR-1239 / #2092): folded from a hardcoded
            // `runtime === 'qwen' ? ... : ...` ternary into a capability-title
            // lookup (see _capabilityTitle above).
            const runtimeLabel = _capabilityTitle(runtime);
            for (const event of ['SubagentStop', 'Stop', 'PreCompact', 'SubagentStart']) {
                if (!extendedEvents.includes(event))
                    continue;
                if (!settings.hooks[event]) {
                    settings.hooks[event] = [];
                }
                const alreadyHasContextMonitor = settings.hooks[event].some((entry) => entry.hooks && entry.hooks.some((h) => referencesHook(h, 'gsd-context-monitor')));
                if (!alreadyHasContextMonitor && node_fs_1.default.existsSync(contextMonitorFile) && contextMonitorCommand) {
                    settings.hooks[event].push({
                        hooks: [
                            {
                                type: 'command',
                                command: contextMonitorCommand,
                                timeout: 10
                            }
                        ]
                    });
                    console.log(`  ${green}✓${reset} Configured ${event} context monitor hook (${runtimeLabel})`);
                }
                else if (!alreadyHasContextMonitor && !node_fs_1.default.existsSync(contextMonitorFile)) {
                    console.warn(`  ${yellow}⚠${reset}  Skipped ${event} hook — gsd-context-monitor.js not found at target`);
                }
            }
        }
        // ── end SubagentStop / Stop / PreCompact / SubagentStart events ────────────
        // ── Extended hook events (#776; Gemini runtime removed #1928) ──────────────
        // The Gemini-3-backend dialect exposes several hook events beyond
        // BeforeTool/AfterTool. These were added for the now-removed Gemini CLI
        // runtime (#776). No currently supported runtime declares them —
        // Antigravity's descriptor carries `extendedHookEvents: []` — so this loop
        // is an inert, descriptor-driven seam: it no-ops for every present runtime
        // and re-activates automatically if a future runtime declares any of them.
        // Three high-value events would be wired here:
        //
        //   BeforeAgent  — fires after user submits a prompt, before the agent
        //                  plans.  Wire gsd-context-monitor for context headroom
        //                  awareness at prompt time.
        //   AfterAgent   — fires once per turn after the model generates its final
        //                  response.  Wire gsd-context-monitor to track headroom
        //                  after each agent turn completes.
        //   BeforeModel  — fires before each LLM call (per-turn, not per-session).
        //                  Wire gsd-context-monitor for per-turn context injection
        //                  — more precise than session-start-only injection.
        //
        // All three reuse gsd-context-monitor.js — no new hook files needed.
        // The `decision:"deny"` retry capability of AfterAgent is intentionally
        // left to the hook script to implement when triggered (gsd-context-monitor
        // exits 0 / advisory-only today; an active quality gate is a follow-on).
        //
        // Note: BeforeToolSelection is NOT wired.  That event does not map to a
        // gsd hook use case at this time; deferred to a follow-on issue.
        //
        // Guard is now descriptor-driven: only events present in extendedEvents are wired.
        for (const extendedEvent of ['BeforeAgent', 'AfterAgent', 'BeforeModel']) {
            if (!extendedEvents.includes(extendedEvent))
                continue;
            if (!Array.isArray(settings.hooks[extendedEvent])) {
                settings.hooks[extendedEvent] = [];
            }
            const alreadyHasContextMonitor = settings.hooks[extendedEvent].some((entry) => entry.hooks && entry.hooks.some((h) => referencesHook(h, 'gsd-context-monitor')));
            if (!alreadyHasContextMonitor && node_fs_1.default.existsSync(contextMonitorFile) && contextMonitorCommand) {
                settings.hooks[extendedEvent].push({
                    hooks: [
                        {
                            type: 'command',
                            command: contextMonitorCommand,
                            timeout: 10
                        }
                    ]
                });
                console.log(`  ${green}✓${reset} Configured ${extendedEvent} context monitor hook`);
            }
            else if (!alreadyHasContextMonitor && !node_fs_1.default.existsSync(contextMonitorFile)) {
                console.warn(`  ${yellow}⚠${reset}  Skipped ${extendedEvent} hook — gsd-context-monitor.js not found at target`);
            }
        }
        // ── end Antigravity-only extended hook events ──────────────────────────────
        // ── FileChanged hook: hot-reload gsd config on .planning/config.json edits ─
        // Claude Code fires FileChanged when a watched file changes on disk.  Wire
        // gsd-config-reload.js to reload the gsd config context whenever the user
        // edits .planning/config.json mid-session, eliminating the need to restart.
        //
        // The matcher "config.json" watches for changes to any file named config.json
        // (Claude Code matches by filename, not full path).  The hook exits silently
        // when the changed file is not the gsd config.
        //
        // Scoped to Claude Code only: Qwen Code's FileChanged support is not yet
        // verified; extend in a follow-on if empirically confirmed.
        if (extendedEvents.includes('FileChanged')) {
            if (!settings.hooks.FileChanged) {
                settings.hooks.FileChanged = [];
            }
            const configReloadFile = node_path_1.default.join(targetDir, 'hooks', 'gsd-config-reload.js');
            const alreadyHasConfigReload = settings.hooks.FileChanged.some((entry) => entry.hooks && entry.hooks.some((h) => referencesHook(h, 'gsd-config-reload')));
            if (!alreadyHasConfigReload && node_fs_1.default.existsSync(configReloadFile) && configReloadCommand) {
                settings.hooks.FileChanged.push({
                    matcher: 'config.json',
                    hooks: [
                        {
                            type: 'command',
                            command: configReloadCommand,
                            timeout: 8
                        }
                    ]
                });
                console.log(`  ${green}✓${reset} Configured FileChanged config-reload hook (Claude Code)`);
            }
            else if (!alreadyHasConfigReload && !node_fs_1.default.existsSync(configReloadFile)) {
                console.warn(`  ${yellow}⚠${reset}  Skipped FileChanged hook — gsd-config-reload.js not found at target`);
            }
            else if (!alreadyHasConfigReload && !configReloadCommand) {
                console.warn(`  ${yellow}⚠${reset}  Skipped FileChanged hook — Node executable path unavailable`);
            }
        }
        // ── end FileChanged hook ────────────────────────────────────────────────────
    }
    /* eslint-enable @typescript-eslint/no-unsafe-member-access,
                     @typescript-eslint/no-unsafe-call,
                     @typescript-eslint/no-unsafe-assignment */
}
// ---------------------------------------------------------------------------
// Kimi hooks.toml (#2095 EoS/kimi Upgrade 1 — native hook bus)
//
// Kimi CLI reads lifecycle hooks from a flat `[[hooks]]` array in its own
// config.toml (moonshotai.github.io/kimi-cli/en/customization/hooks.html),
// not from settings.json. Unlike every other hooksSurface writer above, this
// file lives OUTSIDE the runtime's GSD configDir: kimi's configDir is the
// generic Agent-Skills root (~/.config/agents by default), while config.toml
// is a sibling at ~/.kimi (KIMI_SHARE_DIR override), resolved by
// resolveKimiHooksTomlDir in runtime-homes.cts. Callers resolve that path and
// pass it in explicitly — this module never reaches into runtime-homes.cjs
// itself, keeping the same configDir/targetDir-passed-in shape every other
// writer in this file uses.
//
// GSD-owned [[hooks]] entries are wrapped in marker comments so a reinstall
// can find-and-replace only GSD's own block, leaving any user-authored
// [[hooks]] entries elsewhere in the file untouched — mirrors the marker
// approach stripStaleGsdHookBlocks uses for Codex's config.toml, simplified
// to plain string slicing since this block is a flat, self-contained span
// (no nested per-key structural TOML parsing is needed).
// ---------------------------------------------------------------------------
const KIMI_HOOKS_TOML_MARKER_BEGIN = '# GSD Hooks BEGIN — managed by GSD, do not edit between these markers';
const KIMI_HOOKS_TOML_MARKER_END = '# GSD Hooks END';
function buildKimiHookEntryToml(spec) {
    if (!spec.command)
        return null;
    const lines = ['[[hooks]]', `event = "${spec.event}"`];
    if (spec.matcher) {
        lines.push(`matcher = "${escapeTomlDoubleQuotedString(spec.matcher)}"`);
    }
    lines.push(`command = "${escapeTomlDoubleQuotedString(spec.command)}"`);
    if (typeof spec.timeout === 'number') {
        lines.push(`timeout = ${spec.timeout}`);
    }
    return lines.join('\n');
}
/**
 * Build the full marker-delimited GSD [[hooks]] block for kimi's config.toml,
 * or null when no GSD hook resolved to a usable command (hooks/ missing, or
 * the node/bash runner could not be resolved — mirrors the #1754/#3002
 * defensive guards applySettingsJsonHooks applies per-hook above).
 *
 * Event -> hook mapping mirrors applySettingsJsonHooks' settings.json wiring
 * 1:1 by GSD hook script (update check, session-state, phase-boundary,
 * graphify, context monitor, prompt/read/workflow/worktree guards, commit
 * validation). Kimi's 13 lifecycle events include exact-name equivalents for
 * every Claude-dialect event GSD currently wires (SessionStart, PreToolUse,
 * PostToolUse, Stop, PreCompact, SubagentStart, SubagentStop) — see
 * moonshotai.github.io/kimi-cli/en/customization/hooks.html.
 *
 * Matcher translation (best-effort — Kimi's tool-name vocabulary is
 * confirmed distinct from Claude's by the upstream hooks doc's own examples):
 *   Bash -> Shell, Write -> WriteFile, Edit/MultiEdit -> StrReplaceFile.
 * Read -> ReadFile follows the same WriteFile/StrReplaceFile naming
 * convention but is not independently doc-confirmed. Claude's Agent|Task
 * (subagent-dispatch) matcher segment has no confirmed Kimi tool name and is
 * dropped rather than guessed — gsd-context-monitor's PostToolUse entry runs
 * unmatched (all tools) instead, which only widens when it fires, it never
 * narrows incorrectly.
 */
function buildKimiHooksTomlBlock(targetDir, opts) {
    const { hookOpts } = opts;
    const cmd = (hookName) => {
        if (!node_fs_1.default.existsSync(node_path_1.default.join(targetDir, 'hooks', hookName)))
            return null;
        return buildHookCommand(targetDir, hookName, hookOpts);
    };
    const specs = [
        // SessionStart — unmatched (session-level; no tool_name to filter on).
        { event: 'SessionStart', command: cmd('gsd-check-update.js') },
        { event: 'SessionStart', command: cmd('gsd-session-state.sh') },
        // PreToolUse
        { event: 'PreToolUse', command: cmd('gsd-prompt-guard.js'), matcher: 'WriteFile|StrReplaceFile', timeout: 5 },
        { event: 'PreToolUse', command: cmd('gsd-read-guard.js'), matcher: 'WriteFile|StrReplaceFile', timeout: 5 },
        { event: 'PreToolUse', command: cmd('gsd-worktree-path-guard.js'), matcher: 'WriteFile|StrReplaceFile', timeout: 5 },
        { event: 'PreToolUse', command: cmd('gsd-workflow-guard.js'), matcher: 'Shell|WriteFile|StrReplaceFile', timeout: 5 },
        { event: 'PreToolUse', command: cmd('gsd-validate-commit.sh'), matcher: 'Shell', timeout: 5 },
        // PostToolUse
        { event: 'PostToolUse', command: cmd('gsd-context-monitor.js'), timeout: 10 },
        { event: 'PostToolUse', command: cmd('gsd-phase-boundary.sh'), matcher: 'WriteFile|StrReplaceFile', timeout: 5 },
        { event: 'PostToolUse', command: cmd('gsd-read-injection-scanner.js'), matcher: 'ReadFile', timeout: 5 },
        { event: 'PostToolUse', command: cmd('gsd-graphify-update.sh'), matcher: 'Shell', timeout: 5 },
        // Extended lifecycle events — context-headroom tracking (unmatched).
        { event: 'Stop', command: cmd('gsd-context-monitor.js'), timeout: 10 },
        { event: 'PreCompact', command: cmd('gsd-context-monitor.js'), timeout: 10 },
        { event: 'SubagentStart', command: cmd('gsd-context-monitor.js'), timeout: 10 },
        { event: 'SubagentStop', command: cmd('gsd-context-monitor.js'), timeout: 10 },
    ];
    const entries = specs
        .map(buildKimiHookEntryToml)
        .filter((entry) => entry !== null);
    if (entries.length === 0)
        return null;
    return [KIMI_HOOKS_TOML_MARKER_BEGIN, '', entries.join('\n\n'), '', KIMI_HOOKS_TOML_MARKER_END].join('\n');
}
/**
 * Strip a previously-written GSD [[hooks]] block from kimi's config.toml
 * content. Pure string function (no fs access) so install, uninstall, and
 * tests share one strip implementation. Returns null when stripping leaves
 * nothing but whitespace (the file was GSD-only), so the caller can unlink
 * it instead of writing an empty file.
 */
function stripKimiHooksTomlBlock(content) {
    const beginIdx = content.indexOf(KIMI_HOOKS_TOML_MARKER_BEGIN);
    if (beginIdx === -1) {
        return content.trim() === '' ? null : content;
    }
    const endMarkerIdx = content.indexOf(KIMI_HOOKS_TOML_MARKER_END, beginIdx);
    if (endMarkerIdx === -1) {
        // Malformed marker pair — BEGIN present but no END after it (missing END,
        // or an END that only appears earlier in the file, before BEGIN). Never
        // fall back to content.length here: that would slice to EOF and destroy
        // every user section that follows. Leave the content untouched instead;
        // a subsequent writeKimiHooksToml call will append a fresh, well-formed
        // block rather than silently deleting user data.
        return content;
    }
    const endIdx = endMarkerIdx + KIMI_HOOKS_TOML_MARKER_END.length;
    // Swallow blank lines immediately surrounding the block so repeated
    // strip+rewrite cycles never accumulate blank lines.
    let sliceStart = beginIdx;
    while (sliceStart > 0 && (content[sliceStart - 1] === '\n' || content[sliceStart - 1] === '\r'))
        sliceStart -= 1;
    let sliceEnd = endIdx;
    while (sliceEnd < content.length && (content[sliceEnd] === '\n' || content[sliceEnd] === '\r'))
        sliceEnd += 1;
    const before = content.slice(0, sliceStart);
    const after = content.slice(sliceEnd);
    // Blank-line swallowing above consumes every newline flanking the block,
    // including the one required to keep the surrounding user sections on
    // separate lines. If the block sat BETWEEN two user sections (content
    // survives on both sides), concatenating `before` + `after` directly would
    // glue the last line of the earlier section onto the first line of the
    // later one. Reinsert a blank-line separator in that case; when only one
    // side has content (block at file start or EOF), no separator is needed —
    // that matches the pre-existing idempotent behavior for those shapes.
    const result = before.trim() !== '' && after.trim() !== ''
        ? `${before}\n\n${after}`
        : before + after;
    return result.trim() === '' ? null : result;
}
/**
 * Idempotently (re)write kimi's GSD-owned [[hooks]] block into its native
 * config.toml at `configPath` (resolved by the caller via
 * resolveKimiHooksTomlDir). No-ops (`{changed:false}`) when the computed
 * block is byte-identical to what's already on disk, so reinstalls don't
 * touch the file's mtime for no reason.
 */
function writeKimiHooksToml(configPath, targetDir, opts) {
    const existing = node_fs_1.default.existsSync(configPath) ? node_fs_1.default.readFileSync(configPath, 'utf8') : '';
    const stripped = stripKimiHooksTomlBlock(existing) ?? '';
    const block = buildKimiHooksTomlBlock(targetDir, opts);
    const entryCount = block ? (block.match(/\[\[hooks\]\]/g) || []).length : 0;
    if (!block) {
        if (stripped === existing)
            return { changed: false, path: configPath, entryCount: 0 };
        if (stripped.trim() === '') {
            if (node_fs_1.default.existsSync(configPath))
                node_fs_1.default.unlinkSync(configPath);
        }
        else {
            node_fs_1.default.mkdirSync(node_path_1.default.dirname(configPath), { recursive: true });
            atomicWriteFileSync(configPath, stripped, 'utf8');
        }
        return { changed: true, path: configPath, entryCount: 0 };
    }
    const separator = stripped.trim() === '' ? '' : (stripped.endsWith('\n') ? '\n' : '\n\n');
    const next = stripped.trim() === '' ? `${block}\n` : `${stripped}${separator}${block}\n`;
    if (next === existing)
        return { changed: false, path: configPath, entryCount };
    node_fs_1.default.mkdirSync(node_path_1.default.dirname(configPath), { recursive: true });
    atomicWriteFileSync(configPath, next, 'utf8');
    return { changed: true, path: configPath, entryCount };
}
/**
 * Uninstall-time counterpart to writeKimiHooksToml: strips the GSD block and
 * deletes the file if nothing but GSD's own block was ever in it.
 */
function removeKimiHooksToml(configPath) {
    if (!node_fs_1.default.existsSync(configPath))
        return { changed: false };
    const existing = node_fs_1.default.readFileSync(configPath, 'utf8');
    const stripped = stripKimiHooksTomlBlock(existing);
    if (stripped === existing)
        return { changed: false };
    if (stripped === null || stripped.trim() === '') {
        node_fs_1.default.unlinkSync(configPath);
    }
    else {
        atomicWriteFileSync(configPath, stripped, 'utf8');
    }
    return { changed: true };
}
// ---------------------------------------------------------------------------
// referencesHook
//
// Pure predicate — checks whether a hook entry object references a managed
// hook by name.  Covers all three registration shapes used by GSD:
//   • plain command string (standard form)
//   • args array (command+args / wrapped-launcher form used by windowless
//     launchers on Windows and some custom PATH-less environments) (#976)
//   • url field (type:"http" local-server routing form) (#1004)
// Without covering all three, an http-form or args-form entry is invisible
// and a stock string-command entry is appended on every install/update,
// running the hook twice.
//
// Originally declared inside install()/finishInstall() as a local function;
// promoted here so applySettingsJsonHooks() and finishInstall() share one
// copy (ADR-857 phase 5f-1b).
// ---------------------------------------------------------------------------
function referencesHook(h, hookName) {
    const cmd = h['command'];
    const args = h['args'];
    const url = h['url'];
    return (typeof cmd === 'string' && cmd.includes(hookName)) ||
        (Array.isArray(args) && args.some(a => typeof a === 'string' && a.includes(hookName))) ||
        (typeof url === 'string' && url.includes(hookName));
}
module.exports = {
    // Cline
    buildClineRulesBody,
    buildClineAgentsMdBody,
    buildClinePreToolUseHook,
    mergeGsdAgentsMd,
    writeClineArtifacts,
    GSD_AGENTS_MD_MARKER,
    GSD_AGENTS_MD_CLOSE_MARKER,
    // Cursor
    buildCursorHookEntry,
    isManagedCursorHookEntry,
    reconcileCursorHooksJson,
    writeCursorHooksJson,
    removeCursorHooksJson,
    GSD_CURSOR_SESSION_HOOK_SCRIPT,
    GSD_CURSOR_POST_TOOL_HOOK_SCRIPT,
    GSD_CURSOR_PRE_TOOL_HOOK_SCRIPT,
    GSD_CURSOR_STOP_HOOK_SCRIPT,
    GSD_CURSOR_SUBAGENT_START_HOOK_SCRIPT,
    GSD_CURSOR_SUBAGENT_STOP_HOOK_SCRIPT,
    GSD_CURSOR_HOOK_MARKER,
    // Windsurf/Cascade
    buildWindsurfHookEntry,
    isManagedWindsurfHookEntry,
    reconcileWindsurfHooksJson,
    writeWindsurfHooksJson,
    removeWindsurfHooksJson,
    WINDSURF_HOOK_EVENTS,
    WINDSURF_EVENT_SCRIPT_MAP,
    GSD_WINDSURF_PRE_WRITE_HOOK_SCRIPT,
    GSD_WINDSURF_PRE_COMMAND_HOOK_SCRIPT,
    GSD_WINDSURF_HOOK_SCRIPTS,
    GSD_WINDSURF_HOOK_MARKER,
    // Copilot
    buildCopilotHookConfig,
    writeCopilotHookConfig,
    GSD_COPILOT_HOOK_FILE,
    // Codex hooks.json
    reconcileCodexHooksJsonEvent,
    reconcileCodexHooksJsonSessionStart,
    ensureCodexHooksJsonSessionStart,
    ensureCodexHooksJsonEvent,
    removeCodexHooksJsonEvent,
    removeCodexHooksJsonSessionStart,
    buildCodexHookWindowsShimIR,
    // Codex TOML
    buildCodexHookBlock,
    rewriteLegacyCodexHookBlock,
    // Kimi hooks.toml
    buildKimiHooksTomlBlock,
    stripKimiHooksTomlBlock,
    writeKimiHooksToml,
    removeKimiHooksToml,
    KIMI_HOOKS_TOML_MARKER_BEGIN,
    KIMI_HOOKS_TOML_MARKER_END,
    // Shared
    buildHookCommand,
    applySettingsJsonHooks,
    referencesHook,
    rewriteLegacyManagedNodeHookCommands,
    normalizeNodePath,
    resolveNodeRunner,
    resolveBashRunner,
    // Atomic write seam (shared with bin/install.js so all writes participate
    // in install.js's _cleanTmpFiles() scoped temp-cleanup).
    atomicWriteFileSync,
    __atomicWrittenTmps,
};
