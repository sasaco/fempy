Objective: Design formal terminology and API boundaries for FrameWeb calculation results.

Context:
- The default HTTP response is one canonical solver result for one selected load case, currently called "flat/new format". Typical keys are analysis_type, node_displacements, reaction_forces, element_stresses, metadata.
- A compatibility response selected by Accept application/vnd.frameweb.legacy-cases-v1+json solves all legacy beam load cases and returns an ordered case-id map. Each case is projected to UI fields disg/reac/fsec/shell_fsec/size.
- The user correctly observed that these should not primarily be called old/new formats; there is a containment/cardinality relationship.
- However the compatibility case payload is not literally identical to the canonical single-case result because projection/regrouping/sign conventions occur.
- Preserve the existing default API and avoid breaking current FrameWebforJS while designing a first-class feature.

Please recommend:
1. Precise domain names for single-case result, ordered multi-case collection, and UI projection.
2. A canonical v1 response envelope that makes the collection relationship explicit.
3. A media type name without "legacy".
4. Compatibility/deprecation migration for the existing legacy-cases-v1 media type.
5. Invariants: order, isolation, atomicity, case rate, maximum case count, and schema validation.

Output format:
## Recommendation
## Data Model
## Migration
## Risks
Keep the answer under 50 lines.
