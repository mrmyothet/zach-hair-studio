---
marp: true
paginate: true
transition: fade
# PechaKucha: 6 slides, 20s auto-advance. Do not change the count.
auto-advance: 20
---

<!-- slide 1 -->
# Zach — a hair studio owner
- Runs a salon offering styling, coloring, and products
- Wants a modern website clients can actually book through
- Needs a staff dashboard to manage the schedule
<!-- 20s -->

---

<!-- slide 2 -->
# No modern booking system
- Current site is static — no online appointment booking
- Staff juggle schedules manually
- Product sales are an afterthought, not integrated
- Needs: service discovery → booking → dashboard → commerce

---

<!-- slide 3 -->
# What I built
- **Next.js 15** landing page — hero, services, gallery, team, reviews, contact
- **.NET 10 Web API** — Booking CRUD with EF Core + SQLite
- **Claude Code tooling** — 4 agents, 4 skills, 4 MCP servers
- Specs-driven: mission, tech stack, roadmap, tooling all in `specs/`

---

<!-- slide 4 -->
# How I built it
- **MCP**: Playwright (UI testing), Context7 (live docs), SQLite (DB inspection), GitHub (PRs)
- **Agent**: api-engineer, db-engineer, frontend-engineer, qa-verifier
- **Skill**: dev (full-stack launch), ef-migrations, feature-scaffold, openapi-client
- **Secret scanning**: gitleaks pre-commit + CI via GitHub Actions

---

<!-- slide 5 -->
# Why it matters
- Phase 0 (Foundation) is **done** — API boots, landing page works, tooling is wired
- 9-phase roadmap: services → booking → dashboard → products → launch
- Each agent owns a clear slice — no overlap, no confusion
- Ready to ship Phase 1–2 next (service catalog + booking core)

---

<!-- slide 6 -->
# Done checklist
- [x] repo public
- [x] MCP + skill + agent used
- [x] report.md in team repo
