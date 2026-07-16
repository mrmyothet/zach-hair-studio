"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
/**
 * install-effort-resolver — install-time effort resolution (#443), extracted from
 * the package-root `bin/install.js` (#2071).
 *
 * `gsd-tools effort sync` (src/commands.cts `cmdEffortSync`) must mirror what the
 * installer writes — home `~/.gsd/defaults.json` merged with the project's
 * `.planning/config.json` — which the runtime resolver (`resolveEffortInternal`
 * via `loadConfig`) does NOT do. It previously reached those two functions via
 * `require('../../../bin/install.js')`, but the installer never copies the
 * package-root `bin/install.js` into a runtime home, so `effort sync` crashed with
 * MODULE_NOT_FOUND in every installed runtime (#2071). Moving the logic here — a
 * `src/*.cts` module compiled into the shipped `gsd-core/bin/lib/` tree — lets both
 * the installer AND `effort sync` require it from a location that is always present,
 * keeping a single source of truth (no duplication / drift).
 *
 * Pure with respect to config: `readGsdEffectiveEffortConfig` performs the config
 * reads; `resolveInstallTimeEffort` is pure given a pre-merged effort object.
 */
const node_fs_1 = __importDefault(require("node:fs"));
const node_path_1 = __importDefault(require("node:path"));
const node_os_1 = __importDefault(require("node:os"));
// eslint-disable-next-line @typescript-eslint/no-require-imports -- model-resolver.cjs is an export= CommonJS module
const modelResolver = require("./model-resolver.cjs");
const { EFFORT_SET: GSD_EFFORT_SET } = modelResolver;
/**
 * #2517 — Read a single GSD config file (defaults.json or per-project
 * config.json) into a plain object, returning null on missing/empty files
 * and warning to stderr on JSON parse failures so silent corruption can't
 * mask broken configs (review finding #5).
 */
function _readGsdConfigFile(absPath, label) {
    if (!node_fs_1.default.existsSync(absPath))
        return null;
    let raw;
    try {
        raw = node_fs_1.default.readFileSync(absPath, 'utf-8');
    }
    catch (err) {
        process.stderr.write(`gsd: warning — could not read ${label} (${absPath}): ${err.message}\n`);
        return null;
    }
    try {
        return JSON.parse(raw);
    }
    catch (err) {
        process.stderr.write(`gsd: warning — invalid JSON in ${label} (${absPath}): ${err.message}\n`);
        return null;
    }
}
// #443 — model-catalog and config-defaults.manifest.json exports needed only
// by effort-resolution code paths (resolveInstallTimeEffort /
// generateCodexAgentToml / Claude .md effort injection).  Loaded lazily the
// first time they are needed so that requiring this module in test contexts that
// never trigger an install does NOT produce module-load-time side effects (the
// manifest read + hard throw) that could alter subprocess exit codes or stderr.
let _gsdEffortCatalogCache = null;
function _getGsdEffortCatalog() {
    if (_gsdEffortCatalogCache)
        return _gsdEffortCatalogCache;
    // eslint-disable-next-line @typescript-eslint/no-require-imports -- model-catalog.cjs is an export= CommonJS module
    const { AGENT_DEFAULT_TIERS, renderEffortForRuntime } = require('./model-catalog.cjs');
    // This module lives in gsd-core/bin/lib/, so the shared manifest is one level
    // up in gsd-core/bin/shared/ (bin/lib → bin → shared). (In bin/install.js this
    // path was `.., gsd-core, bin, shared` relative to the package-root bin/.)
    const manifestPath = node_path_1.default.join(__dirname, '..', 'shared', 'config-defaults.manifest.json');
    let manifestData;
    try {
        manifestData = JSON.parse(node_fs_1.default.readFileSync(manifestPath, 'utf-8'));
    }
    catch (_err) {
        // Fail loudly — a missing manifest is a broken install, not a soft degradation.
        throw new Error(`gsd install: cannot load config-defaults.manifest.json at ${manifestPath}: ${_err.message}`);
    }
    const tierDefaults = (manifestData.effort &&
        manifestData.effort.routing_tier_defaults &&
        typeof manifestData.effort.routing_tier_defaults === 'object' &&
        !Array.isArray(manifestData.effort.routing_tier_defaults))
        ? manifestData.effort.routing_tier_defaults
        : { light: 'low', standard: 'high', heavy: 'xhigh' }; // guard: unreachable if manifest is valid
    const effortDefault = (manifestData.effort && typeof manifestData.effort.default === 'string')
        ? manifestData.effort.default
        : 'high'; // guard: unreachable if manifest is valid
    _gsdEffortCatalogCache = {
        AGENT_DEFAULT_TIERS,
        renderEffortForRuntime,
        EFFORT_MANIFEST_TIER_DEFAULTS: tierDefaults,
        EFFORT_MANIFEST_DEFAULT: effortDefault,
    };
    return _gsdEffortCatalogCache;
}
/**
 * #443 — Read the merged `effort` config block for install-time effort resolution.
 *
 * Probes the same config sources as readGsdRuntimeProfileResolver (per-project
 * `.planning/config.json` wins over `~/.gsd/defaults.json`) but extracts the
 * `effort` object instead of the model-profile fields.
 *
 * Returns the merged `effort` object or null when neither source defines one.
 * The caller can pass this to resolveInstallTimeEffort() which is pure and
 * requires no filesystem access beyond what this helper already performs.
 *
 * @param targetDir  Runtime install root (walks up to find .planning/).
 */
function readGsdEffectiveEffortConfig(targetDir = null) {
    const homeDefaults = _readGsdConfigFile(node_path_1.default.join(node_os_1.default.homedir(), '.gsd', 'defaults.json'), '~/.gsd/defaults.json');
    let projectConfig = null;
    if (targetDir) {
        let probeDir = node_path_1.default.resolve(targetDir);
        for (let depth = 0; depth < 8; depth += 1) {
            const candidate = node_path_1.default.join(probeDir, '.planning', 'config.json');
            if (node_fs_1.default.existsSync(candidate)) {
                projectConfig = _readGsdConfigFile(candidate, '.planning/config.json');
                break;
            }
            const parent = node_path_1.default.dirname(probeDir);
            if (parent === probeDir)
                break;
            probeDir = parent;
        }
    }
    const homeEffort = (homeDefaults && homeDefaults.effort && typeof homeDefaults.effort === 'object' && !Array.isArray(homeDefaults.effort))
        ? homeDefaults.effort
        : null;
    const projectEffort = (projectConfig && projectConfig.effort && typeof projectConfig.effort === 'object' && !Array.isArray(projectConfig.effort))
        ? projectConfig.effort
        : null;
    if (!homeEffort && !projectEffort)
        return null;
    // Per-project wins on conflict within each sub-field. Merge field-by-field so
    // a project config that only sets agent_overrides still inherits global
    // routing_tier_defaults and default.
    return {
        ...(homeEffort || {}),
        ...(projectEffort || {}),
        // Deep-merge agent_overrides (project wins per-key)
        agent_overrides: {
            ...((homeEffort && homeEffort.agent_overrides) || {}),
            ...((projectEffort && projectEffort.agent_overrides) || {}),
        },
    };
}
/**
 * #443 — Resolve install-time effort for a given agent, using the same
 * precedence chain as resolveEffortInternal() in core.cjs, but operating
 * on a pre-loaded effortCfg object (no loadConfig side-effects at install).
 *
 * Precedence (mirrors resolveEffortInternal):
 *   1. effortCfg.agent_overrides[agentName]
 *   2. effortCfg.routing_tier_defaults[agentTier]  (if effortCfg present)
 *      — OR manifest tier defaults when effortCfg is null
 *   3. effortCfg.default
 *   4. 'high' (hardcoded fallback)
 *
 * @param effortCfg   Result of readGsdEffectiveEffortConfig().
 * @param agentName   e.g. 'gsd-planner'
 * @returns           Universal effort string (low/medium/high/xhigh/max/minimal)
 */
function resolveInstallTimeEffort(effortCfg, agentName) {
    // Validates each candidate against the canonical EFFORT_SET (sourced once
    // from model-resolver.cjs) before accepting it, mirroring resolveEffortInternal
    // exactly. Invalid values fall through to the next precedence layer; final
    // fallback 'high'.
    // Step 1: agent_overrides
    if (effortCfg) {
        const ao = effortCfg.agent_overrides;
        if (ao && typeof ao === 'object' && !Array.isArray(ao)) {
            const v = ao[agentName];
            if (typeof v === 'string' && GSD_EFFORT_SET.has(v))
                return v;
        }
    }
    // Step 2: routing_tier_defaults keyed by the agent's catalog tier
    const { AGENT_DEFAULT_TIERS, EFFORT_MANIFEST_TIER_DEFAULTS, EFFORT_MANIFEST_DEFAULT } = _getGsdEffortCatalog();
    const agentTier = AGENT_DEFAULT_TIERS[agentName];
    if (agentTier) {
        if (effortCfg && effortCfg.routing_tier_defaults &&
            typeof effortCfg.routing_tier_defaults === 'object' &&
            !Array.isArray(effortCfg.routing_tier_defaults)) {
            const v = effortCfg.routing_tier_defaults[agentTier];
            if (typeof v === 'string' && GSD_EFFORT_SET.has(v))
                return v;
        }
        else if (!effortCfg) {
            // No effort config — use manifest tier defaults
            const v = EFFORT_MANIFEST_TIER_DEFAULTS[agentTier];
            if (typeof v === 'string' && GSD_EFFORT_SET.has(v))
                return v;
        }
        // effortCfg exists but has no routing_tier_defaults — fall through
    }
    // Step 3: effort.default
    if (effortCfg) {
        const d = effortCfg.default;
        if (typeof d === 'string' && GSD_EFFORT_SET.has(d))
            return d;
    }
    // Step 4: manifest default (sourced from config-defaults.manifest.json effort.default)
    // If even the manifest default is invalid, fall back to 'high'.
    if (typeof EFFORT_MANIFEST_DEFAULT === 'string' && GSD_EFFORT_SET.has(EFFORT_MANIFEST_DEFAULT)) {
        return EFFORT_MANIFEST_DEFAULT;
    }
    return 'high';
}
module.exports = {
    readGsdEffectiveEffortConfig,
    resolveInstallTimeEffort,
    _getGsdEffortCatalog,
    _readGsdConfigFile,
};
