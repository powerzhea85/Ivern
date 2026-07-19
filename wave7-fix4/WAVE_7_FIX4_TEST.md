# Wave 7 FIX4 Complete Spawner QA

Install all three DLLs from the package after removing every older Benjamin Menu and original SPTCheatMenu DLL.

Required checks:

- MP-133 inventory spawn is complete, equipable, reloadable and fireable.
- MP-133 ground spawn is complete, pickup works and weapon is usable.
- FIR-enabled MP-133 is recognized by Debut.
- A modded weapon with a runtime preset spawns complete and usable.
- A weapon without a complete registered preset is rejected instead of spawning a bare receiver.
- Vanilla and modded non-weapon items work in inventory and on ground.
- Large money and ammunition totals split using runtime max when stack size is 0.
- Custom stack size splits totals correctly and clamps to the real maximum.
- FIR toggle and Full Condition work.
- No new console errors.
