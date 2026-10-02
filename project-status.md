# PROJECT STATUS – Food Production Simulator (Tiefkühlpizza-Linie)

**Stand:** 02.10.2026 (Mittag) · Commit vom 02.10. enthält alle Änderungen des Tages (Code, Szene, Configs, diese Datei). **Unity war nicht geöffnet** – der Code vom 02.10. Mittag ist nur syntaktisch geprüft (Parser), nicht von Unity kompiliert. Die Setup-Menüs (Oven, Cooling, Freezer, Packaging, Fault + Quality) sind **noch nicht ausgeführt**.

## Rolle in diesem Chat
[Beim Start der Tages-Session hier eintragen: PM / Dev A / Dev B / Dev C – siehe Rollenbeschreibung unten]

- **PM** – Rezept-/Fehlerfall-Design, Balancing, Asset-Sourcing, Playtesting/QA, Status-Datei-Pflege
- **Dev A** – Maschinen & Produktionsstationen (MachineBase, 8 Produktionsstationen)
- **Dev B** – Daten & Materialfluss (Förderbänder, Puffer, Sensoren, Recipe, Product-State, später Save/Load)
- **Dev C** – Bewegung/Interaktion, HMI & Plattform (First-Person, Aim-Interaktion, HMI/UI, Plattform-UX, Audio, Tutorial)

## Architektur-Konventionen (verbindlich, Stand 02.10.)
- Sprache: Englisch für Code/Kommentare/Bezeichner
- Naming (Microsoft): PascalCase (Klassen/Methoden/Properties/Events/öffentliche Felder), camelCase (Parameter/Locals), `_camelCase` (private Felder); ScriptableObject-Klassen mit Präfix `SO_` (z. B. `SO_PressConfig`)
- Assemblies: `Game.Core` → (keine) · `Game.Production` → Core, Localization, SerializeInterfaces · `Game.Quality` → Core, Production · `Game.HMI` → Core, Production, **Quality**, TMP, Localization · `Game.Platform` → Core, InputSystem, TMP · `Game.Editor` (nur Editor) → Core, Production, HMI, **Quality**, TMP. **`Game.Sensors` gibt es nicht mehr** – Sensoren und Produkt-Typen liegen in `Game.Production`
- Maschinen-Zustände (`MachineState`): `Ready → Starting → Running → Stopping → Stopped`, Störung `Fault → Maintenance → Ready`. API: `StartRun()`, `StopRun()`, `TriggerFault()`, `AcknowledgeFault()`, `CompleteMaintenance()`, `ResetToIdle()`, `RequestRun()` (startet aus Ready oder Stopped, nie aus Fault/Maintenance). Neu: `MachineBase.FaultReason` (öffentlich lesbar, Code des aktuellen Fehlers)
- Rückweg aus Stopped/Fault/Maintenance nur über explizite Bediener-/HMI-Aktion – kein Selbst-Reset
- **Warnungen** (HMI amber) sind ein separates Signal auf `MachineBase` (`HasWarning`, `WarningReason`, `WarningChanged`, `SetWarning()`), **kein** eigener `MachineState`. Weitergeleitet über `SO_MachineOverviewChannel.MachineWarningChanged`
- Keine Unity-Magic-Method-Namen in Maschinen-APIs
- Komposition statt Mehrfachvererbung: `IInteractable` sitzt auf Maschinenteilen (Knöpfe, Hebel) und HMI-Elementen, nie auf der Maschine selbst
- Rezept wird **nicht** im Code an Maschinen gebunden – Bediener liest Werte am Rezept-Terminal ab und stellt sie an der Maschine ein (`SetTargetWeight()`, `SetPressSpeed()` …)
- Produktzustand: `RawDough → MixedDough → PortionedDough → FormedPizza → SaucedPizza → ToppedPizza → BakedPizza → CooledPizza → FrozenPizza → PackagedPizza`
- Produktidentität auf physischen Objekten über `ProductToken` (trägt `ProductInstance`). Sensoren liefern Rohwerte, Maschinen bewerten, **Qualität bewertet nur das QualitySystem** am Linienende
- **Ausschuss-Markierung:** Stationen dürfen ein Produkt mit `ProductInstance.Reject(reason)` als Ausschuss markieren (z. B. Presshub durch Fault abgebrochen) – das endgültige Urteil fällt das QualitySystem
- **Förderbänder** sind `MachineBase` (`ConveyorBelt`), es gibt **kein** `IConveyor`/`ILoadReceiver` mehr (Dateien gelöscht). Produkte sind Rigidbodies; das Band setzt ihre horizontale Geschwindigkeit, es bewegt ein Produkt nur, wenn dessen Mitte über dem Band liegt. Teig liegt nie auf einem Band, das unter ihm durchläuft
- Nicht jede Strecke ist ein Band (Mixer → Portionierer: Arbeiter trägt den Topf)
- Bewegung: freies 3D-Movement (First-Person) mit Aim-Point-Interaktion – **keine** Pfeil-Navigation mehr
- HMI: Unity UGUI (NoesisGUI importiert, ungenutzt), `TextMeshProUGUI` in Views; Farben: Fault rot, Warning/Ausschuss amber, Normal grün, inaktiv grau
- Content als ScriptableObjects, keine Hardcoded-Werte; Lokalisierung über Unity Localization (Google Sheets), Fallback-Texte im Code bis Keys existieren
- **Terminal-Werte:** Maschinen liefern Sollwerte/Ist-Werte über `IMachineParameterSource`; das HMI kennt keine konkreten Maschinentypen. Neue Stationen implementieren das Interface statt eigener UI
- **Durchlaufstationen** (Dosierung, Ofen, Kühlung, Froster, Verpackung) besitzen ihr eigenes Band (`_belt`) und melden Aufnahmebereitschaft über `IInfeedReadiness`; das vorgelagerte Band wartet
- Input: nur neues Input System · Save-Format: JSON, versioniert (ab Woche 3)

## Aktueller Plan-Tag
Woche 2 (Tag [bitte eintragen]) – Code für **alle 8 Stationen, FaultSystem und QualitySystem** ist geschrieben. In der Szene läuft weiterhin **Mixer → Portionierer → Presse → Dosierstation** (Endstand `ToppedPizza`). Nächster Schritt: Unity öffnen und die Setup-Menüs ausführen (siehe „Nächste Schritte“).

## Fertige Features

### Produktionskette (getestet im Play Mode, 01.10.)
1. **MixerMachine** – mischt, Trommel kippen per Bediener-Knopf → Teigkugel (`MixedDough`, Gewicht aus `SO_MixerConfig`) fällt in den Topf
2. **Topf-Transport** – Pot ist `HoldInteractable`, Arbeiter trägt ihn zum Portionierer
3. **PortionerMachine** – `PotSensor` erkennt den Topf, Topf rastet auf dem Lift ein; Lift hoch/runter per Knöpfe, Teig fällt physisch in den `Hopper`; Portionierer erzeugt nach Gewicht `pizza`-Prefabs als `PortionedDough`; Hopper leeren per Knopf
4. **Förderstrecke** – Portionierer-Band → Zulaufband → Puffer-Band (4 Plätze) → Pressen-Band → Dosierstation
5. **PressMachine** – Hub startet, wenn der `ContactCollider` der Pizza die `TriggerZone` unter dem Kolben berührt; Teigling wird mittig ausgerichtet und kinematisch gehalten; `PortionedDough → FormedPizza`; danach fährt der Boden auf dem Band weiter

### Förderband- und Puffersystem (Dev B, 01.10.)
- `ConveyorBelt : MachineBase` – Transport, Start/Stop-Rampen, `SetSpeed()`, Seitenführung, Stau-Erkennung → Fault `Jam`. Betriebsarten: Transportband (pausiert bei Rückstau, Warning), Puffer-Band mit `AccumulationZone` (4 Plätze, Warning ab 3/4, Fault `BufferFull` nach 20 s voll), freies Band
- Puffer voll → Zulaufband, Portionierer-Band und Portionierer pausieren und laufen von selbst weiter
- `ConveyorLineController` – Linie in Flussrichtung, verkabelt Downstream automatisch, startet hinten → vorne, stoppt vorne → hinten, Start beim Play
- `PresenceSensor` mit Option **Products Only** und Collider-Liste
- Editor: *Tools → Food Production → Setup Portioner-Former Line*

### Terminals, Dosierstation, Durchlaufstationen (02.10. Vormittag)
- **Maschinen-Terminals** Mixer, Portionierer, Presse über `IMachineParameterSource` (Sollwerte mit −/+ und Halten-Repeat, Ist-Werte mit Farbstufe). In Szene eingebaut, Play Mode ohne Fehler – Bedienung noch nicht durchgespielt
- **Dosierstation** (`DosingMachine`) in Szene eingebaut: `SauceZone`/`ToppingZone`, Tanks mit Warning < 20 % und Fault `SauceEmpty`/`ToppingEmpty`, Nachfüllknopf, Sollwerte am Terminal. Produktdurchlauf noch nicht getestet
- **Ofen / Kühlung / Froster** (`ContinuousProcessStation` + `ProcessZone` + `SO_ContinuousProcessConfig`): Code fertig, **noch nicht in der Szene**

### Neu 02.10. Mittag (Code fertig, noch nicht in Unity kompiliert/ausgeführt)
- **Verpackung (8. Station):** `ContinuousProcessStation` ohne Temperatur (`FrozenPizza → PackagedPizza`, 8 s, Min 3 / Max 30 s). Menü *Tools → Food Production → Setup Packaging* (gleiche Logik wie die Tunnel, Name-Key `text.packaging`, Config `PackagingConfig.asset`, Platzhalter-Gehäuse braun). `TunnelStationSetup` legt bei Stationen ohne Temperatur keinen TemperatureSensor an und setzt `UsesTemperature`/`RequireTemperatureBeforeRun` aus der Spec
- **Produktdaten:** `ProductInstance` hat neu `BakeTemperatureCelsius`, `CoolingTemperatureCelsius`, `FreezingTemperatureCelsius` (jeder Tunnel schreibt seine eigene Durchschnittstemperatur – `MeasuredTemperatureCelsius` wurde vom nächsten Tunnel überschrieben) sowie `RejectReason`/`IsRejected`/`Reject()`
- **FaultSystem** (`Game.Production`, Ordner `Production/Faults`):
  - `SO_FaultCatalog` mit `FaultDefinition` (Code als Präfix, Meldung, Abhilfe-Text, Localization-Keys mit englischem Fallback, Trainingsfall ja/nein, injizierbar ja/nein)
  - **MVP-Fehlerfälle (PM-Entscheidung):** 1. `SauceEmpty`/`ToppingEmpty` (Dosierung) · 2. `BufferFull` (Pressen-Puffer) · 3. `Jam` (Band) · 4. `TemperatureOutOfRange`/`HeatUpTimeout` (Ofen, Heizungsausfall injizierbar) · 5. `WrongProduct` (Presse). Zusätzlich `BeltFault` (Station wegen Bandstörung, kein Trainingsfall)
  - `FaultMonitor` (eine Instanz pro Szene): beobachtet alle `MachineBase`, führt die Liste aktiver Fehler (Fault + Maintenance) mit Definition, zählt Fehler je Code und Stillstandszeit, Events `FaultsChanged`/`FaultRaised`/`FaultCleared`. Training: `InjectFault(machine, code)`, `InjectRandomTrainingFault()` (Kontextmenü), optional automatisch alle X s
  - **Heizungsausfall:** `ContinuousProcessStation.SimulateHeaterFailure()` schaltet die Temperaturregelung ab; die Temperatur driftet, Warning → nach Grace-Zeit Fault `TemperatureOutOfRange`. Erst Wartung (`OnEnterMaintenance`) repariert
  - **Gehaltenes Produkt im Fault (geklärt):** Presse mitten im Hub → Kolben fährt zurück, Teigling wird freigegeben und als Ausschuss `PressInterrupted` markiert (wird nicht nochmal gepresst). Presse wartet nur auf freien Auslauf → geformter Boden bleibt gehalten und wird nach dem Neustart freigegeben. Falsches Produkt in der Presse → Ausschuss `WrongProduct`, wird danach ignoriert und fährt durch (kein Dauer-Fault). Produkte in Tunneln bleiben drin (Verweilzeit wächst → QualitySystem). Produkte auf stehenden Bändern warten
- **QualitySystem** (`Game.Quality`):
  - `SO_QualitySpec` mit Margherita-Sollwerten und Toleranzen (Gewicht 250 ± 15 g, Ø 28 ± 1 cm, Dicke 3 ± 0,5 mm, Sauce 80 ± 8 g, Belag 120 ± 12 g, Backzeit 15 ± 1,5 s, Backtemperatur 250–320 °C, Kühlung ≤ 25 °C, Froster ≤ −15 °C, Endzustand `PackagedPizza`); jede Prüfung abschaltbar
  - `LineEndSink` (`Game.Production`): Trigger am Linienende, meldet jedes ankommende Produkt einmal und entfernt es nach 0,5 s → **behebt „Pizzen fallen vom Band“**
  - `QualityInspector`: bewertet jedes Produkt am `LineEndSink` (Defekt-Codes wie `Underweight`, `Overbaked`, `ColdChain`, `NotFinished:ToppedPizza`, `Rejected:PressInterrupted`, fehlende Messwerte `…Missing`), zählt Gut/Ausschuss, Defekte je Code, Durchsatz (gute Einheiten/min über 60 s), Verlauf der letzten 20, Log in der Konsole
- **HMI (Dev C):**
  - Overview: **Bänder erscheinen nur noch bei Fault** (`SO_MachineOverviewChannel.SetHiddenUnlessFault`, gesetzt vom `MachineOverviewReporter` für alle `ConveyorBelt`)
  - **Warning amber** in der Maschinenliste: `MachineStateRowView.SetWarning()` – Lampe amber, Grund hinter dem Zustand (Fault/Wartung haben Vorrang)
  - `LineStatusBinder`: füllt Durchsatz, produzierte Einheiten, Ausschuss (amber ab 10 %, rot ab 25 %), aktive Fehler und die **Alarmliste** (Maschine, Meldung + Abhilfe; in Wartung amber) aus FaultMonitor und QualityInspector
  - Platzhalter-Texte entfernt („New Text 1 … 9“ in `Panel_Machine`, `Canvas_HMI`, `MachineTerminal`; „Hello“ im `PromptLabel` der Szene)
- **Editor:** *Tools → Food Production → Setup Fault + Quality System* (`LineSystemsSetup.cs`) legt `LineSystems` (FaultMonitor, QualityInspector, LineStatusBinder) und `LineEnd` an, erstellt `ScriptableObjects/Faults/FaultCatalog.asset` und `ScriptableObjects/Quality/QualitySpec_Margherita.asset`. Die Tunnel-/Verpackungs-Setups verschieben ein vorhandenes `LineEnd` automatisch hinter die neue letzte Station
- **Kleinkram:** `OutputSensor` im Portionierer-Prefab ist jetzt Trigger · Build Settings: `Factory_Prototyp` als erste Szene · gelöscht: `IConveyor`, `ILoadReceiver`, `MachineIntakeAdapter`, `PortionerElevator`, `Hopper.AddDough()`

### Editor-Tools (Menü *Tools → Food Production*, alle wiederholbar + Undo, danach Strg+S)
- *Setup Portioner-Former Line* (01.10., ausgeführt)
- *Setup Machine Terminals (Portioner + Press)* (02.10., ausgeführt)
- *Setup Dosing Station* (02.10., ausgeführt)
- *Setup Oven* / *Setup Cooling Tunnel* / *Setup Shock Freezer* / *Setup Packaging* – je ein neues 4-m-Band hinter der vorherigen Station + Tunnel (**noch nicht ausgeführt**)
- *Setup Fault + Quality System* (**noch nicht ausgeführt**)

### Basis (Woche 1)
- `MachineBase`/`IMachine` mit validierter Übergangstabelle, `StateChanged`-Event, `MachineOverviewReporter` → `SO_MachineOverviewChannel`
- Sensoren: `PresenceSensor`, `WeightSensor`, `TemperatureSensor`, `LevelSensor` (+ `IFillLevelSource`), `PotSensor`
- `ProductState`, `ProductInstance`, `ProductToken`, `PizzaStateVisual`, `ProductStateTrigger`, `SO_RecipeDefinition`
- Plattform (Dev C): `FirstPersonController`, `AimInteractor`, `MouseInteractor`, `AimReticleView`, `HoldItem`/`HoldInteractable`, `InputModeController`
- HMI (Dev C): `HMI_Canvas` mit Overview-Panel und Maschinen-Panel, `RecipeTerminalPanel`, `HmiTerminalInteractable`, `SO_HmiTheme`
- Lokalisierung: Google-Sheets-Pull, `LanguageSwitcher`, Locales en/fr/de/eu (+ es)

## Nächste Schritte (Reihenfolge)
1. **Unity öffnen (6000.3.17f1), kompilieren lassen**, Konsole auf Fehler prüfen (neuer Code ist nur syntaktisch geprüft)
2. In `Factory_Prototyp` nacheinander ausführen: *Setup Oven* → *Setup Cooling Tunnel* → *Setup Shock Freezer* → *Setup Packaging* → *Setup Fault + Quality System*. Nach jedem Schritt prüfen, dass das neue Band nicht in eine Wand ragt (Laufrichtung −X hinter x = 31,75; 4 neue Bänder à 4 m = 16 m). Strg+S
3. **Durchlauftest** im Play Mode: Mixer → Topf → Portionierer → Presse → Dosierung → Ofen → Kühlung → Froster → Verpackung → `LineEnd`. In der Konsole `[Quality] … OK`? Overview: Durchsatz/Ausschuss/Alarme sichtbar? Terminals bedienen, Sollwerte ändern und Wirkung prüfen
4. **Fehlerfälle durchspielen:** Saucentank leer laufen lassen, Presse stoppen (BufferFull), Heizungsausfall per Kontextmenü am `FaultMonitor` („Inject random training fault“), jeweils Quittieren → Wartung → Start
5. Committen
6. Woche 3: Save/Load (JSON), Debug-/Balancing-Tools, Plattform-UX, Meilenstein-Build (WebGL)

## Bekannte Probleme / offene Punkte
- **Ungetestet:** alles vom 02.10. Mittag (siehe oben). Mögliche Stolperstellen: Bandgeschwindigkeit für Verweilzeiten (Warnung in der Konsole, falls `SO_ConveyorConfig` Min/Max nicht reicht), Platz in der Halle für 4 weitere Bänder, Lage des `LineEnd`-Triggers
- **Visuell:** `PizzaStateVisual` zeigt für Cooled/Frozen/Packaged noch das Baked-Modell (kein Karton-Modell); Tunnel/Verpackung/Dosierung sind Primitive-Platzhalter
- **Bänder optisch statisch:** `ConveyorPlankVisual` ist nicht angeschlossen; die Streifen sind Teil der Band-Meshes – braucht eigene Planken-Objekte oder Textur-Scrolling (im Editor klären)
- **Lokalisierung:** keine Keys für Content-Meldungen, Sollwert-/Ist-Wert-Labels, Fehlermeldungen/Abhilfen (`SO_FaultCatalog`), Defekt-Codes; Schockfroster hat keinen eigenen Name-Key (nutzt `text.cooling`). Keys im Google Sheet anlegen, dann in Assets eintragen
- **`DummyMachine`** bleibt, weil `SampleScene` sie noch nutzt (SampleScene ist zweite Szene in den Build Settings – ggf. entfernen)
- **`IProductProcessor`** wird von keiner Station mehr implementiert (nur noch in Kommentaren) – kann gelöscht werden, wenn niemand es plant
- **Cross-Team-Sync:** Änderungen an fremdem Code bestätigen lassen – Dev A: `MachineBase` (Warning, `RequestRun`, `FaultReason`), `PressMachine` (Fault-Verhalten, Reject), `PortionerMachine` (`_downstream`); Dev B: `PresenceSensor`, `TemperatureSensor.SetHeatingTarget()`, `ProductInstance` (neue Felder); Dev C: `OverviewPanel`, `MachineStateRowView`, `LineStatusBinder`, Prefab-Texte
- **Formanlage/Presse** ohne Dimensionssensor – Durchmesser/Dicke sind Bediener-Einstellung, das QualitySystem prüft diese Einstellung
- **Terminal-Position** neuer Stationen ist grob gesetzt (Seite −X, 1,14 m über Boden) – im Scene View prüfen
- **Gelöst 02.10.:** Linienende (LineEndSink), Bänder in der Overview, Warning amber, Platzhalter-Texte, OutputSensor-Trigger, Fault-Verhalten bei gehaltenem Produkt, ungenutzte Interfaces

## Margherita-Rezeptwerte (PM, vorläufig – Anpassung nach Playtest 1)
- Teigmischer: Mehl 500 g, Wasser 300 ml, Hefe 7 g, Salz 10 g, Öl 15 ml, Mischdauer 20 s
- Portionierer: Zielgewicht 250 g ± 15 g
- Presse (Formanlage): Durchmesser 28 cm, Dicke 3 mm (Toleranz QS: ± 1 cm / ± 0,5 mm)
- Dosierstation: Sauce 80 g, Belag 120 g (Toleranz QS: ± 10 %)
- Ofen: Backzeit 15 s (± 1,5 s), Backtemp 280 °C, Mindesttemp Fehlerfall 250 °C
- Kühleinheit: Zieltemp 20 °C, Dauer 10 s (QS: ≤ 25 °C)
- Schockfroster: Zieltemp −18 °C, Durchlaufzeit 12 s (QS: ≤ −15 °C)
- Verpackung: Durchlaufzeit 8 s

## Referenz: Tag-für-Tag-Plan (Kurzfassung)
Woche 1 (T1-5): Fundament – Setup, Interfaces, Machine-State-Machine, Bewegung/Interaktion, erste Maschinen
Woche 2 (T6-10): Restliche Maschinen, FaultSystem mit 3-5 Fehlerfällen, QualitySystem, HMI-Statistik
Woche 3 (T11-15): Save/Load, Debug-/Balancing-Tools, Plattform-UX, Meilenstein-Build
Woche 4 (T16-20): Stabilisierung, Bugfixing, MVP-Abnahme
Woche 5 (T21-25): VR-Proof-of-Concept (XR-Bindings, Bewegung/Teleport für freies Movement), Performance-Optimierung
Woche 6 (T26-30): Polish, erweiterte QA, Balancing-Tuning, Release
