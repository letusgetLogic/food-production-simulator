# PROJECT STATUS – Food Production Simulator (Tiefkühlpizza-Linie)

## Rolle in diesem Chat
[Beim Start der Tages-Session hier eintragen: PM / Dev A / Dev B / Dev C – siehe Rollenbeschreibung unten]

- **PM** – Rezept-/Fehlerfall-Design, Balancing, Asset-Sourcing, Playtesting/QA, Status-Datei-Pflege
- **Dev A** – Maschinen & Produktionsstationen (Machine-System, State Machine, 8 Produktionsstationen)
- **Dev B** – Daten & Materialfluss (Conveyor, Sensor, Recipe, Product-State, später Save/Load)
- **Dev C** – Navigation, HMI & Plattform (Pfeil-Navigation, HMI/UI, Plattform-UX, Audio, Tutorial)

## Architektur-Konventionen (verbindlich)
- Sprache: Englisch für Code/Kommentare/Bezeichner
- Naming: PascalCase (Klassen/Methoden/Properties/Events/öffentliche Felder, auch auf ScriptableObjects), camelCase (Parameter/Locals), _camelCase (private Felder)
- Kern-Interfaces: IMachine, ISensor, IProductProcessor, IConveyor, IQualityCheck, IInteractable/IInteractor
- Assembly-Struktur: Game.Core, Game.Production, Game.Sensors, Game.Quality, Game.HMI, Game.Platform
- Machine-State-Machine: Idle/Starting/Running/Stopping/Stopped, bei Fehler Fault/Maintenance/Running
- Fault ist aus fast jedem State erreichbar außer Maintenance; Ausweg aus Fault nur über Maintenance -> Idle
- Rückweg aus `Stopped`/`Fault`/`Maintenance` läuft **immer** über explizite Bediener-/HMI-Aktion (`ResetToIdle()`, `AcknowledgeFault()`, `CompleteMaintenance()`) – kein Selbst-Reset
- IMachine-API darf keine Unity-Magic-Method-Namen verwenden (z. B. `Start` → `StartMachine()`) – vor neuen Interface-Methoden gegenprüfen; `StartMachine()`/`StopMachine()` bestätigt symmetrisch
- `MachineBase` (echter Quellcode): Property heißt `CurrentState` (nicht `State`); `StartMachine()` geht nur Idle→Starting, `StopMachine()` nur Running→Stopping; Weiterschalten Starting→Running bzw. Stopping→Stopped erfolgt in der Subklasse über protected Hooks `OnEnterStarting()`/`OnEnterStopping()` + `SetState(MachineState target)`; ungültige Übergänge werden von `SetState` still (nur Warning-Log) abgewiesen
- Timing für Zustandsübergänge wird pro Maschine in der Subklasse definiert, nicht zentral in `MachineBase`
- **Komponenten-Komposition statt Mehrfachvererbung:** `MachineBase` und `InteractableBase` teilen keine gemeinsame Basisklasse – `IMachine` und `IInteractable` als separate Komponenten auf demselben GameObject (Entscheidung Tag 1/2)
- Maschinen müssen selbst **nicht** `IInteractable` implementieren – Bedienung erfolgt über separate Hotspots/HMI-Elemente (Dev C), nicht direkt am Maschinen-GameObject
- Rezept wird **nicht** automatisch im Code an die Maschine gebunden – Bediener liest Rezept am HMI ab und stellt Parameter manuell ein (z. B. `SetMixingDuration()`, `SetPortioningDuration()`, `SetFormingDuration()`)
- Produktzustand: RawDough → MixedDough → PortionedDough → FormedPizza → SaucedPizza → ToppedPizza → BakedPizza → CooledPizza → FrozenPizza → PackagedPizza (`ProductState`-Enum, 10 Werte, Game.Production)
- Bestätigte `IProductProcessor`-Signatur (echter Quellcode, nutzt `ProductInstance`, nicht `ProductToken`):
  - `ProductState ExpectedInputState { get; }`
  - `ProductState OutputState { get; }`
  - `bool CanAcceptProduct { get; }`
  - `bool TryBeginProcessing(ProductInstance product)`
  - `bool IsProcessingComplete { get; }`
  - `bool TryCollectProcessedProduct(out ProductInstance product)`
  - `TryBeginProcessing` validiert `product.CurrentState` gegen `ExpectedInputState`; `TryCollectProcessedProduct` aktualisiert `ProductInstance.CurrentState` auf `OutputState` und gibt das Produkt frei
- `IConveyor` transportiert nur physische Last (GameObject), **keine** ProductInstance – Produktidentität läuft über `ProductToken` + stationsseitige Sensoren; Übergabe zwischen Segmenten explizit über `TryReleaseLoad`/`TryAcceptLoad`, nicht automatisch
- `IsJammed` (Fault-relevant, physischer Defekt) ist von `IsBackedUp` (normale Rückstauung durch gestoppte Folgestation, kein Fehler) getrennt; `IsJammed` aktuell nur Platzhalter-Getter fürs spätere Fault-System, `IsBackedUp` hängt von extern gesetztem `SetDownstreamAvailabilityCheck` ab
- Sensoren liefern nur rohe Messwerte (`CurrentValue`); fachliche Bewertung (`IsWithinNormalRange`, Rezept-Konformität) liegt bei der Maschine, nicht beim Sensor; Sensoren scannen ihre eigene Trigger-Zone direkt und lösen die `ProductInstance` über das `ProductToken` des erfassten Objekts auf (Entscheidung Tag 2)
- Navigation: Punkt-zu-Punkt via interaktiver Pfeile, kein freies Movement
- Content als ScriptableObjects, keine Hardcoded-Werte
- Save-Format: JSON, versioniert (relevant ab Woche 3)
- Input: neues Unity Input System (`com.unity.inputsystem`), Active Input Handling = "Input System Package (New)"/"Both" – kein Legacy `UnityEngine.Input` in neuem Code

## Aktueller Plan-Tag
Tag 3 von 30 – Woche 1 – **Dev A abgeschlossen**; Dev B, Dev C, PM für Tag 3 noch offen

## Fertige Features

**Tag 1 – Dev A (Machine-System-Fundament, Game.Production):**
- `IMachine`-Interface, `MachineState`-Enum (7 Werte, XML-Docs)
- `MachineBase : MonoBehaviour, IMachine` mit validierter Zustandsübergangs-Tabelle, `StateChanged`-Event (reines C#-Event), `TriggerFault/AcknowledgeFault/CompleteMaintenance/ResetToIdle`
- `DummyMachine` als Referenzimplementierung (ersetzt Tag 2 durch Teigmischer)
- Im Editor Play Mode getestet, fehlerfrei

**Tag 1 – Dev B (Daten & Materialfluss, Game.Production/Game.Sensors):**
- `ProductState`-Enum (10 Werte, RawDough → PackagedPizza)
- `IConveyor` (physische Fördertechnik-API, Slot-basiert, mehrere Produkte gleichzeitig)
- `ProductToken` (Bindeglied physisches Objekt ↔ `ProductInstance`)
- `ISensor` / `ISensor<T>` (Game.Sensors) für alle 6 späteren Sensortypen, polymorphe Sensor-Listen
- `IProductProcessor` (Stationskontrakt, bewusst unabhängig von `MachineState` – Verzahnung folgt später)
- `RecipeDefinition`-ScriptableObject (Teig, Portionierung, Sauce, variable Toppings-Liste, Ziel-Backzeit/-temp, Endgewicht + Toleranzen) – bereit für PM-Rezeptwerte (Margherita)
- `ProductInstance`-Klasse (Referenz statt Struct, referenziert `RecipeDefinition` + `RecipeId`)

**Tag 1 – Dev C (Navigation/HMI/Plattform, Game.Core/Game.Platform):**
- `IInteractable`, `IInteractor` (Game.Core, plattform-agnostisch)
- `InteractableBase` (abstrakte MonoBehaviour-Basisklasse)
- `MouseInteractor : IInteractor` (Desktop, neues Input System)
- `DebugLogInteractable` (Smoke-Test, erfolgreich getestet – temporär, Wegwerf-Referenz)
- Assembly-Trennung `Game.Core`/`Game.Platform` real umgesetzt
- Szenen-Grundgerüst `Factory_Main` (URP, Blockout, 8 Stations-Platzhalter mit Collidern)
- Ordnerstruktur vollständig angelegt
- WebGL-Build-Grundkonfiguration (Brotli, 512 MB, Linear Color Space, Data Caching)

**Tag 1 – PM:**
- Backlog-Grundgerüst Phase 1 (#1–20, Abgleich mit Original-Projektplan noch offen)
- Margherita-Rezeptwerte für alle 8 Stationen (Details: siehe letzte PM-Session bzw. `tag1-pm-ergebnisse.md`)
- 5 Fehlerfälle konkretisiert
- Asset-Recherche: 11 Kandidaten, Empfehlung *Factory Industrial Complex* + ein Conveyor-Paket

**Tag 2 – Dev A (erste echte Maschine, Game.Production):**
- Teigmischer (`DoughMixerMachine` + `DoughMixerConfig`) implementiert, `DummyMachine` ersetzt
- Implementiert `IMachine` (via `MachineBase`) und `IProductProcessor` (`CanAcceptProduct`, `TryBeginProcessing`, `IsProcessingComplete`, `TryCollectProcessedProduct`)
- Kein `IInteractable` – Bedienung ausschließlich über Hotspot/HMI (Dev C) via `StartMachine()`/`StopMachine()`
- Kein direkter `RecipeDefinition`-Bezug im Code – Bediener liest Rezept am Bedienfeld ab und ruft `SetMixingDuration(float seconds)` auf
- Starting/Stopping-Timing selbst implementiert via `OnEnterStarting()`/`OnEnterStopping()` + `SetState(...)`, Dauer aus `DoughMixerConfig` (`StartupDurationSeconds`/`ShutdownDurationSeconds`)
- `OnEnterFault()` stoppt laufende Timing-Coroutinen (nur minimale Absicherung, kein echtes Fault-Handling)
- Konventions-Fix: öffentliche Felder in `DoughMixerConfig` von camelCase auf PascalCase korrigiert

**Tag 2 – Dev B (Conveyor & Sensoren, Game.Production/Game.Sensors):**
- `ConveyorConfig` (ScriptableObject: `SlotCount`, `SlotSpacing`, `TransportSpeed`, `AllowManualJamToggle`)
- `SlotConveyor` (finale `IConveyor`-Implementierung): verwaltet nur GameObject-Loads + `SlotOccupancy` (`IReadOnlyList<bool>`), kennt bewusst weder `ProductToken` noch `ProductInstance`. Bewegung pro Frame von hinten nach vorne innerhalb der eigenen Slots; Übergabe an den nächsten Abschnitt läuft NICHT automatisch, sondern über `TryReleaseLoad(out load)`/`TryAcceptLoad(load)`, aufgerufen von einem noch zu bauenden Controller
- `IsBackedUp` ist nur aussagekräftig, wenn extern per `SetDownstreamAvailabilityCheck(Func<bool>)` mitgeteilt wird, ob der nächste Abschnitt aufnahmebereit ist – sonst wartet ein fertiger Load einfach im Exit-Slot
- `IsJammed` ist reiner Getter im Interface; da noch keine echte Jam-Hardware simuliert wird, gibt es intern `SetJammed(bool)` als Platzhalter fürs spätere Fault-System
- finaler `IConveyor`-Vertrag: `SlotCount`, `SlotOccupancy` (`IReadOnlyList<bool>`), `IsJammed` (get-only), `IsBackedUp` (get-only), `CanAcceptLoad`, `TryAcceptLoad(GameObject)`, `TryReleaseLoad(out GameObject)` – arbeitet ausschließlich mit physischen Loads, keine Kenntnis von Produktidentität
- `PresenceSensor` (`ISensor<bool>`) und `WeightSensor` (`ISensor<float>`) scannen ihre eigene Trigger-Zone direkt (`OnTriggerEnter`/`Exit`) statt den Conveyor nach Slot-Inhalten zu fragen – entkoppelt Game.Sensors sauber von der konkreten `IConveyor`-Implementierung
- `PresenceSensor` braucht kein `ProductToken` (reine physische Anwesenheit über Overlap-Zähler); `WeightSensor` holt sich `ProductToken` per `TryGetComponent` vom eintretenden Collider und liest `ProductInstance.MeasuredWeightGrams`
- `PresenceSensor` wird vom Mischer genutzt, um zu prüfen ob die Schüssel vorhanden ist – Voraussetzung zum Starten
- Architektur-Entscheidung (bewusst noch NICHT implementiert): Eine Strecke zwischen zwei Maschinen kann aus mehreren `IConveyor`-Segmenten bestehen, gesteuert über einen Controller (physisch: Steuerungskasten) – natürlicher Ort für die Verkettung von `TryReleaseLoad`/`TryAcceptLoad` zwischen Segmenten sowie das `SetDownstreamAvailabilityCheck`-Wiring. Wartet auf explizite Aufgabenstellung

**Tag 3 – Dev A (Portionierer & Former, Game.Production):**
- `PortionerMachine` (`MixedDough` → `PortionedDough`) implementiert – gewichtsbasiert, Zielgewicht/Toleranz per Config + HMI-Setter (`SetTargetWeight`, `SetToleranceGrams`, `SetPortioningDuration`), Bewertung über `WeightSensor.CurrentValue` + `SetWithinNormalRange(bool)` auf dem Sensor (Maschine bewertet, Sensor liefert nur Rohwert)
- `FormerMachine` (`PortionedDough` → `FormedPizza`) implementiert – gleiches Pattern, aber bewusst ohne Qualitätsbewertung im Code, da kein Dimensions-/Formsensor existiert (nur `PresenceSensor`/`WeightSensor` bestätigt); Zieldurchmesser/-dicke in `FormerConfig` nur als Referenzwerte für HMI/spätere QualitySystem-Nutzung
- Damit 3 von 8 Produktionsmaschinen fertig (Teigmischer, Portionierer, Former)

## In Arbeit
- **Dev A:** vierte Produktionsmaschine (Dosierstation für Sauce/Belag, `FormedPizza` → `SaucedPizza` → `ToppedPizza`), noch innerhalb Woche 1 (T1-5, erste 4 Maschinen)
- **Dev B, Dev C, PM:** konkrete Tag-3-Aufgabenstellung noch offen. Naheliegend für Dev B laut Tag-2-Notiz: Conveyor-Line-Controller zur Verkettung mehrerer `IConveyor`-Segmente

## Bekannte Probleme / offene Punkte
- **Gelöst (Tag 1→2):** Kollision `MachineBase`/`InteractableBase` als gemeinsame Basisklasse nicht möglich → Komposition-Lösung beschlossen. Am Teigmischer bestätigt: Maschine implementiert kein `IInteractable`, Bedienung läuft über Hotspot/HMI.
- **Gelöst (Tag 2):** Sensor-Erkennungsmodell entschieden – pro physische Trigger-Zone statt slot-basiert; Sensoren entkoppelt vom `IConveyor`.
- **Gelöst (Tag 3):** `IProductProcessor`-Signatur im echten Quellcode bestätigt (nutzt `ProductInstance`, nicht `ProductToken`); `MachineBase`-Property heißt `CurrentState`; Fault aus fast jedem State erreichbar außer Maintenance, Ausweg nur über Maintenance → Idle.
- **Offen, vor Woche 2 (FaultSystem):** Echtes Fault-Verhalten bei laufender Verarbeitung – was passiert mit dem gehaltenen Produkt (`_heldProduct`) bei Fault während Mischen/Portionieren/Formen? Bisher bei allen drei Maschinen nur minimale Absicherung (Coroutinen stoppen).
- **Offen, für Tag 3 (Dev B, voraussichtlich):** Conveyor-Line-Controller zur Verkettung mehrerer `IConveyor`-Segmente (`TryReleaseLoad`/`TryAcceptLoad`, `SetDownstreamAvailabilityCheck`-Wiring) noch nicht gebaut.
- **Offen, vor FaultSystem (Tag 7/8) zu klären:** Wird ein Conveyor selbst ein `IMachine` (mit `MachineState.Fault` bei `IsJammed`), oder wertet das FaultSystem `IsJammed` separat aus, ohne dass der Conveyor `IMachine` implementiert? Betrifft PM-Fehlerfall 5 (Conveyor-Stau).
- **Offen, vor Woche 2 Ende:** `StateChanged` ist aktuell reines C#-Event, keine ScriptableObject-Event-Channel-Architektur. Team-Entscheidung nötig, bevor Dev C die HMI-Statistik baut (~Tag 9/10) – danach wird ein Wechsel teurer.
- **Offen:** Falls/sobald ein Dimensions- oder Formsensor im Sensors-Assembly ergänzt wird, muss `FormerMachine` analog zum Portionierer um eine Qualitätsbewertung erweitert werden.
- Backlog-Grundgerüst (#1–20) noch nicht gegen Original-`food-production-simulator-projektplan.md` verifiziert.

## Margherita-Rezeptwerte (PM, spielbar-schnell, vorläufig – Anpassung nach Playtest 1 Tag 8)
- Teigmischer: Mehl 500g, Wasser 300ml, Hefe 7g, Salz 10g, Öl 15ml, Mischdauer 20s (vorläufig)
- Portionierer: Zielgewicht 250g ± 15g Toleranz (vorläufig)
- Formanlage: Durchmesser 28cm, Dicke 3mm (nur Richtwerte, kein Sensor)
- Dosierstation: Sauce 80g, Belag 120g
- Ofen: Backzeit 15s (vorläufig), Backtemp 280°C, Mindesttemp Fehlerfall 250°C (vorläufig)
- Kühleinheit: Zieltemp 20°C, Dauer 10s
- Schockfroster: Zieltemp −18°C, Durchlaufzeit 12s (vorläufig)
- Verpackung: Durchlaufzeit 8s

## Referenz: Tag-für-Tag-Plan (Kurzfassung)
Woche 1 (T1-5): Fundament – Setup, Interfaces, Machine-State-Machine, Navigation-Pfeile, erste 4 Maschinen
Woche 2 (T6-10): Restliche 4 Maschinen, FaultSystem mit 3-5 Fehlerfällen, QualitySystem, HMI-Statistik
Woche 3 (T11-15): Save/Load, Debug-/Balancing-Tools, Plattform-UX, Meilenstein-Build
Woche 4 (T16-20): Stabilisierung, Bugfixing, MVP-Abnahme
Woche 5 (T21-25): VR-Proof-of-Concept (XR-Bindings, Pfeile als Teleport-Ziele), Performance-Optimierung
Woche 6 (T26-30): Polish, erweiterte QA, Balancing-Tuning, Release
