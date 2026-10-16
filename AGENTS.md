# One More Bonk minimal rebuild

Use the human user's instructions first. Keep the first release small and reviewable.

## Frozen product boundary

- Reference ForgeTax's researched forging/relic/economy loop. Preserve the verified probability and ordered effect implementation unless a specific defect is demonstrated.
- Keep the approved bright workshop UI direction and existing original sword art. Visual style reuse does not authorize importing the old Session/Career controllers.
- Relic hard maximum is 8. New local player profiles start with 2 unlocked slots. Slots 3–8 unlock sequentially through earned rewarded ads and persist across contracts/restarts: at most 6 successful slot unlocks per profile.
- A second independent placement may grant one existing ward/dragon scale per contract, in the merchant preparation page. It adds inventory; the player arms it manually. No post-break ad resurrection.
- Keep slot entitlement in Profile and ward budget in Run. Closed/shown/no-fill is not earned. Grant and receipt consumption must commit together and be idempotent.
- Do not add XP, talent trees, bonus-credit side orders, foundation offers, reservations, new currencies, energy, combat, ninth slots, or a generic live-rule/reward framework.
- Original rewind is a documented unresolved/deferred scope difference. Do not call fatal-only recovery equivalent to original undo or claim complete Steam parity.

## Code boundaries

- Reuse the audited forging kernel and required data/models. Do not import whole legacy ForgeSession, RiskSession, Career, ContractSession or RiskWorkshop controllers for convenience.
- Keep Unity data loading explicit. The initial RiskRelicData adapter uses Resources/JsonUtility; do not describe it as a zero-Unity dependency.
- Keep test-only Unity shims outside Assets. They must never be compiled into the Unity player.
- New saves, entry scene, namespace and build identity belong to this project. Do not read/write the old project's saves or overwrite its builds/settings.
- Never place SDK callbacks or animation-end handlers in charge of direct money, RNG, slot or inventory mutations.

## Verification and delivery

- When changing forging rules/models/catalogue data, run the self-contained golden-vector check under Tools and relevant ordered-effect tests. A 133-vector pass is probability evidence, not whole-game balance proof.
- For Session/reward changes, verify resource consistency, one-time commit, stale/duplicate callbacks, no-fill/cancel, write failure and saved entitlement continuity.
- Keep local paths, personal videos, credentials, caches, previous builds and generated research archives out of Git. Use repository-relative paths and source hashes for provenance.
- Report what was actually implemented/tested. Do not label the initial core baseline as a finished Unity game, real ad integration, or production-balanced release.
