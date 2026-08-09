"use strict";
/**
 * Model Resolver — Model and effort resolution policy
 *
 * ADR-857 rollout phase 2f: extracted from core.cts (issue #888).
 * Owns model and effort resolution policy: resolves the model, runtime tier,
 * planning granularity, reasoning effort, and fast-mode for a given agent by
 * reading project config and resolving against the model profiles and catalog.
 * Behaviour is preserved byte-for-behaviour from the prior location; only
 * the module boundary moved. The core.cjs re-export spine was retired in
 * epic #1267; callers import resolvers from model-resolver.cjs directly.
 *
 * Dependencies (leaf modules only):
 *   - node:fs / node:path (read the per-install .gsd-runtime marker + project config for the #2297 omit gate)
 *   - ./runtime-name-policy.cjs (resolveRuntimeNameFromCandidates — canonicalize the active runtime)
 *   - ./planning-workspace.cjs  (planningDir — workstream/project-aware project-config path)
 *   - ./config-loader.cjs    (loadConfig)
 *   - ./configuration.cjs    (CONFIG_DEFAULTS as CANONICAL_CONFIG_DEFAULTS)
 *   - ./model-profiles.cjs   (MODEL_PROFILES, AGENT_TO_PHASE_TYPE, AGENT_DEFAULT_TIERS, VALID_AGENT_TIERS, nextTier)
 *   - ./model-catalog.cjs    (MODEL_ALIAS_MAP, RUNTIME_PROFILE_MAP, PROVIDER_PRESETS, VALID_TIERS)
 */
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
// eslint-disable-next-line @typescript-eslint/no-require-imports
const configLoaderModule = require("./config-loader.cjs");
const { loadConfig } = configLoaderModule;
// ─── Configuration Module (for CANONICAL_CONFIG_DEFAULTS used by effort/fast_mode resolvers) ─
const configuration_cjs_1 = require("./configuration.cjs");
// eslint-disable-next-line @typescript-eslint/no-require-imports
const modelProfiles = require("./model-profiles.cjs");
const { MODEL_PROFILES, AGENT_TO_PHASE_TYPE, AGENT_DEFAULT_TIERS, VALID_AGENT_TIERS, nextTier } = modelProfiles;
const model_catalog_cjs_1 = require("./model-catalog.cjs");
const node_fs_1 = __importDefault(require("node:fs"));
const node_path_1 = __importDefault(require("node:path"));
const runtime_name_policy_cjs_1 = require("./runtime-name-policy.cjs");
// eslint-disable-next-line @typescript-eslint/no-require-imports
const planningWorkspaceMod = require("./planning-workspace.cjs");
const { planningDir } = planningWorkspaceMod;
// ─── #2297: per-install runtime identity for the resolve_model_ids:"omit" gate ─
//
// The installer writes `resolve_model_ids:"omit"` into the SHARED
// ~/.gsd/defaults.json for every runtime that lacks native model aliases (#1156).
// Because that file is machine-wide, a non-Claude install would otherwise poison
// a Claude no-project resolution into returning '' — silently defeating Claude's
// adaptive tier aliases. The "omit" must therefore apply only when a runtime that
// genuinely lacks native aliases is the one resolving.
//
// In a no-project session there is no `.planning/config.json` (so config.runtime
// is null) and GSD_RUNTIME is not exported by gsd-core, so the only reliable
// current-runtime signal is the per-install marker the installer co-locates next
// to VERSION at <install>/gsd-core/.gsd-runtime (this file's dir is
// <install>/gsd-core/bin/lib). Precedence for the gate: project config.runtime →
// GSD_RUNTIME env (manual/CI override + test seam) → install marker → 'claude'.
//
// `claude` is currently the ONLY runtime with nativeModelAliases:true; a
// registry-parity test guards this set so a future alias-capable runtime fails
// loudly here instead of silently omitting.
const RUNTIMES_WITH_NATIVE_ALIASES = new Set(['claude']);
let _installMarkerCache;
function readInstallRuntimeMarker() {
    if (_installMarkerCache !== undefined)
        return _installMarkerCache;
    try {
        const markerPath = node_path_1.default.join(__dirname, '..', '..', '.gsd-runtime');
        const raw = node_fs_1.default.readFileSync(markerPath, 'utf8').trim();
        _installMarkerCache = raw || null;
    }
    catch {
        // No marker: dev/source tree, or an install predating #2297. Fall through to
        // the 'claude' default (keeps tier aliases — never worse than the bug).
        _installMarkerCache = null;
    }
    return _installMarkerCache;
}
// Test seams for the install-marker rung (the dev/source tree has no marker, so
// the file read always bottoms out at 'claude' — these let tests exercise the
// third precedence rung and reset the module-level cache between cases).
function _setInstallRuntimeMarkerForTests(value) {
    _installMarkerCache = value;
}
function _resetInstallRuntimeMarkerCacheForTests() {
    _installMarkerCache = undefined;
}
// The runtime whose install is actually resolving, canonicalized so an alias or
// case variant (e.g. "claude-code"/"Claude") cannot defeat the native-alias
// check below (#2297 review). Precedence mirrors resolveRuntime()
// (runtime-slash.cts): GSD_RUNTIME env → project config.runtime → per-install
// .gsd-runtime marker → 'claude'.
function resolveActiveRuntime(config) {
    return (0, runtime_name_policy_cjs_1.resolveRuntimeNameFromCandidates)(process.env['GSD_RUNTIME'], config['runtime'], readInstallRuntimeMarker()) || 'claude';
}
// Did the PROJECT's own config (root `.planning/config.json` or the active
// workstream/project override) explicitly set resolve_model_ids to "omit"?
// Project config takes precedence over the shared ~/.gsd/defaults.json (#2297
// out-of-scope guard + #2517 finding #4): an explicit project "omit" is honored
// regardless of runtime, whereas an "omit" that came only from the global
// defaults is ignored by native-alias runtimes. Workstream/project-scope aware
// via planningDir (mirrors loadConfig's precedence: workstream value wins over
// root); a plain read avoids loadConfig's normalization side effects.
function projectExplicitlySetsOmit(cwd) {
    const wsDir = planningDir(cwd);
    const rootDir = node_path_1.default.join(cwd, '.planning');
    const layers = wsDir === rootDir ? [rootDir] : [wsDir, rootDir]; // workstream > root
    for (const dir of layers) {
        try {
            const parsed = JSON.parse(node_fs_1.default.readFileSync(node_path_1.default.join(dir, 'config.json'), 'utf8'));
            const value = parsed?.['resolve_model_ids'];
            // First layer that sets the key wins (matches loadConfig's deep-merge
            // precedence). A layer that omits the key falls through to the next.
            if (value !== undefined)
                return value === 'omit';
        }
        catch {
            // Absent/unreadable layer — try the next.
        }
    }
    return false;
}
/**
 * #2517 — Resolve the runtime-aware tier entry for (runtime, tier).
 */
function resolveTierEntry({ runtime, tier, overrides }) {
    if (!runtime || !tier)
        return null;
    const runtimeMap = model_catalog_cjs_1.RUNTIME_PROFILE_MAP;
    const builtin = runtimeMap[runtime]?.[tier] || null;
    const overridesMap = overrides;
    const userRaw = overridesMap?.[runtime]?.[tier];
    let userEntry = null;
    if (userRaw) {
        userEntry = typeof userRaw === 'string' ? { model: userRaw } : userRaw;
    }
    if (!builtin && !userEntry)
        return null;
    return { ...(builtin || {}), ...(userEntry || {}) };
}
/**
 * Convenience wrapper used by resolveModelInternal.
 */
function _resolveRuntimeTier(config, tier) {
    return resolveTierEntry({
        runtime: config['runtime'],
        tier,
        overrides: config['model_profile_overrides'],
    });
}
// Reverse of the Claude tier-default IDs, plus the Fable alias which Claude
// Code's Agent tool accepts but which is not a GSD model-profile tier (#1133).
const CLAUDE_POLICY_ID_TO_ALIAS = {
    ...Object.fromEntries(Object.entries(model_catalog_cjs_1.MODEL_ALIAS_MAP)
        .filter((e) => typeof e[1] === 'string')
        .map(([aliasName, id]) => [id, aliasName])),
    'claude-fable-5': 'fable',
};
const CLAUDE_AGENT_ALIASES = new Set(['opus', 'sonnet', 'haiku', 'fable']);
// Dedupe stderr warnings so repeated agent resolutions don't spam (#1133).
const _modelPolicyUnmappableWarned = new Set();
function warnModelPolicyUnmappable(agentType, policyModel, tier) {
    const key = `${agentType}::${policyModel}::${tier}`;
    if (_modelPolicyUnmappableWarned.has(key))
        return;
    _modelPolicyUnmappableWarned.add(key);
    // MUST go to stderr — resolve-model's JSON result is parsed from stdout.
    process.stderr.write(`gsd: warning — model_policy resolved "${policyModel}" for ${agentType}, ` +
        `but it has no Claude agent alias; using "${tier}" instead.\n`);
}
// Test-only: reset the model_policy warn-dedupe cache between cases (#1133).
function _resetModelPolicyWarningCacheForTests() {
    _modelPolicyUnmappableWarned.clear();
}
// Dedupe stderr warnings for unmappable model_overrides Claude IDs (#2041).
const _modelOverrideUnmappableWarned = new Set();
function warnModelOverrideUnmappable(agentType, overrideValue) {
    const key = `${agentType}::${overrideValue}`;
    if (_modelOverrideUnmappableWarned.has(key))
        return;
    _modelOverrideUnmappableWarned.add(key);
    // Cap emission length so an oversized or secret-shaped value cannot leak in
    // full to stderr/logs (#2041 security review). MUST go to stderr — resolve-
    // model's JSON result is parsed from stdout.
    const safe = overrideValue.length > 64 ? overrideValue.slice(0, 64) + '…' : overrideValue;
    process.stderr.write(`gsd: warning — model_overrides value "${safe}" for ${agentType} ` +
        `has no Claude agent alias; falling through to tier resolution.\n`);
}
// Test-only: reset the model_overrides warn-dedupe cache between cases (#2041).
function _resetModelOverrideWarningCacheForTests() {
    _modelOverrideUnmappableWarned.clear();
}
/**
 * #2041 — Map a `model_overrides` value to its Claude Agent-tool alias on the
 * claude runtime, mirroring the `model_policy` path (#1144). Claude Code's
 * Agent tool `model` parameter documents only tier aliases (opus/sonnet/haiku/
 * fable); a full Claude model ID returned verbatim is silently dropped by the
 * spawner. Returns the value to return verbatim, or null to signal "fall
 * through to normal tier/dynamic-routing resolution" (used when a Claude full
 * ID has no alias — matches model_policy's warn-and-fall-through). Non-Claude
 * runtimes and non-Claude values always pass through verbatim.
 *
 * Hardening (code+security review): a `typeof` guard preserves the pre-fix
 * no-crash behavior if a malformed config surfaces a non-string value, and an
 * `Object.hasOwn` lookup defeats `__proto__`/`constructor` lookups on the plain
 * object literal so those reserved keys cannot return a truthy non-string.
 */
function mapClaudeOverrideForRuntime(override, configRuntime, agentType) {
    // Defensive: model_overrides is typed Record<string,string> but a malformed
    // config could surface a non-string; pass through verbatim (preserving the
    // pre-fix no-crash behaviour) and let the downstream Agent tool reject it.
    if (typeof override !== 'string')
        return override;
    const onClaude = !configRuntime || configRuntime === 'claude';
    if (!onClaude)
        return override;
    // Object.hasOwn guards against __proto__/constructor returning a truthy
    // non-string from the plain object literal (#2041 security review).
    if (Object.hasOwn(CLAUDE_POLICY_ID_TO_ALIAS, override)) {
        return CLAUDE_POLICY_ID_TO_ALIAS[override];
    }
    if (CLAUDE_AGENT_ALIASES.has(override))
        return override;
    if (override.startsWith('claude-')) {
        warnModelOverrideUnmappable(agentType, override);
        return null;
    }
    return override;
}
/**
 * #49 — Provider-neutral model policy preset resolution.
 */
function resolveModelPolicy(policy, tier) {
    if (!policy || typeof policy !== 'object')
        return null;
    if (!tier)
        return null;
    const runtime = policy['runtime'];
    const rtOverrides = policy['runtime_tiers'];
    if (runtime && typeof runtime === 'string' && rtOverrides && typeof rtOverrides === 'object') {
        const rtOverridesMap = rtOverrides;
        if (Object.hasOwn(rtOverridesMap, runtime)) {
            const runtimeEntry = rtOverridesMap[runtime];
            if (runtimeEntry && typeof runtimeEntry === 'object' && Object.hasOwn(runtimeEntry, tier)) {
                const raw = runtimeEntry[tier];
                if (raw != null) {
                    const entry = typeof raw === 'string' ? { model: raw } : raw;
                    if (entry && entry['model'])
                        return entry['model'];
                }
            }
        }
    }
    const provider = policy['provider'];
    if (!provider || typeof provider !== 'string')
        return null;
    if (provider === 'generic' || provider === 'custom') {
        const TIER_TO_POLICY_KEY = { opus: 'high', sonnet: 'medium', haiku: 'low' };
        const policyKey = TIER_TO_POLICY_KEY[tier];
        if (!policyKey)
            return null;
        const v = policy[policyKey];
        return (v && typeof v === 'string') ? v : null;
    }
    const presetsMap = model_catalog_cjs_1.PROVIDER_PRESETS;
    if (!Object.hasOwn(presetsMap, provider))
        return null;
    const presetForProvider = presetsMap[provider];
    if (!presetForProvider || typeof presetForProvider !== 'object')
        return null;
    if (!Object.hasOwn(presetForProvider, tier))
        return null;
    const tierPresets = presetForProvider[tier];
    if (!tierPresets || typeof tierPresets !== 'object')
        return null;
    const budget = (policy['budget'] && typeof policy['budget'] === 'string') ? policy['budget'] : 'medium';
    if (!Object.hasOwn(tierPresets, budget))
        return null;
    const budgetEntry = tierPresets[budget];
    if (!budgetEntry || !budgetEntry.model)
        return null;
    return budgetEntry.model;
}
function resolveModelInternal(cwd, agentType) {
    const config = loadConfig(cwd);
    // 1. Per-agent override (#2041: map Claude full IDs → Agent-tool aliases on
    // the claude runtime, mirroring the model_policy path #1144; non-Claude
    // runtimes and non-Claude values pass through verbatim).
    const modelOverrides = config['model_overrides'];
    const override = modelOverrides?.[agentType];
    if (override) {
        const mapped = mapClaudeOverrideForRuntime(override, config['runtime'], agentType);
        if (mapped !== null)
            return mapped;
        // Unmappable Claude ID — fall through to tier resolution (matches model_policy).
    }
    // 2. Compute the tier
    // eslint-disable-next-line @typescript-eslint/no-base-to-string
    const profile = String(config['model_profile'] || 'balanced').toLowerCase();
    const agentModels = MODEL_PROFILES[agentType];
    const phaseType = (AGENT_TO_PHASE_TYPE)[agentType];
    const configModels = config['models'];
    const phaseTypeTier = (phaseType && configModels && typeof configModels === 'object')
        ? configModels[phaseType]
        : undefined;
    const tier = (phaseTypeTier && model_catalog_cjs_1.VALID_TIERS.has(phaseTypeTier))
        ? phaseTypeTier
        : (profile === 'inherit'
            ? 'inherit'
            : (agentModels ? (agentModels[profile] || agentModels['balanced']) : null));
    // 2.5. model_policy preset (#49, #1133)
    const configRuntime = config['runtime'];
    if (tier && tier !== 'inherit') {
        const onClaude = !configRuntime || configRuntime === 'claude';
        const effectiveRuntime = configRuntime || 'claude';
        const mergedPolicy = config['model_policy']
            ? { ...config['model_policy'], runtime: effectiveRuntime }
            : null;
        const policyModel = resolveModelPolicy(mergedPolicy, tier);
        if (policyModel) {
            // Non-Claude runtimes take full model IDs verbatim (unchanged behavior).
            if (!onClaude)
                return policyModel;
            // Claude Code's Agent tool takes tier aliases (opus/sonnet/haiku/fable),
            // not full model IDs — map the policy-resolved ID back to an alias (#1133).
            const aliasForId = CLAUDE_POLICY_ID_TO_ALIAS[policyModel];
            if (aliasForId)
                return aliasForId;
            // The policy value may already be a bare Claude agent alias (e.g. "fable").
            if (CLAUDE_AGENT_ALIASES.has(policyModel))
                return policyModel;
            // No Claude alias for this ID (e.g. a pinned minor version like
            // claude-opus-4-5). Warn once and fall through to the tier alias rather
            // than returning an ID Claude Code cannot spawn.
            warnModelPolicyUnmappable(agentType, policyModel, tier);
        }
    }
    // 3. Runtime-aware resolution (#2517)
    if (configRuntime && configRuntime !== 'claude' && tier && tier !== 'inherit') {
        const entry = _resolveRuntimeTier(config, tier);
        if (entry?.model)
            return entry.model;
    }
    // 4. resolve_model_ids: "omit" — runtime-aware (#2297). Honor "omit" when the
    // PROJECT explicitly set it (user intent — project config wins, #2517 finding
    // #4) OR when the active runtime genuinely lacks native model aliases. Only a
    // native-alias runtime (Claude) ignores an "omit" that came solely from the
    // SHARED ~/.gsd/defaults.json — the #2297 poisoning fix — and falls through to
    // its tier aliases below. Active runtime: GSD_RUNTIME → config.runtime → the
    // per-install .gsd-runtime marker → 'claude' (canonicalized).
    // NOTE: a non-Claude runtime that HAS a populated runtime-tier map already
    // returned its own model id at step 3 above, before this gate — for those the
    // explicit-project-omit honoring here is moot (step 3 wins, by #2517 design).
    if (config['resolve_model_ids'] === 'omit'
        && (projectExplicitlySetsOmit(cwd) || !RUNTIMES_WITH_NATIVE_ALIASES.has(resolveActiveRuntime(config)))) {
        return '';
    }
    // 5. Profile lookup (Claude-native default).
    if (!agentModels) {
        return profile === 'quality' ? 'opus'
            : profile === 'budget' ? 'haiku'
                : profile === 'inherit' ? 'inherit'
                    : 'sonnet';
    }
    if (tier === 'inherit')
        return 'inherit';
    const alias = tier;
    // Only the explicit `true` opt-in materializes full model IDs (#1569). Guard
    // against the loose-truthy check catching a "omit" that a native-alias runtime
    // ignored above (#2297): "omit" must fall through to the tier ALIAS here, not
    // be materialized into a full ID Claude's Agent tool cannot spawn.
    if (config['resolve_model_ids'] === true) {
        return model_catalog_cjs_1.MODEL_ALIAS_MAP[alias] || alias;
    }
    return alias;
}
const VALID_GRANULARITIES = new Set(['coarse', 'standard', 'fine']);
/**
 * Resolve the planning granularity for a phase type (#68).
 */
function resolveGranularityInternal(cwd, phaseType, override) {
    if (override !== undefined && override !== null && override !== '') {
        if (VALID_GRANULARITIES.has(override)) {
            return override;
        }
    }
    const config = loadConfig(cwd);
    const configGranularities = config['granularities'];
    const perPhase = (phaseType && configGranularities && typeof configGranularities === 'object')
        ? configGranularities[phaseType]
        : undefined;
    if (perPhase && VALID_GRANULARITIES.has(perPhase)) {
        return perPhase;
    }
    if (config['granularity'] !== undefined && config['granularity'] !== null && config['granularity'] !== '') {
        return config['granularity'];
    }
    const planning = config['planning'];
    const planningGran = planning && planning['granularity'];
    if (planningGran !== undefined && planningGran !== null && planningGran !== '') {
        return planningGran;
    }
    return 'standard';
}
/**
 * Validate a CLI granularity override at the command boundary. Empty/null/undefined
 * are treated as "no override" (no-op). An invalid non-empty value calls `fail`.
 */
function assertValidGranularityOverride(override, fail) {
    if (override !== undefined && override !== null && override !== '' && !VALID_GRANULARITIES.has(override)) {
        fail(`invalid granularity '${override}' (valid: ${[...VALID_GRANULARITIES].join(', ')})`);
    }
}
/**
 * #3024 — Resolve a model for a specific dynamic-routing attempt.
 */
function resolveModelForTier(cwd, agentType, attempt) {
    const config = loadConfig(cwd);
    const attemptN = Number.isInteger(attempt) && attempt > 0 ? attempt : 0;
    const modelOverrides = config['model_overrides'];
    const override = modelOverrides?.[agentType];
    if (override) {
        const mapped = mapClaudeOverrideForRuntime(override, config['runtime'], agentType);
        if (mapped !== null)
            return mapped;
        // Unmappable Claude ID — fall through to dynamic_routing / model_policy resolution.
    }
    if (config['model_policy'] && config['runtime'] && config['runtime'] !== 'claude') {
        return resolveModelInternal(cwd, agentType);
    }
    const dr = config['dynamic_routing'];
    if (!dr || typeof dr !== 'object' || dr['enabled'] !== true) {
        return resolveModelInternal(cwd, agentType);
    }
    const tierModels = dr['tier_models'];
    if (!tierModels || typeof tierModels !== 'object') {
        return resolveModelInternal(cwd, agentType);
    }
    const defaultTier = (AGENT_DEFAULT_TIERS)[agentType];
    if (!defaultTier || !(VALID_AGENT_TIERS).has(defaultTier)) {
        return resolveModelInternal(cwd, agentType);
    }
    const maxEscalations = Number.isInteger(dr['max_escalations']) && dr['max_escalations'] >= 0
        ? dr['max_escalations']
        : 1;
    const escalationEnabled = dr['escalate_on_failure'] !== false;
    const effectiveAttempt = escalationEnabled
        ? Math.min(attemptN, maxEscalations)
        : 0;
    let tier = defaultTier;
    for (let i = 0; i < effectiveAttempt; i += 1) {
        const next = (nextTier)(tier);
        if (!next || next === tier)
            break;
        tier = next;
    }
    const alias = tierModels[tier];
    if (typeof alias !== 'string' || alias.length === 0) {
        return resolveModelInternal(cwd, agentType);
    }
    return alias;
}
/**
 * Keep only usable model ids: non-empty strings. A malformed config can put
 * anything in here (nulls, numbers, blank strings), and a blank model id would
 * resolve to an unusable agent invocation rather than failing visibly. Invalid
 * entries are dropped and the surviving order is preserved, so the ladder stays
 * predictable (ADR 227 — validate shape, not just type).
 */
function sanitizeProviderEscalation(raw) {
    if (!Array.isArray(raw))
        return [];
    return raw.filter((entry) => typeof entry === 'string' && entry.trim().length > 0);
}
/**
 * #2296 — Resolve the model for one attempt of the PROVIDER escalation ladder.
 *
 * The tier ladder (`resolveModelForTier`) escalates within one provider's
 * `tier_models`, which does not help when that provider is the thing that is
 * throttled. This walks `dynamic_routing.provider_escalation` instead: an
 * ordered list of alternative model ids, capped by
 * `min(max_escalations, list length)`.
 *
 * `applicable` is the caller's policy decision (only a quota-exceeded
 * classification should consult this ladder). It is a parameter rather than a
 * class check here so this module keeps depending only on leaf modules, per
 * CONTEXT.md's Model Resolution module contract.
 *
 * Attempt 0 — and every non-applicable call — stays on the source model.
 * `exhausted` reports that the ladder is spent so the caller can fail loudly
 * naming every model it tried.
 */
function resolveProviderEscalation(cwd, agentType, attempt, applicable) {
    // The model that would be used with no provider escalation at all.
    const from = resolveModelForTier(cwd, agentType, 0);
    const stay = (exhausted = false) => ({
        from,
        to: from,
        escalated: false,
        exhausted,
        attempted: [from],
        index: 0,
    });
    if (!applicable)
        return stay();
    const config = loadConfig(cwd);
    const dr = config['dynamic_routing'];
    if (!dr || typeof dr !== 'object' || dr['enabled'] !== true)
        return stay();
    if (dr['escalate_on_failure'] === false)
        return stay();
    const list = sanitizeProviderEscalation(dr['provider_escalation']);
    if (list.length === 0)
        return stay();
    // Same default and same validity rule as the tier ladder above — a negative or
    // non-integer max_escalations is invalid config, not a request for zero.
    const maxEscalations = Number.isInteger(dr['max_escalations']) && dr['max_escalations'] >= 0
        ? dr['max_escalations']
        : 1;
    const cap = Math.min(maxEscalations, list.length);
    // An explicit cap of 0 means the ladder exists but is spent before it starts.
    if (cap === 0)
        return stay(true);
    const attemptN = Number.isInteger(attempt) && attempt > 0 ? attempt : 0;
    if (attemptN === 0)
        return stay();
    const index = Math.min(attemptN, cap);
    return {
        from,
        to: list[index - 1],
        escalated: true,
        exhausted: attemptN > cap,
        attempted: [from, ...list.slice(0, index)],
        index,
    };
}
// ─── #443 — Unified effort + fast_mode resolvers ─────────────────────────────
const VALID_EFFORTS = ['minimal', 'low', 'medium', 'high', 'xhigh', 'max'];
const EFFORT_SET = new Set(VALID_EFFORTS);
/**
 * Walk one step up the effort ladder from `e`.
 */
function nextEffort(e) {
    const i = VALID_EFFORTS.indexOf(e);
    if (i < 0)
        return null;
    return VALID_EFFORTS[Math.min(i + 1, VALID_EFFORTS.length - 1)];
}
/**
 * #443 — Resolve a universal effort string for (cwd, agentType).
 */
function resolveEffortInternal(cwd, agentType, opts) {
    // Step 1: invocation override
    if (opts && typeof opts.override === 'string' && EFFORT_SET.has(opts.override)) {
        return opts.override;
    }
    const config = loadConfig(cwd);
    const effortCfg = (config['effort'] && typeof config['effort'] === 'object' && !Array.isArray(config['effort']))
        ? config['effort']
        : null;
    // Step 2: agent_overrides
    if (effortCfg) {
        const ao = effortCfg['agent_overrides'];
        if (ao && typeof ao === 'object' && !Array.isArray(ao)) {
            const v = ao[agentType];
            if (typeof v === 'string' && EFFORT_SET.has(v))
                return v;
        }
    }
    else {
        const canonicalEffort = (configuration_cjs_1.CONFIG_DEFAULTS)['effort'];
        const mao = canonicalEffort && typeof canonicalEffort === 'object'
            ? canonicalEffort['agent_overrides']
            : undefined;
        if (mao && typeof mao === 'object' && !Array.isArray(mao)) {
            const v = mao[agentType];
            if (typeof v === 'string' && EFFORT_SET.has(v))
                return v;
        }
    }
    // Step 3: routing_tier_defaults by agent's default tier.
    const agentTier = (AGENT_DEFAULT_TIERS)[agentType];
    if (agentTier) {
        if (effortCfg && effortCfg['routing_tier_defaults'] &&
            typeof effortCfg['routing_tier_defaults'] === 'object' &&
            !Array.isArray(effortCfg['routing_tier_defaults'])) {
            const v = effortCfg['routing_tier_defaults'][agentTier];
            if (typeof v === 'string' && EFFORT_SET.has(v))
                return v;
        }
        else if (!effortCfg) {
            const canonicalEffort = (configuration_cjs_1.CONFIG_DEFAULTS)['effort'];
            const manifestDefaults = canonicalEffort && typeof canonicalEffort === 'object'
                ? canonicalEffort['routing_tier_defaults']
                : undefined;
            if (manifestDefaults && typeof manifestDefaults === 'object') {
                const v = manifestDefaults[agentTier];
                if (typeof v === 'string' && EFFORT_SET.has(v))
                    return v;
            }
        }
    }
    // Step 4: effort.default
    if (effortCfg) {
        const d = effortCfg['default'];
        if (typeof d === 'string' && EFFORT_SET.has(d))
            return d;
    }
    else {
        const canonicalEffort = (configuration_cjs_1.CONFIG_DEFAULTS)['effort'];
        const d = canonicalEffort && typeof canonicalEffort === 'object'
            ? canonicalEffort['default']
            : undefined;
        if (typeof d === 'string' && EFFORT_SET.has(d))
            return d;
    }
    // Step 5: hardcoded default
    return 'high';
}
/**
 * #443 — Resolve fast_mode boolean for (cwd, agentType).
 */
function resolveFastModeInternal(cwd, agentType, opts) {
    // Step 1: invocation override
    if (opts && typeof opts.override === 'boolean') {
        return opts.override;
    }
    const config = loadConfig(cwd);
    const fmCfg = (config['fast_mode'] && typeof config['fast_mode'] === 'object' && !Array.isArray(config['fast_mode']))
        ? config['fast_mode']
        : null;
    // Step 2: agent_overrides
    if (fmCfg) {
        const ao = fmCfg['agent_overrides'];
        if (ao && typeof ao === 'object' && !Array.isArray(ao)) {
            const v = ao[agentType];
            if (typeof v === 'boolean')
                return v;
        }
    }
    // Step 3: routing_tier_defaults by agent's default tier.
    const agentTier = (AGENT_DEFAULT_TIERS)[agentType];
    if (agentTier) {
        if (fmCfg && fmCfg['routing_tier_defaults'] &&
            typeof fmCfg['routing_tier_defaults'] === 'object' &&
            !Array.isArray(fmCfg['routing_tier_defaults'])) {
            const v = fmCfg['routing_tier_defaults'][agentTier];
            if (typeof v === 'boolean')
                return v;
        }
        else if (!fmCfg) {
            const canonicalFm = (configuration_cjs_1.CONFIG_DEFAULTS)['fast_mode'];
            const manifestDefaults = canonicalFm && typeof canonicalFm === 'object'
                ? canonicalFm['routing_tier_defaults']
                : undefined;
            if (manifestDefaults && typeof manifestDefaults === 'object') {
                const v = manifestDefaults[agentTier];
                if (typeof v === 'boolean')
                    return v;
            }
        }
    }
    // Step 4: fast_mode.enabled
    if (fmCfg && typeof fmCfg['enabled'] === 'boolean') {
        return fmCfg['enabled'];
    }
    // Step 5: hardcoded default
    return false;
}
/**
 * #443 — Resolve effort for a dynamic-routing attempt (with escalation).
 */
function resolveEffortForTier(cwd, agentType, attempt) {
    const base = resolveEffortInternal(cwd, agentType);
    const config = loadConfig(cwd);
    const dr = config['dynamic_routing'];
    if (!dr || typeof dr !== 'object' || dr['enabled'] !== true) {
        return base;
    }
    if (dr['escalate_on_failure'] === false) {
        return base;
    }
    const maxEscalations = Number.isInteger(dr['max_escalations']) && dr['max_escalations'] >= 0
        ? dr['max_escalations']
        : 1;
    const attemptN = Number.isInteger(attempt) && attempt > 0 ? attempt : 0;
    const effectiveAttempt = Math.min(attemptN, maxEscalations);
    let current = base;
    for (let i = 0; i < effectiveAttempt; i++) {
        const next = nextEffort(current);
        if (!next || next === current)
            break;
        current = next;
    }
    return current;
}
module.exports = {
    resolveTierEntry,
    CLAUDE_AGENT_ALIASES,
    resolveModelPolicy,
    resolveModelInternal,
    _resetModelPolicyWarningCacheForTests,
    _resetModelOverrideWarningCacheForTests,
    _setInstallRuntimeMarkerForTests,
    _resetInstallRuntimeMarkerCacheForTests,
    VALID_GRANULARITIES,
    resolveGranularityInternal,
    assertValidGranularityOverride,
    resolveModelForTier,
    resolveProviderEscalation,
    VALID_EFFORTS,
    EFFORT_SET,
    nextEffort,
    resolveEffortInternal,
    resolveFastModeInternal,
    resolveEffortForTier,
};
