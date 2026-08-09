"use strict";
/**
 * Workstream Inventory Builder — pure projection from pre-collected
 * filesystem data to typed WorkstreamInventory. No I/O. No async.
 *
 * ADR-457 build-at-publish: the hand-written
 * bin/lib/workstream-inventory-builder.cjs collapsed to a TypeScript source
 * of truth. Behaviour is preserved byte-for-behaviour from the prior
 * hand-written .cjs; only types are added.
 */
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.pickRollupWinners = pickRollupWinners;
exports.isCompletedInventory = isCompletedInventory;
exports.buildWorkstreamInventory = buildWorkstreamInventory;
const node_path_1 = __importDefault(require("node:path"));
// Internal helpers
function toPosixPath(p) {
    return p.split('\\').join('/');
}
/**
 * #2562: verification verdicts that DISQUALIFY a phase from `complete`, even
 * when its SUMMARY count meets its PLAN count. Deliberately scoped to the two
 * EXPLICIT failing verdicts the verifier emits — `missing`/`unknown` (verifier
 * off / not yet run) and `stale` (mtime-derived, #2348) are intentionally left
 * untouched so verifier-disabled projects do not regress to never-complete.
 *
 * #2645: `'unrecorded'` is NOT a verdict the verifier itself ever emits — it
 * is an internal sentinel `workstream-inventory.cts`'s verification-deletion
 * ledger substitutes for `'missing'` once that workstream has ADOPTED the
 * ledger (a `.verification-ledger.json` file exists for it) but has no
 * remembered entry for this specific phase. Pre-adoption (no ledger file at
 * all) still resolves to plain `'missing'`, which stays OUTSIDE this set —
 * that is what keeps a project untouched by this fix until it actually uses
 * the verifier at least once. Post-adoption, an unrecorded phase fails
 * CLOSED (counted here) rather than open, so a corrupt or evidence-absent
 * ledger entry can no longer be read as "safe to complete" the way a bare
 * `'missing'` sentinel is.
 */
const FAILING_VERIFICATION_STATUSES = new Set(['gaps_found', 'human_needed', 'unrecorded']);
/**
 * #2562 / Bug #2445 / #2645 review: pick ONE winning item per key from a
 * PRE-SORTED list — newest `mtimeMs` wins; on an exact tie the incumbent
 * (first-in-sort-order) wins, since only a STRICTLY greater mtime replaces
 * it. `includeItem` lets a caller exclude items before comparison (e.g.
 * out-of-milestone directories) — critically, the filter runs BEFORE the
 * mtime comparison, so an excluded item can never win a tie or a comparison
 * against an included one.
 *
 * Extracted as the SINGLE shared implementation after a #2645 review found
 * two independently-written copies of this exact rule had silently
 * diverged: `workstream-inventory.cts`'s ledger-winner selection compared
 * raw mtimes with no scoping filter, while this module's own `rollupDirByKey`
 * (below) filtered out-of-milestone directories first. A stale out-of-
 * milestone directory with a newer mtime than the live in-milestone one
 * (plausible after a checkout/rebase resets mtimes) could then win the
 * LEDGER's selection while losing the BUILDER's — reopening #2645's own
 * hole for the phase that actually counts toward `completed_phases`,
 * reachable with a plain `rm` and no ledger tampering. A comment asserting
 * two hand-written copies "use the same rule" is not a guarantee they do;
 * one shared function is.
 */
function pickRollupWinners(sortedItems, keyOf, mtimeOf, includeItem = () => true) {
    const winners = new Map();
    for (const item of sortedItems) {
        if (!includeItem(item))
            continue;
        const key = keyOf(item);
        const incumbent = winners.get(key);
        if (incumbent === undefined || mtimeOf(item) > mtimeOf(incumbent)) {
            winners.set(key, item);
        }
    }
    return winners;
}
function isCompletedInventory(status) {
    const s = (typeof status === 'string'
        ? status
        : typeof status === 'number' || typeof status === 'boolean'
            ? String(status)
            : '').trim().toLowerCase();
    return /\bmilestone\s+complete\b/.test(s) || /\barchived\b/.test(s);
}
function buildWorkstreamInventory(inputs) {
    const { name, projectDir, workstreamDir, phaseDirNames, activeWorkstreamName, phaseFilesCounts, roadmapPhaseCount, stateProjection, filesExist, milestoneShipped, milestoneShippedSignal, currentMilestonePhaseCount = 0, milestoneScoped, } = inputs;
    // A caller that passes only the legacy boolean states THAT a signal fired but
    // not which one. Treat it as `legacy` — the ungated strength — so pre-review
    // callers keep their exact behavior rather than silently acquiring a new gate.
    const shippedSignal = milestoneShippedSignal !== undefined ? milestoneShippedSignal : (milestoneShipped ? 'legacy' : null);
    const milestoneShippedResolved = shippedSignal !== null;
    // #2562: when scoping is active, prior-milestone phase directories are
    // excluded from the completion rollup and the denominator. The caller states
    // this; the count-derived default is the pre-#2562 fallback for callers that
    // do not, and cannot represent a scoped-but-empty current milestone.
    const scoped = milestoneScoped ?? currentMilestonePhaseCount > 0;
    // Index counts by directory for O(1) lookup during sort/iteration
    const countsMap = new Map();
    for (const entry of phaseFilesCounts) {
        countsMap.set(entry.directory, entry);
    }
    // #2562 / Bug #2445: pick ONE directory per phase key for the rollup. Stale
    // same-numbered directories left over from a prior milestone would otherwise
    // each add to the numerator while the denominator counts distinct phases —
    // pushing completed_phases past it, where the old `Math.min` cap silently
    // rounded the result up to 100% and hid an unstarted phase. Newest-on-disk
    // wins, mirroring state.cts's #2445 de-duplication. `pickRollupWinners` is
    // the SHARED implementation `workstream-inventory.cts`'s ledger-winner
    // selection also calls, so the two can never independently diverge again
    // (#2645 review).
    const rollupDirByKey = pickRollupWinners([...phaseDirNames].sort(), (dir) => countsMap.get(dir)?.phaseKey ?? dir, (dir) => countsMap.get(dir)?.mtimeMs ?? 0, (dir) => !(scoped && countsMap.get(dir)?.inMilestone === false));
    const rollupDirs = new Set(rollupDirByKey.values());
    const phases = [];
    let completedPhases = 0;
    let totalPlans = 0;
    let completedPlans = 0;
    // #2562 review: in-milestone phase directories still present under `phases/`,
    // whatever their status. A CLEAN archive has none — `milestone complete` moves
    // them all out — so this counts exactly the phases that outlived the archive,
    // which is what distinguishes "archived" from "archived, then reopened".
    // Deliberately NOT "…and unfinished": a complete live dir beside a declared
    // but never-scaffolded phase is a dirty archive too, and the dirless phase has
    // no directory to inspect.
    let liveInMilestonePhases = 0;
    for (const dir of [...phaseDirNames].sort()) {
        const counts = countsMap.get(dir);
        const planCount = counts?.planCount ?? 0;
        const summaryCount = counts?.summaryCount ?? 0;
        // #2562: SUMMARY≥PLAN parity is necessary but not sufficient — a phase whose
        // verification verdict is an explicit failing one is still in progress.
        const verificationStatus = counts?.verificationStatus ?? 'missing';
        const summariesMeetPlans = summaryCount >= planCount && planCount > 0;
        const status = summariesMeetPlans && !FAILING_VERIFICATION_STATUSES.has(verificationStatus)
            ? 'complete'
            : planCount > 0
                ? 'in_progress'
                : 'pending';
        // #2562: only current-milestone phases feed the rollup when scoping is on,
        // and only one directory per phase key (see rollupDirs above).
        const countsTowardMilestone = (!scoped || counts?.inMilestone !== false) && rollupDirs.has(dir);
        if (countsTowardMilestone) {
            totalPlans += planCount;
            completedPlans += Math.min(summaryCount, planCount);
            if (status === 'complete')
                completedPhases++;
            liveInMilestonePhases++;
        }
        phases.push({
            directory: dir,
            status,
            plan_count: planCount,
            summary_count: summaryCount,
        });
    }
    // #2562: the denominator is the current milestone's declared phase count when
    // scoping is active (catches phases declared but never scaffolded), else the
    // legacy whole-roadmap heading count.
    const effectivePhaseCount = scoped ? currentMilestonePhaseCount : roadmapPhaseCount;
    // #2562 invariant: the numerator counts de-duplicated in-milestone phase keys
    // and the denominator counts the union of those keys with the roadmap's own
    // declarations, so the numerator can never exceed it. Raising the numerator
    // above the denominator means the two sides were derived in different key
    // spaces — the defect class this issue is about. The old `Math.min(100, …)`
    // capped that away and reported 100%; this makes it fail loudly instead.
    if (scoped && completedPhases > effectivePhaseCount) {
        throw new Error(`workstream inventory invariant violated for "${name}": completed_phases (${completedPhases}) ` +
            `exceeds the current-milestone denominator (${effectivePhaseCount}). The completion numerator and ` +
            `denominator were derived in different phase-key spaces.`);
    }
    // #1913: derive status from authoritative shipped signals rather than trusting
    // the mutable STATE.md `Status` field. When a shipped signal is present, the
    // workstream is "milestone complete" regardless of a stale field value.
    //
    // #2562 review: a shipped signal is a CLAIM, and a claim its own milestone's
    // artifacts contradict must not be echoed as fact — the defect class this issue
    // is about reaches `status`, not just `progress_percent`. The two signals need
    // DIFFERENT cross-checks; one check for both regresses the commonest shape:
    //
    //  - `heading` — operator-typed marker in the LIVE roadmap. Nothing has been
    //    archived, so every phase the milestone declares should be on disk and
    //    complete. Gate on the full ratio, which also catches the
    //    declared-but-never-scaffolded phases that have no directory to inspect.
    //  - `snapshot` — `milestones/<version>-ROADMAP.md`. The `milestone complete`
    //    run that writes it also MOVES the milestone's phase directories into
    //    `milestones/<version>-phases/` (milestone.cts:783-790) while COPYING —
    //    never truncating — the live ROADMAP (:700-702), so its Progress rows
    //    survive. A CLEAN archive therefore reads 0/N by construction, and gating
    //    it on the ratio alone would strip `milestone complete` from every
    //    archived milestone. But a live in-milestone directory means the archive
    //    is NOT clean — a phase was added or reopened after it, reachable because
    //    `milestone complete` does not advance STATE's `milestone:` field
    //    (state-transition.cts:83, :1335); only `/gsd-new-milestone` does (:1224).
    //    Once any in-milestone directory is live the ratio IS meaningful again, so
    //    the check is the conjunction. Requiring the live directory to itself be
    //    unfinished was too narrow: it let a complete live dir alongside a
    //    declared-but-unscaffolded phase reproduce the reported symptom, since a
    //    dirless phase has nothing to inspect.
    //
    // The whole cross-check is scoped-only, and NOT because of the signal: when
    // scoping is off, `effectivePhaseCount` is the whole-roadmap count and
    // membership is everything, so there is no current-milestone artifact set to
    // check a current-milestone claim against. `legacy` is additionally ungated by
    // signal — it is the fallback for an unknown milestone version, which is
    // exactly when scoping cannot engage either.
    const fieldStatus = stateProjection.status;
    const shippedContradicted = scoped && (shippedSignal === 'heading'
        ? completedPhases < effectivePhaseCount
        : shippedSignal === 'snapshot'
            ? liveInMilestonePhases > 0 && completedPhases < effectivePhaseCount
            : false);
    const useDerived = milestoneShippedResolved && !shippedContradicted;
    // Refusing the claim does not make the STATE field a safe fallback: it is
    // operator-written and in this window it commonly ALSO reads "milestone
    // complete", which would re-report the refused claim through the other door.
    // Against contradicting artifacts, NEITHER source may assert completion.
    const artifactOverride = shippedContradicted && isCompletedInventory(fieldStatus);
    const status = useDerived
        ? 'milestone complete'
        : artifactOverride
            ? 'in_progress'
            : fieldStatus;
    const status_source = useDerived || artifactOverride ? 'derived' : 'field';
    const status_conflict = (useDerived && !isCompletedInventory(fieldStatus)) || artifactOverride;
    return {
        name,
        path: toPosixPath(node_path_1.default.relative(projectDir, workstreamDir)),
        active: name === activeWorkstreamName,
        files: {
            roadmap: filesExist.roadmap,
            state: filesExist.state,
            requirements: filesExist.requirements,
        },
        status,
        status_source,
        status_conflict,
        milestone_shipped_unverified: shippedContradicted,
        current_phase: stateProjection.current_phase,
        last_activity: stateProjection.last_activity,
        phases,
        phase_count: phases.length,
        completed_phases: completedPhases,
        roadmap_phase_count: effectivePhaseCount,
        total_plans: totalPlans,
        completed_plans: completedPlans,
        // The `Math.min` cap is unreachable under milestone scoping (the invariant
        // above throws first) and survives only for the legacy unscoped path, where
        // the denominator is a roadmap heading count that a caller cannot guarantee
        // bounds the numerator.
        progress_percent: effectivePhaseCount > 0
            ? Math.min(100, Math.round((completedPhases / effectivePhaseCount) * 100))
            : 0,
    };
}
