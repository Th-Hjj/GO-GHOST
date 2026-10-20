# Retained compatibility boundary

These types remain because the actual original player/enemy executors and serialized prefab contracts still refer to them. They do not enable historical progression in the formal Duel.

- LoadoutState: retained enums, identifiers, deployment data and pure shared LoadoutRules. The old draft, victory/archive and NextOption helpers live in LegacyLoadout under Legacy/Assets/_Legacy/Prototypes/SharedPlayer.
- AugmentationDefinition / IAttemptAugmentation: original actor/augmentation interfaces and definitions.
- LegacyCombatIntent: original input-request contract.
- PlayerCounterArm / PlayerTimeSlow / InnerAIDangerSense: optional components referenced by retained executors.
- Playerabilityso / Playerabilitysystem / Assistrules: original serialized/API compatibility.

WeaponProfile lives in Shared/Scripts/Player. BodyDiagramGeometry, MissionBodyRegion and MissionParticleBrain live in Shared/Scripts/Presentation. Current conditional threat prediction lives in Levels/L01_Duel/Scripts/ConditionalThreatGeometry.

Preserve APIs/field names and prefab schema. Current runtime must not depend on archive-owned classes or assets. Legacy may reuse these contracts. These current files compile independently of the optional root Legacy archive. Keep them when deleting Legacy: they belong to the retained game executors. Historical partial extensions are absent by default and return only after explicit archive restoration.
