# Phantombite AutoTransfer

Ladezonen für den automatischen Item-Transfer zwischen Spieler bzw. Schiff und Container („Trader Zone“), **ohne das
Inventar-Fenster** zu öffnen. Ideal für große Einkäufe und Lieferungen, zum Beispiel zusammen mit Phantombite Economy.

## Funktionen
- Zonen und Container werden über die **Custom Data** der Blöcke beschrieben, Vorlagen werden automatisch eingetragen
- Transfer Spieler/Schiff → Container und zurück, mit Stapel-Takt, Benachrichtigungen und LCD-Anzeige
- Zonen werden beim Start automatisch gefunden und lassen sich neu einlesen
- Reagiert auf die Server-Last: bei Überlast langsameres Tempo (über den Phantombite Core)

## Commands
```
!pbc autotrans <command>
```
| Command | Wer | Beschreibung |
|---|---|---|
| `in` | alle | Transfer starten (Spieler/Schiff → Container) |
| `out` | alle | Transfer starten (Container → Spieler/Schiff) |
| `stop` | alle | Transfer stoppen |
| `list` | alle | Container-Inhalt anzeigen |
| `scan` | Admin | Zonen neu einlesen |
| `report` | Admin | Zonenstatus anzeigen |
| `log` | Admin | Transfer-Log ein/aus |

## Konfiguration
`AutoTransfer_Config.ini` im World-Storage (wird beim ersten Start angelegt).

## Voraussetzungen
- **Phantombite Core** (Commands, außerdem benötigen einige Blöcke den `AdminChip`)

Workshop-ID: 3693780953
