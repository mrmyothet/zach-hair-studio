"use strict";
/**
 * Phase Locator — Phase-directory search and location
 *
 * ADR-857 rollout phase 2d: extracted from core.cts (issue #881).
 * Owns active-phase discovery against the `.planning/phases/` tree
 * (`searchPhaseInDir`, `findPhaseInternal`) and archived-phase-dir
 * enumeration (`getArchivedPhaseDirs`), matching phase ids/tokens against
 * the filesystem. Behaviour is preserved byte-for-behaviour from the prior
 * location; only the module boundary moved. The core.cjs re-export spine
 * was retired in epic #1267; callers import phase-locator helpers directly.
 *
 * Dependencies (leaf modules only — no loadConfig):
 *   - node:fs / node:path (stdlib)
 *   - ./phase-id.cjs       (normalizePhaseName, phaseTokenMatches, extractPhaseToken)
 *   - ./core-utils.cjs     (readSubdirectories, getPhaseFileStats, extractCanonicalPlanId, toPosixPath)
 *   - ./planning-workspace.cjs (planningDir)
 */
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
const node_fs_1 = __importDefault(require("node:fs"));
const node_path_1 = __importDefault(require("node:path"));
// eslint-disable-next-line @typescript-eslint/no-require-imports
const phaseIdModule = require("./phase-id.cjs");
const { normalizePhaseName, phaseTokenMatches, extractPhaseToken } = phaseIdModule;
// eslint-disable-next-line @typescript-eslint/no-require-imports
const coreUtilsModule = require("./core-utils.cjs");
const { readSubdirectories, getPhaseFileStats, extractCanonicalPlanId, toPosixPath } = coreUtilsModule;
// eslint-disable-next-line @typescript-eslint/no-require-imports
const planningWorkspace = require("./planning-workspace.cjs");
const { planningDir } = planningWorkspace;
// eslint-disable-next-line @typescript-eslint/no-require-imports
const frontmatterModule = require("./frontmatter.cjs");
const { extractFrontmatter } = frontmatterModule;
// eslint-disable-next-line @typescript-eslint/no-require-imports
const planDependencyGraphModule = require("./plan-dependency-graph.cjs");
const { computeHaltPropagation, buildSummaryFileIndex, isSummaryFileHalted } = planDependencyGraphModule;
/**
 * #2830: parse a plan file's `depends_on` frontmatter. Returns [] — never
 * throws — on a missing/unreadable/malformed plan or absent field, matching
 * this primitive's existing fail-safe posture (a plan directory this
 * primitive can otherwise read must never throw here).
 */
function parsePlanDependsOn(phaseDir, planFile) {
    try {
        const planPath = node_path_1.default.join(phaseDir, planFile);
        const content = node_fs_1.default.readFileSync(planPath, 'utf-8');
        const fm = extractFrontmatter(content, planPath);
        const fmDeps = fm['depends_on'];
        if (Array.isArray(fmDeps))
            return fmDeps.map(String);
        if (typeof fmDeps === 'string' && fmDeps.trim() !== '')
            return [fmDeps];
        return [];
    }
    catch {
        return [];
    }
}
// ─── Phase search helpers ─────────────────────────────────────────────────────
/**
 * #2855: single source of truth for resolving and enumerating a project's
 * (or, when a workstream is active, that workstream's OWN) archived-milestone
 * directories — `<planningDir(cwd)>/milestones/vX.Y-phases/`. Both
 * `findPhaseInternal`'s archive fallback and `getArchivedPhaseDirs` used to
 * carry independent copies of this resolve-then-enumerate logic, which is
 * exactly the shape that let the original #2855 bug (hardcoded root path)
 * exist in one copy and not the other. Sharing this seam means a future
 * change to how the archive tree is located only needs to happen once.
 * Most-recent-milestone-first order (reverse-sorted directory names).
 * Never throws: an absent/unreadable milestones/ dir yields [].
 */
function listArchiveVersionDirs(cwd) {
    const milestonesDir = node_path_1.default.join(planningDir(cwd), 'milestones');
    if (!node_fs_1.default.existsSync(milestonesDir))
        return [];
    try {
        const milestoneEntries = node_fs_1.default.readdirSync(milestonesDir, { withFileTypes: true });
        return milestoneEntries
            .filter(e => e.isDirectory() && /^v[\d.]+-phases$/.test(e.name))
            .map(e => e.name)
            .sort()
            .reverse()
            .map(archiveName => ({
            version: archiveName.match(/^(v[\d.]+)-phases$/)[1],
            archivePath: node_path_1.default.join(milestonesDir, archiveName),
        }));
    }
    catch {
        return [];
    }
}
function searchPhaseInDir(baseDir, relBase, normalized) {
    try {
        const dirs = readSubdirectories(baseDir, true);
        const matches = dirs.filter(d => phaseTokenMatches(d, normalized));
        if (matches.length === 0)
            return null;
        // #2237: fail loud when multiple directories match the same bare phase
        // number — this happens when unrelated projects share a .planning/phases/
        // tree. Silently taking the first match risks cross-project file writes.
        if (matches.length > 1) {
            return {
                found: false,
                directory: '',
                phase_number: normalized,
                phase_name: null,
                phase_slug: null,
                plans: [],
                summaries: [],
                incomplete_plans: [],
                has_research: false,
                has_context: false,
                has_verification: false,
                has_reviews: false,
                ambiguous_matches: matches,
                halted_plans: [],
                blocked_by: {},
                runnable_plans: [],
            };
        }
        const match = matches[0];
        const phaseToken = extractPhaseToken(match);
        const phaseNumber = phaseToken || normalized;
        const afterToken = match.slice(phaseToken ? phaseToken.length : 0).replace(/^-/, '');
        const phaseName = afterToken || null;
        const phaseDir = node_path_1.default.join(baseDir, match);
        const { plans: unsortedPlans, summaries: unsortedSummaries, hasResearch, hasContext, hasVerification, hasReviews } = getPhaseFileStats(phaseDir);
        const plans = unsortedPlans.sort();
        const summaries = unsortedSummaries.sort();
        const completedPlanIds = new Set(summaries.flatMap(s => {
            const exact = s.replace('-SUMMARY.md', '').replace('SUMMARY.md', '');
            const canonical = extractCanonicalPlanId(s);
            return canonical === exact ? [exact] : [exact, canonical];
        }));
        const incompletePlans = plans.filter(p => {
            const planId = p.replace('-PLAN.md', '').replace('PLAN.md', '');
            const canonical = extractCanonicalPlanId(p);
            return !completedPlanIds.has(planId) && !completedPlanIds.has(canonical);
        });
        // #2830: reverse lookup from a completed plan's id (exact or canonical) to
        // its actual summary filename. Shared builder (also used by phase.cts's
        // cmdPhasePlanIndex) so the two can never disagree about which summary
        // belongs to which plan.
        const summaryFileByPlanId = buildSummaryFileIndex(summaries, extractCanonicalPlanId);
        // #2830: this primitive previously never parsed depends_on at all — see
        // src/plan-dependency-graph.cts's file header. Build the same
        // PlanHaltNode[] shape phase.cts's cmdPhasePlanIndex builds (id resolution
        // mirrors its planMap/canonicalToId pattern) and hand it to the ONE
        // shared halt-propagation traversal so this reader and the wave-grouping
        // reader can never diverge on the halt rule again.
        const planIds = plans.map(p => p.replace('-PLAN.md', '').replace('PLAN.md', ''));
        const planIdByLower = new Map(planIds.map(id => [id.toLowerCase(), id]));
        const canonicalToPlanId = new Map(plans.map((p, i) => [extractCanonicalPlanId(p).toLowerCase(), planIds[i]]));
        const haltNodes = plans.map((p, i) => {
            const planId = planIds[i];
            const canonical = extractCanonicalPlanId(p);
            const summaryFile = summaryFileByPlanId.get(planId) ?? summaryFileByPlanId.get(canonical);
            const halted = summaryFile !== undefined && isSummaryFileHalted(node_path_1.default.join(phaseDir, summaryFile));
            const resolvedDependsOn = parsePlanDependsOn(phaseDir, p)
                .map((dep) => {
                const lower = dep.toLowerCase();
                return planIdByLower.get(lower) ?? canonicalToPlanId.get(lower) ?? null;
            })
                .filter((id) => id !== null);
            return { id: planId, resolvedDependsOn, halted };
        });
        const { blockedBy } = computeHaltPropagation(haltNodes);
        const haltedPlans = plans.filter((_, i) => haltNodes[i].halted);
        const incompletePlanSet = new Set(incompletePlans);
        const blockedByFiles = {};
        const runnablePlans = [];
        for (let i = 0; i < plans.length; i++) {
            const p = plans[i];
            if (!incompletePlanSet.has(p))
                continue;
            const causes = blockedBy.get(planIds[i]) ?? [];
            if (causes.length > 0) {
                blockedByFiles[p] = causes;
            }
            else {
                runnablePlans.push(p);
            }
        }
        return {
            found: true,
            directory: toPosixPath(node_path_1.default.join(relBase, match)),
            phase_number: phaseNumber,
            phase_name: phaseName,
            phase_slug: phaseName ? phaseName.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '') : null,
            plans,
            summaries,
            incomplete_plans: incompletePlans,
            has_research: hasResearch,
            has_context: hasContext,
            has_verification: hasVerification,
            has_reviews: hasReviews,
            halted_plans: haltedPlans,
            blocked_by: blockedByFiles,
            runnable_plans: runnablePlans,
        };
    }
    catch {
        return null;
    }
}
function findPhaseInternal(cwd, phase) {
    if (!phase)
        return null;
    const phasesDir = node_path_1.default.join(planningDir(cwd), 'phases');
    const normalized = normalizePhaseName(phase);
    const relPhasesDir = toPosixPath(node_path_1.default.relative(cwd, phasesDir));
    const current = searchPhaseInDir(phasesDir, relPhasesDir, normalized);
    if (current)
        return current;
    // #2855: scope the archived-milestone fallback to the SAME workstream as the
    // active-phase search above (planningDir(cwd) resolves GSD_WORKSTREAM/GSD_PROJECT
    // the identical way both places), not the hardcoded project-root tree. Archived
    // phases genuinely live under a workstream's own `.planning/workstreams/<ws>/
    // milestones/` — that is where archivePhaseDirectories (milestone.cts) writes
    // them via the same planningDir(cwd) resolution. Hardcoding root here let a
    // pending workstream phase resolve to an unrelated workstream's (or a flat-mode
    // project's) archived phase that merely shares a phase number. Shared with
    // getArchivedPhaseDirs via listArchiveVersionDirs (see its doc comment).
    for (const { version, archivePath } of listArchiveVersionDirs(cwd)) {
        const relBase = toPosixPath(node_path_1.default.relative(cwd, archivePath));
        const result = searchPhaseInDir(archivePath, relBase, normalized);
        if (result) {
            result.archived = version;
            return result;
        }
    }
    return null;
}
function getArchivedPhaseDirs(cwd) {
    // #2855: same workstream-scoped resolution as findPhaseInternal above, via
    // the shared listArchiveVersionDirs helper. `phase.list --include-archived`
    // (the primary non-init consumer) must not leak a different workstream's
    // archive either.
    const results = [];
    for (const { version, archivePath } of listArchiveVersionDirs(cwd)) {
        const dirs = readSubdirectories(archivePath, true);
        for (const dir of dirs) {
            results.push({
                name: dir,
                milestone: version,
                basePath: toPosixPath(node_path_1.default.relative(cwd, archivePath)),
                fullPath: node_path_1.default.join(archivePath, dir),
            });
        }
    }
    return results;
}
module.exports = {
    searchPhaseInDir,
    findPhaseInternal,
    getArchivedPhaseDirs,
};
