Objective: Validate the initial diagnosis and choose the safest fix boundary.

Evidence: HTTP 200 returns one flat modern result (`node_displacements`, `reaction_forces`, `element_stresses`). Old FrameWeb2 and FrameWebforJS require `{caseId: {disg, reac, fsec}}`. Angular workers silently skip fields without those keys and return empty success. Ct has 11 load cases, but current legacy parsing selects only the first. Existing modern HTTP tests require the flat response.

Constraints: read-only; do not inspect files beyond `.agents/docs/research/troubleshoot-framewebforjs-results-not-displayed-context.md`. Preserve modern API consumers and all Ct case IDs.

Output exactly:
## Verdict
CONFIRMED or REJECTED, with two sentences.
## Safest Boundary
Choose backend replacement, frontend adaptation, or versioned/explicit compatibility adapter, with two sentences.
## Must-Test Contracts
Maximum five bullets.
