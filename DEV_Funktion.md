# DEV Funktion — Phantombite AutoTransfer

Stand: 2026-09-19 · Workshop-ID 3693780953 · Core-Kanal 1995009 · Registrierter Kurzname: `autotrans`

## Zweck
Ladezonen für den automatischen Item-Transfer zwischen Spieler bzw. Schiff und Container („Trader Zone“),
ohne das Inventar-Fenster zu benutzen. Ergänzt Phantombite Economy (Waren ein- und auslagern), läuft aber
eigenständig und unabhängig davon.

## Bedienung (`!pbc autotrans ...`)
| Command | Admin | Wirkung |
|---|---|---|
| `in` | nein | Transfer starten (Spieler/Schiff → Container) |
| `out` | nein | Transfer starten (Container → Spieler/Schiff) |
| `stop` | nein | Transfer stoppen |
| `list` | nein | Container-Inhalt anzeigen |
| `scan` | ja | Zonen neu einlesen |
| `report` | ja | Zonenstatus anzeigen |
| `log` | ja | Transfer-Log ein/aus |

## Wie es arbeitet
- Zonen und Container werden über die **CustomData** der Blöcke beschrieben. Ein Template wird automatisch
  in passende Blöcke geschrieben (beim Start, bei neuen Blöcken und per Abfrage alle 5 Sekunden, weil
  `OnBlockAdded` auf Dedicated Servern unzuverlässig ist).
- Beim ersten Update werden Zonen aus der CustomData geladen. Gibt es keine, läuft automatisch ein Zonen-Scan.
- Transfers laufen im Stapel-Takt (Basis 2 s). Der Server führt aus, der Spieler bekommt Meldungen (Paket-Kanal
  für Benachrichtigungen, HUD-Meldung).

## Konfiguration
`AutoTransfer_Config.ini` im World-Storage (nur Server, wird bei Fehlen angelegt).

## Dateien
```
Data/Cubeblocks/   Cubeblocks_Logistic.sbc, _InventoryContainer.sbc, CubeBlocks_LCDPanels.sbc, Category
Data/Models/       Container- und Connector-Modelle
Data/Scripts/Phantombite Autotransfer/
  Core/     AutoTransfer_Session.cs, IModule.cs, ModuleManager.cs
  Modules/  AutoTransfer_Main.cs (Zonen, Transfer, LCDs, Templates — 2700 Zeilen)
            AutoTransfer_Command.cs (Core-Anbindung, Commands, Benachrichtigungen)
            AutoTransfer_FileManager.cs (Config)
```
Einige Blöcke benötigen den `AdminChip` aus Phantombite_Core.

## Core-Anbindung
Meldet sich als `autotrans` an. Empfängt `READY`, `LOGLEVEL`, `PERFLEVEL`, `CMD`. Meldet die 5-Sekunden-Abfrage
per `HEAVY_START`/`HEAVY_END`. **Performance-Level:** 1+ verdoppelt das Transfer-Intervall, 2 überspringt
Idle-Zonen, 3 begrenzt auf 5 Items pro Tick; die Template-Abfrage läuft bei höherem Level seltener (10/20/40 s).

## Offene Punkte / Roadmap
- [ ] Angleichen an `0_Phantombite_MOD_TEMPLATE.md` (README, patch_notes, thumb.jpg)
- [ ] `AutoTransfer_Main.cs` ist mit 2700 Zeilen sehr groß, Aufteilen in Zonen / Transfer / LCD / Templates wäre sinnvoll
- [ ] Ungenutztes Feld `AutoTransferFileManager._initialized` (Compile-Warnung)
- [ ] Der Ordner heißt auf GitHub `Phantombite_Autotransfer`, der lokale Mod-Name im Core ist `Phantombite_AutoTransfer` — vereinheitlichen
