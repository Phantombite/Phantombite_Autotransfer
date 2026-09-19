# DEV History — Phantombite AutoTransfer

## 2026-09-19 — Bereinigung
- Compile-Fehler behoben: fehlende `using`-Zeilen (`VRage.ModAPI` für `IMyEntity`, `VRage.Game` für `MyFontEnum`)
- Die 5-Sekunden-Template-Abfrage meldet sich beim Core (`HEAVY_START`/`HEAVY_END`), überspringt Blöcke ohne
  Funktionsteil und läuft bei erhöhtem Performance-Level seltener
- Der Core kennt den Kurznamen `autotrans` jetzt als Alias (vorher bekam AutoTransfer nie ein Debug-Level)
- Doku angelegt, `.gitignore` ergänzt
