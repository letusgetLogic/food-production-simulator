# PROJECT STATUS – Food Production Simulator (Tiefkühlpizza-Linie)

## Rolle in diesem Chat
[Beim Start der Tages-Session hier eintragen: PM / Dev A / Dev B / Dev C – siehe Rollenbeschreibung unten]

- **PM** – Rezept-/Fehlerfall-Design, Balancing, Asset-Sourcing, Playtesting/QA, Status-Datei-Pflege
- **Dev A** – Maschinen & Produktionsstationen (Machine-System, State Machine, 8 Produktionsstationen)
- **Dev B** – Daten & Materialfluss (Conveyor, Sensor, Recipe, Product-State, später Save/Load)
- **Dev C** – Navigation, HMI & Plattform (Pfeil-Navigation, HMI/UI, Plattform-UX, Audio, Tutorial)

## Architektur-Konventionen (verbindlich)
- Sprache: Englisch für Code/Kommentare/Bezeichner
- Naming: PascalCase (Klassen/Methoden/Properties/Events), camelCase (Parameter/Locals), _camelCase (private Felder)
- Kern-Interfaces: IMachine, ISensor, IProductProcessor, IConveyor, IQualityCheck, IInteractable/IInteractor
- Assembly-Struktur: Game.Core, Game.Production, Game.Sensors, Game.Quality, Game.HMI, Game.Platform
- Machine-State-Machine: Idle/Starting/Running/Stopping/Stopped, bei Fehler Fault/Maintenance/Running
- Rückweg aus `Stopped`/`Fault`/`Maintenance` läuft **immer** über explizite Bediener-/HMI-Aktion (`ResetToIdle()`, `AcknowledgeFault()`, `CompleteMaintenance()`) – kein Selbst-Reset
- IMachine-API darf keine Unity-Magic-Method-Namen verwenden (z. B. `Start` → `StartMachine()`) – vor neuen Interface-Methoden gegenprüfen
- Produktzustand: RawDough → ... → PackagedPizza (`ProductState`-Enum, 10 Werte, Game.Production)
- `IConveyor` transportiert nur physische Last (GameObject), **keine** ProductInstance – Produktidentität läuft über `ProductToken` + stationsseitige Sensoren
- `IsJammed` (Fault-relevant, physischer Defekt) ist von `IsBackedUp` (normale Rückstauung durch gestoppte Folgestation, kein Fehler) getrennt
- **Komponenten-Komposition statt Mehrfachvererbung:** Eine Station kann gleichzeitig Maschine (`IMachine`) und interaktiv (`IInteractable`) sein. Da beides als MonoBehaviour-Basisklasse (`MachineBase`, `InteractableBase`) existiert und C# keine Mehrfachvererbung erlaubt, wird `InteractableBase`-Funktionalität als **separate Komponente auf demselben GameObject** eingebunden, nicht als gemeinsame Basisklasse. (Entscheidung Tag 1/2, siehe unten)
- Navigation: Punkt-zu-Punkt via interaktiver Pfeile, kein freies Movement
- Content als ScriptableObjects, keine Hardcoded-Werte
- Save-Format: JSON, versioniert (relevant ab Woche 3)
- Input: neues Unity Input System (`com.unity.inputsystem`), Active Input Handling = "Input System Package (New)"/"Both" – kein Legacy `UnityEngine.Input` in neuem Code

## Aktueller Plan-Tag
Tag 2 von 30 – Woche 1

## Fertige Features

**Tag 1 – Dev A (Machine-System-Fundament, Game.Production):**
- `IMachine`-Interface, `MachineState`-Enum (7 Werte, XML-Docs)
- `MachineBase : MonoBehaviour, IMachine` mit validierter Zustandsübergangs-Tabelle, `StateChanged`-Event (reines C#-Event), `TriggerFault/AcknowledgeFault/CompleteMaintenance/ResetToIdle`
- `DummyMachine` als Referenzimplementierung (**temporär** – wird Tag 2 durch Teigmischer ersetzt)
- Im Editor Play Mode getestet, fehlerfrei

**Tag 1 – Dev B (Daten & Materialfluss, Game.Production/Game.Sensors):**
- `ProductState`-Enum (10 Werte, RawDough → PackagedPizza)
- `IConveyor` (physische Fördertechnik-API, Slot-basiert, mehrere Produkte gleichzeitig)
- `ProductToken` (Bindeglied physisches Objekt ↔ `ProductInstance`)
- `ISensor` / `ISensor<T>` (Game.Sensors) für alle 6 späteren Sensortypen, polymorphe Sensor-Listen
- `IProductProcessor` (Stationskontrakt, bewusst unabhängig von `MachineState` – Verzahnung folgt später)
- `RecipeDefinition`-ScriptableObject (Teig, Portionierung, Sauce, variable Toppings-Liste, Ziel-Backzeit/-temp, Endgewicht + Toleranzen) – **bereit für PM-Rezeptwerte (Margherita, siehe unten)**
- `ProductInstance`-Klasse (Referenz statt Struct, referenziert `RecipeDefinition` + `RecipeId`)

**Tag 1 – Dev C (Navigation/HMI/Plattform, Game.Core/Game.Platform):**
- `IInteractable`, `IInteractor` (Game.Core, plattform-agnostisch)
- `InteractableBase` (abstrakte MonoBehaviour-Basisklasse)
- `MouseInteractor : IInteractor` (Desktop, neues Input System)
- `DebugLogInteractable` (Smoke-Test, erfolgreich getestet – **temporär**, Wegwerf-Referenz)
- Assembly-Trennung `Game.Core`/`Game.Platform` real umgesetzt
- Szenen-Grundgerüst `Factory_Main` (URP, Blockout, 8 Stations-Platzhalter mit Collidern)
- Ordnerstruktur vollständig angelegt
- WebGL-Build-Grundkonfiguration (Brotli, 512 MB, Linear Color Space, Data Caching)

**Tag 1 – PM:**
- Backlog-Grundgerüst Phase 1 (#1–20, Abgleich mit Original-Projektplan noch offen)
- Margherita-Rezeptwerte für alle 8 Stationen (Details: siehe letzte PM-Session bzw. `tag1-pm-ergebnisse.md`)
- 5 Fehlerfälle konkretisiert
- Asset-Recherche: 11 Kandidaten, Empfehlung *Factory Industrial Complex* + ein Conveyor-Paket

## In Arbeit
- Dev A: Tag 2 – erste echte Maschine (Teigmischer), `DummyMachine` wird ersetzt. **Muss die neue Komposition-Konvention (IInteractable als separate Komponente) berücksichtigen.**
- Dev C: Tag 2 – Pfeil-Hotspots (`IInteractable`-Implementierungen auf `InteractableBase`), Kamera-Übergangslogik. `DebugLogInteractable` danach entfernen.
- Dev B: bereit für Tag 2/3 – konkrete Sensor-Implementierungen, echtes Conveyor-Verhalten/Bewegung

## Bekannte Probleme / offene Punkte
- **Gelöst (Tag 1→2):** Kollision `MachineBase`/`InteractableBase` als gemeinsame Basisklasse nicht möglich → Komposition-Lösung beschlossen (siehe Architektur-Konventionen oben). Noch offen: konkrete Umsetzung an der ersten echten Maschine verifizieren.
- **Offen, vor FaultSystem (Tag 7/8) zu klären:** Wird ein Conveyor selbst ein `IMachine` (mit `MachineState.Fault` bei `IsJammed`), oder wertet das FaultSystem `IsJammed` separat aus, ohne dass der Conveyor `IMachine` implementiert? Betrifft PM-Fehlerfall 5 (Conveyor-Stau).
- **Offen, vor Woche 2 Ende:** `StateChanged` ist aktuell reines C#-Event, keine ScriptableObject-Event-Channel-Architektur. Team-Entscheidung nötig, bevor Dev C die HMI-Statistik baut (~Tag 9/10) – danach wird ein Wechsel teurer.
- Backlog-Grundgerüst (#1–20) noch nicht gegen Original-`food-production-simulator-projektplan.md` verifiziert.

## Referenz: Tag-für-Tag-Plan (Kurzfassung)
Woche 1 (T1-5): Fundament – Setup, Interfaces, Machine-State-Machine, Navigation-Pfeile, erste 4 Maschinen
Woche 2 (T6-10): Restliche 4 Maschinen, FaultSystem mit 3-5 Fehlerfällen, QualitySystem, HMI-Statistik
Woche 3 (T11-15): Save/Load, Debug-/Balancing-Tools, Plattform-UX, Meilenstein-Build
Woche 4 (T16-20): Stabilisierung, Bugfixing, MVP-Abnahme
Woche 5 (T21-25): VR-Proof-of-Concept (XR-Bindings, Pfeile als Teleport-Ziele), Performance-Optimierung
Woche 6 (T26-30): Polish, erweiterte QA, Balancing-Tuning, Release
