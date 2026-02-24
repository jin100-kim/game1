# Risk Log

| ID | Date | Risk | Impact | Mitigation | Status |
|---|---|---|---|---|---|
| R-001 | 2026-02-22 | Runtime-generated UI may need layout tuning on uncommon resolutions. | Medium | Keep anchors centralized and add resolution smoke check in next slice. | Open |
| R-002 | 2026-02-22 | Enemy/projectile logic is object-per-entity; high counts may drop FPS later. | Medium | Move to pooling when profiling shows sustained pressure. | Open |
| R-003 | 2026-02-22 | Current level-up option set is fixed, not weighted/randomized. | Low | Add weighted option table and rarity logic in balancing slice. | Open |
| R-004 | 2026-02-22 | Automated tests cover math/rules only, not PlayMode integration. | Medium | Add PlayMode smoke test after milestone stabilization. | Open |
