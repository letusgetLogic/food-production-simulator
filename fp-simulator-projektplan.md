# Food Production Simulator – Projektplan (Unity 6, 3D, Browser → VR-erweiterbar)

## 0. Rahmenbedingungen & Annahmen

| Parameter | Wert |
|---|---|
| Engine | Unity 6 (URP), 3D |
| Zielplattform Phase 1 | WebGL (Browser) |
| Zielplattform Phase 2 | VR (Architektur-Vorbereitung, kein volles VR-Feature-Set) |
| Genre | Industrielle Produktionssimulation (Tiefkühlpizza-Fertigung) |
| Team | 4 Accounts: 1 PM (kein Code) + 3 Entwickler (C#) |
| Unity-Erfahrung | Fortgeschritten (Prefabs, ScriptableObjects bekannt) |
| MVP-Umfang | Eine Produktionslinie, ein Pizza-Rezept (Margherita), 8 Stationen (Teigmischer bis Verpackung), Förderbänder, Kern-Sensorik, 3–5 Fehlerfälle, einfaches HMI, Produktionsstatistik |
| Bewegung/Navigation | Punkt-zu-Punkt via interaktiver Pfeile (Hotspot-Navigation), kein freies WASD-Movement |
| Assets | Placeholder (Start) → Store-Assets + KI-generierte Inhalte (laufend ersetzt) |
| Zusatzfeatures | Save/Load, plattformspezifisches UX, Balancing-/Debug-Tools, Tutorial/Onboarding |
| AI-Plan | Alle 4 Accounts: Claude Free, nur Chat (claude.ai), kein Claude Code, kein API-Zugang |
| Gesamtdauer | 6 Wochen = 30 Arbeitstage, voller Tag-für-Tag-Plan über alle 30 Tage: Tag 1–20 Kernproduktion, Tag 21–30 VR-PoC/Polish/Puffer |

**Änderungen gegenüber der Vorversion:** Das Design-Dokument ersetzt das generische "Ketten/Economy/Progression"-Konzept durch eine spezifische industrielle Produktionslinie mit Maschinen-Zustandsautomaten, Sensorik, Rezept-/Produkt-Zustandsmodell und Fehlersystem. Economy- und Freischalt-Progression entfallen; an ihre Stelle tritt die Produktionsstatistik (produzierte Einheiten/Ausschuss) aus dem HMI. Die Bewegung erfolgt über klickbare Navigations-Pfeile statt freier Spielerbewegung – das reduziert den Aufwand für den Player-Controller und passt architektonisch gut zur späteren VR-Teleport-Navigation (Wiederverwendung des gleichen Hotspot-Konzepts).

**Hinweis zu AI-Limits:** unverändert gegenüber der Vorversion – siehe Abschnitt 5.

---

## 1. Feature-Liste mit Aufwandsschätzung

Kapazität Kernphase (Tag 1–20): 3 Entwickler × 20 Tage = 60 PT. Kapazität Phase 2 (Tag 21–30): 3 Entwickler × 10 Tage = 30 PT.

### Phase 1 – Kernproduktion (Tag 1–20)

| # | Feature | Beschreibung | Aufwand (PT) |
|---|---|---|---|
| 1 | Projekt-Setup & Architektur | Repo, Unity-Projekt, Assembly Definitions, Coding-Konventionen, Paket-Setup (URP, Input System) | 3 |
| 2 | Interaction-Abstraction-Layer | `IInteractable`/`IInteractor` für Navigations-Pfeile und HMI-Bedienelemente, entkoppelt vom konkreten Input | 2 |
| 3 | Navigation (Pfeil-Hotspots) | Klickbare Bereichs-Pfeile, Kamera-Übergänge zwischen Stationen – Grundlage für spätere VR-Teleport-Navigation | 3 |
| 4 | Conveyor-System | `IConveyor`: Transport der Produktobjekte zwischen Stationen, Blockade-Erkennung | 3 |
| 5 | Machine-System | `IMachine` + generische State Machine (Idle/Starting/Running/Stopping/Stopped, Fault/Maintenance) | 5 |
| 6 | Sensor-System | `ISensor`: Presence-, Weight-, Temperature-, Level-, Motor-, Jam-Sensor | 3 |
| 7 | Recipe-System | ScriptableObject-Rezeptdefinition (Margherita: Mengen, Zeiten, Temperaturen) | 3 |
| 8 | Product-State-System | `IProductProcessor`: Zustandskette RawDough → … → PackagedPizza | 4 |
| 9 | 8 Produktionsstationen | Teigmischer, Portionierer, Formanlage, Dosierstation, Ofen, Kühleinheit, Schockfroster, Verpackung als `IMachine`-Implementierungen | 7 |
| 10 | Fault-System | 3–5 Fehlerfälle: Material fehlt, Förderband blockiert, Ofentemp. zu niedrig, falsches Gewicht, Sensorfehler | 3 |
| 11 | Quality-Check | `IQualityCheck`: Gewichts-/Temperaturprüfung → Ausschuss/Ausschleusen | 1 |
| 12 | HMI/UI-System | Industrielles Bedienpanel: Status, Maschinenzustände, Temperaturen, Füllstände, Produktionsgeschwindigkeit, produzierte Einheiten, Ausschuss, aktive Fehler | 5 |
| 13 | Save/Load-System | JSON-Serialisierung von Linien-, Maschinen- und Statistikzustand | 3 |
| 14 | Balancing-/Debug-Tools | Editor-Tuning via ScriptableObjects, Debug-Menü (Fehler/Sensorwerte manuell auslösen) | 3 |
| 15 | Plattform-UX (Browser) | WebGL-Build-Konfiguration, Loading-Screen, UI-Skalierung | 3 |
| 16 | Tutorial/Onboarding | Einführung in Navigation und HMI-Ablesen | 2 |
| 17 | Asset-Integration | Store-Assets + KI-generierte Inhalte (Fabrikhalle, Maschinenmodelle), Placeholder ersetzen | 3 |
| 18 | Audio (Basis) | Maschinengeräusche, Alarm-Sound | 1 |
| 19 | QA/Bugfixing (laufend) | Rollierende Qualitätssicherung während Phase 1 | 3 |
| 20 | Puffer | Unvorhergesehenes | 2 |
| | **Summe** | | **~62 PT** (Kapazität 60 PT – PM federt Spitzen ab, siehe Hinweis) |

*Hinweis:* Die Summe liegt leicht über der reinen 3-Entwickler-Kapazität – genau wie in der Vorversion durch PM-Unterstützung (Content-/Datenaufgaben) und Tag 16–19 als Verschiebe-Puffer abgefangen.

### Phase 2 – VR-Vorbereitung, Polish, Puffer (Tag 21–30)

| # | Feature | Beschreibung | Aufwand (PT) |
|---|---|---|---|
| 21 | VR-Interaction-Proof-of-Concept | XR Interaction Toolkit an Interaction-Abstraction anbinden; Navigations-Pfeile als VR-Teleport-Ziele adaptieren | 6 |
| 22 | Performance/WebGL-Optimierung | Draw-Call-Reduktion, Batching, LODs, Ladezeiten | 4 |
| 23 | Polish-Pass | Maschinen-VFX, Alarm-Lichter, Sound-Feinschliff, Fabrikambiente | 6 |
| 24 | Erweiterte QA/Bugfixing | Fehlerfall-Szenarien, Cross-Browser-Test, VR-Grundfunktionstest | 6 |
| 25 | Balancing-Tuning | Prozesszeiten/Toleranzen nach Playtest-Ergebnissen | 3 |
| 26 | Puffer/Kontingenz | Unvorhergesehenes, Release-Vorbereitung | 5 |
| | **Summe** | | **30 PT** (Kapazität 30 PT) |

---

## 2. Team-Rollenzuordnung

| Account | Rolle | Schwerpunkt |
|---|---|---|
| **PM** | Projektmanager (kein Code) | Rezept-/Fehlerfall-Design, Balancing, Asset-Sourcing, Playtesting/QA, Status-Datei-Pflege, Doku |
| **Dev A** | Maschinen & Produktionsstationen | Machine-System (State Machine), 8 Produktionsstationen, Fault-Integration |
| **Dev B** | Daten & Materialfluss | Conveyor-, Sensor-, Recipe-, Product-State-System, Save/Load |
| **Dev C** | Navigation, HMI & Plattform | Pfeil-Navigation, HMI/UI, Plattform-UX, Audio, Tutorial |

---

## 3. Tag-für-Tag-Plan (Tag 1–30)

### Woche 1 – Fundament (Tag 1–5)

| Tag | PM | Dev A | Dev B | Dev C |
|---|---|---|---|---|
| 1 | Backlog, Pizza-Rezept-Daten spezifizieren (Zutaten, Zeiten, Temperaturen), Store-Asset-Recherche | Projektgrundgerüst, `IMachine`-Interface + generische Machine-Basisklasse mit State Machine | `IConveyor`/`ISensor`/`IProductProcessor`-Interfaces, ScriptableObject-Datenschema (Recipe, ProductState) | Szenen-Grundgerüst (Fabrikhalle-Blockout), URP/WebGL-Setup, `IInteractable`/`IInteractor` vorbereiten |
| 2 | Stationen-Reihenfolge/Layout finalisieren, 3–5 Fehlerfälle spezifizieren | Fault/Maintenance-Erweiterung der State Machine, 1. Maschine (Teigmischer) | ConveyorSystem-Grundgerüst, ProductSystem-Zustandskette als Datenmodell | Navigation: Pfeil-Hotspots definieren, Kamera-Übergangslogik |
| 3 | Erste Asset-Charge importieren, HMI-Layout-Konzept skizzieren | 2. Maschine (Portionierer), Machine-Fault-Trigger-Hooks | SensorSystem-Basis (Presence-/Weight-Sensor), Recipe an Teigmischer anbinden | Navigation mit Testszene verbinden, HMI-Grundgerüst (Canvas, Panel-Layout) |
| 4 | Balancing-Ausgangswerte dokumentieren, Playtesting-Setup | 3. Maschine (Formanlage), Übergabe-Logik über Conveyor | Temperature-/Level-Sensor, ProductSystem an Portionierer/Formanlage anbinden | HMI-Statusanzeige (Maschinenzustände live) |
| 5 | Wochen-Review, Branches integrieren, Status-Datei-Update, Woche-2-Ziele | 4. Maschine (Dosierstation), Bugfixing Kette 1–4 | Motor-/Jam-Sensor, Conveyor-Blockade-Erkennung | Navigation+HMI-Integrationstest, erste durchgängige Klick-Tour |

### Woche 2 – Kernprozess & Fehler-System (Tag 6–10)

| Tag | PM | Dev A | Dev B | Dev C |
|---|---|---|---|---|
| 6 | Fehlerfall-Szenarien abstimmen, Asset-Integration fortsetzen | 5. Maschine (Ofen) inkl. Temperatur-/Backzeit-Logik | RecipeSystem an alle bisherigen Stationen anbinden | HMI: Temperatur-/Füllstandsanzeige, Fehleranzeige-Platzhalter |
| 7 | Content-Review, Playtest 1 vorbereiten | 6. Maschine (Kühleinheit) mit Temperaturverlauf | FaultSystem-Grundgerüst (Event-basiert) | Tutorial-Grundgerüst (Navigation + HMI-Lesen) |
| 8 | Playtest 1 durchführen, Notizen sammeln | 7. Maschine (Schockfroster) | 3–5 Fehlerfälle im FaultSystem verdrahten | HMI: aktive-Fehler-Anzeige, Alarm-Visualisierung |
| 9 | Playtest-1-Ergebnisse auswerten, Backlog Woche 3 | 8. Maschine (Verpackung) – Kette komplett | QualitySystem (Prüfung → Ausschuss/Ausschleusen) | Produktionsstatistik-Anzeige im HMI |
| 10 | Wochen-Review, Status-Datei-Update, interne Demo vorbereiten | Bugfixing/Integration/Regressionstest komplette Kette | Bugfixing/Integration/Regressionstest komplette Kette | Bugfixing/Integration/Regressionstest komplette Kette |

### Woche 3 – Save/Load, Debug-Tools, Plattform-UX (Tag 11–15)

| Tag | PM | Dev A | Dev B | Dev C |
|---|---|---|---|---|
| 11 | Balancing Iteration 1 nach Playtest, KI-Assets Review | Bugfixing/Feinschliff Maschinenlogik, Code-Cleanup | Save/Load-System (Linie, Maschinen, Statistik als JSON) | Plattform-UX: Loading-Screen, UI-Skalierung |
| 12 | Store-Lizenzcheck, Asset-Feinschliff Fabrikhalle | Maschinen-Feedback-Hooks für VFX/Sound (Polish-Vorbereitung) | Save/Load Edge-Cases (defekter Spielstand, Versionierung) | Navigation-Polish (Pfeil-Feedback, Hover-/Klick-States) |
| 13 | Playtest 2 (Fokus Fehler-/HMI-Verständlichkeit) | Balancing-/Debug-Tools: Fehler manuell auslösbar machen | Save/Load-UI (Save-Slots, Continue-Screen) | Tutorial-Logik an echte Navigation/HMI-Elemente anbinden |
| 14 | Playtest-2-Ergebnisse einarbeiten, Architektur-Doku aktualisieren | Debug-Menü erweitern (Sensor-Werte live einsehen/überschreiben) | Balancing-Werte-Iteration (Prozesszeiten/Toleranzen) | HMI-Polish (Icons, Fonts, industrielles Layout) |
| 15 | Review, Status-Datei-Update, Restarbeiten Woche 4 definieren | Integrationstag: Meilenstein-Build erzeugen | Integrationstag: Meilenstein-Build erzeugen | Integrationstag: Meilenstein-Build erzeugen |

### Woche 4 – Stabilisierung & MVP-Fertigstellung (Tag 16–20)

| Tag | PM | Dev A | Dev B | Dev C |
|---|---|---|---|---|
| 16 | Regressionstest, Bug-Liste priorisieren | Bugfixing Machine-/Fault-System, Code-Cleanup (MS-Konventionen) | Bugfixing Conveyor/Sensor/Save-Load, Code-Cleanup | Bugfixing Navigation/HMI/Audio, Code-Cleanup |
| 17 | Bug-Triage, Re-Test | Team-Bug-Bash nach Priorität | Team-Bug-Bash nach Priorität | Team-Bug-Bash nach Priorität |
| 18 | Asset-Lizenz-Check final, Doku finalisieren | Finaler Balancing-Pass (Zeiten/Toleranzen) | Finaler Save/Load-Stresstest | Finaler WebGL-Build-Test (Chrome/Firefox/Edge) |
| 19 | Release-Notes/MVP-Zusammenfassung, finaler interner Playtest | Polishing-Pass | Polishing-Pass | Polishing-Pass |
| 20 | **MVP-Abnahme-Review**, Status-Datei-Update, Kickoff Woche 5 vorbereiten | Finaler Build, kritische Fixes | Finaler Build, kritische Fixes | WebGL-Deployment-Test (Hosting-Check) |

### Woche 5 – VR-Proof-of-Concept & Performance (Tag 21–25)

| Tag | PM | Dev A | Dev B | Dev C |
|---|---|---|---|---|
| 21 | VR-Testgerät/Setup organisieren, VR-Testplan entwerfen | XR Interaction Toolkit einbinden, Define-Symbol `PLATFORM_VR`, Struktur für parallele XR-Bindings | Analyse Save/Load-Kompatibilität für VR-Build | Performance-Baseline messen (Profiler, Draw Calls) |
| 22 | Balancing-Feedback aus MVP-Playtest sammeln | XR-Bindings für `IInteractable`/`IInteractor` (HMI-Buttons in VR bedienbar) | Navigations-Pfeile als VR-Teleport-Ziele adaptieren | Draw-Call-Reduktion (Batching, Material-Konsolidierung) |
| 23 | VR-Testplan verfeinern, interne VR-Testrunde vorbereiten | XR-Interaktion mit Machine-System testen | Save/Load-Kompatibilität Desktop/VR sicherstellen | LOD-Setup Fabrikhalle, Post-Processing-Anpassung |
| 24 | Interne VR-Testrunde durchführen, Notizen sammeln | Bugfixing XR-Interaktionen | Teleport-/Interaktions-Feintuning nach Test | WebGL-Ladezeiten optimieren |
| 25 | Wochen-Review, Status-Datei-Update, VR-PoC-Ergebnisse dokumentieren | Integrationstag: VR-PoC-Build erzeugen | Regressionstest Desktop-Version | Regressionstest Desktop-Version |

### Woche 6 – Polish, Balancing, Puffer, Abschluss (Tag 26–30)

| Tag | PM | Dev A | Dev B | Dev C |
|---|---|---|---|---|
| 26 | Balancing final abstimmen, Restfeedback konsolidieren | Balancing-Tuning Produktionsprozess nach VR-PoC/Playtest | Polish Save/Load-UX (Save-Slot-Anzeige, Fehlermeldungen) | Visuelles Polish (Maschinen-VFX, Alarm-Lichter, Fabrikambiente) |
| 27 | Asset-Lizenzen final querprüfen, Doku-Update | Polish Maschinen-Feedback (Sound + VFX bei Fertigstellung/Fault) | Erweiterte QA: Fehlerfall-/Save-Load-Stresstest | Cross-Browser-Test (Chrome/Firefox/Edge), UX-Detailfixes |
| 28 | Bug-Triage, Re-Test-Koordination | Team-Bug-Bash: VR-Grundfunktion + Desktop-Regression | Team-Bug-Bash: VR-Grundfunktion + Desktop-Regression | Team-Bug-Bash: VR-Grundfunktion + Desktop-Regression |
| 29 | Release-Notes finalisieren, letzter interner Playtest (Desktop + VR-PoC) | Restliche Bugfixes nach Priorität | Restliche Bugfixes nach Priorität | Letzte Performance-Politur |
| 30 | **Finale Abnahme**, Status-Datei-Abschluss-Update, Übergabe-Zusammenfassung | Finaler WebGL-Build, kritische Fixes | Finaler WebGL-Build, kritische Fixes | Deployment-Test (Hosting-Check), Projektdoku-Übergabe |

---

## 4. AI-Nutzung im Tagesverlauf (Free-Plan, 4 Accounts, nur Chat)

Da alle 4 Accounts auf dem Free-Plan ohne Claude Code/API laufen, ist jede Interaktion manuelles Copy-Paste in Unity – die Nachrichten-Ökonomie zählt mehr als die reine Anzahl.

- **Getrennte Chats pro Rolle:** Jeder Account nutzt ausschließlich seinen eigenen Chat für seine Rolle. Keine gemischten Anfragen – das minimiert Kontext-Overhead pro Nachricht.
- **Status-Datei zuerst einfügen:** Da kein Gedächtnis zwischen Sitzungen besteht (neuer Chat pro Arbeitstag), spart das Einfügen der Status-Datei (Abschnitt 6) am Anfang jedes Chats Rückfragen – und damit Nachrichten.
- **Große, klar spezifizierte Anfragen bevorzugen:** Ein vollständig spezifiziertes Feature/eine Klasse auf einmal anfragen statt viele kleine Trial-and-Error-Nachrichten.
- **5-Stunden-Fenster ausnutzen:** Das Limit rolliert ab der ersten Nachricht des Fensters (kein fester Tagesreset). Arbeitstag in zwei bewusste AI-Blöcke (vormittags/nachmittags) einteilen statt gleichmäßig zu verteilen.
- **Peak-Zeiten meiden, wo möglich:** Bei hoher Serverlast wird der Free-Plan stärker gedrosselt.
- **PM-Account als Puffer:** Der PM braucht i. d. R. weniger große Codeblöcke (eher Recherche/Text/Asset-Kuratierung) und kann als Kontingent-Reserve dienen.
- **Eskalationspfad:** Reicht das Budget nicht, ist ein Umstieg auf Pro (~20 $/Monat/Account) eine Option – Community-Schätzungen gehen von grob 5× höherem Kontingent aus (keine offizielle Zahl).
- **Unsicherheit einplanen:** Die genannten Limit-Größenordnungen sind inoffizielle Beobachtungen und können sich ändern.

---

## 5. Architektur-Konventionen (verbindlich für alle Accounts)

- **Sprache:** Sämtlicher Code, Kommentare und Bezeichner ausschließlich auf Englisch.
- **Naming (Microsoft-Konvention):**
  - `PascalCase` für Klassen, Structs, Interfaces (mit `I`-Präfix), Methoden, Properties, Events, öffentliche Felder.
  - `camelCase` für Parameter und lokale Variablen.
  - `_camelCase` für private Felder (Präfix mit Unterstrich).
  - Keine Hungarian Notation.
  - XML-Doc-Kommentare (`///`) auf Englisch für öffentliche APIs.
- **Kern-Interfaces (aus dem Design-Dokument):** `IMachine`, `ISensor`, `IProductProcessor`, `IConveyor`, `IQualityCheck` – zusätzlich `IInteractable`/`IInteractor` für Navigation und HMI-Bedienung.
- **Struktur:** Assembly Definitions je Modul, z. B. `Game.Core` (Interfaces, Interaction-Layer, Navigation), `Game.Production` (MachineSystem, ConveyorSystem, RecipeSystem, ProductSystem, die 8 Stationen), `Game.Sensors` (SensorSystem), `Game.Quality` (QualitySystem, FaultSystem), `Game.HMI` (UI/Bedienpanel), `Game.Platform` (WebGL, Save/Load, Debug-Tools).
- **Interaction-Abstraction:** Kein Code darf direkt gegen Desktop-Input oder XR-Input koppeln. Navigations-Pfeile und HMI-Bedienelemente laufen über `IInteractable`/`IInteractor`, damit Woche 5 die VR-Teleport-/Grab-Bindings ohne Redesign andocken kann. **`IInteractable` sitzt nicht auf der Maschine als Ganzes**, sondern auf einzelnen Maschinenteilen (z. B. Bedienknöpfen, Wartungszugängen) und auf HMI-Elementen – die Maschine selbst (`MachineBase`) bleibt ohne `InteractableBase`.
- **Assembly-Abhängigkeitsrichtung (verbindlich):** `Game.Core → Game.Sensors → Game.Production → Game.Quality → Game.HMI` – jede Assembly referenziert nur "nach unten", nie zurück. Gemeinsam benötigte Typen wandern in die niedrigere Schicht (`Game.Core`), statt eine Rückreferenz einzuführen.
- **Produktidentität auf physischen Objekten:** `ProductToken` (Bindeglied physisches Objekt ↔ `ProductInstance`) liegt in `Game.Core`, nicht in `Game.Production` – dadurch bleibt `Game.Sensors` unabhängig von `Game.Production` (kein Zirkelbezug). Sensoren liefern ausschließlich rohe, produktneutrale Messwerte; die Korrelation von Messwert und Produktidentität (`ProductToken` vom aktuellen GameObject an der Station holen) übernimmt die jeweilige Station in `Game.Production`, nicht der Sensor selbst.
- **Maschinen-Zustandsautomat:** Einheitlich `Idle → Starting → Running → Stopping → Stopped`, bei Störung `Fault → Maintenance → Running`, implementiert in einer generischen Basisklasse, von der alle 8 Stationen erben. Rückweg aus `Stopped`/`Fault`/`Maintenance` läuft **immer** über explizite Bediener-/HMI-Aktion (`ResetToIdle()`, `AcknowledgeFault()`, `CompleteMaintenance()`) – kein Selbst-Reset.
- **Naming-Sonderregel `IMachine`:** Keine Unity-Magic-Method-Namen in der API (z. B. `Start` → `StartMachine()`), da diese sonst mit dem MonoBehaviour-Lifecycle kollidieren – vor jeder neuen Interface-Methode gegenprüfen.
- **Produktzustand:** `RawDough → MixedDough → PortionedDough → FormedPizza → SaucedPizza → ToppedPizza → BakedPizza → CooledPizza → FrozenPizza → PackagedPizza` als zentrales Datenmodell (`IProductProcessor`), das jede Station validiert und weiterreicht.
- **Conveyor-Datenmodell:** `IConveyor` transportiert ausschließlich die physische Last (`GameObject`), **keine** `ProductInstance` direkt – die Produktidentität wird über `ProductToken` (Bindeglied physisches Objekt ↔ `ProductInstance`) und stationsseitige Sensoren aufgelöst.
- **Fehlerzustand vs. Normalbetrieb bei Förderbändern:** `IsJammed` (physischer Defekt, Fault-relevant) ist strikt von `IsBackedUp` (normale Rückstauung durch eine gestoppte Folgestation, kein Fehler) zu trennen, damit das FaultSystem nicht bei jedem normalen Maschinenstopp fälschlich einen Fehler meldet.
- **Komponenten-Komposition statt Mehrfachvererbung:** Da C# keine Mehrfachvererbung erlaubt und `MachineBase`/`InteractableBase` beide eigene MonoBehaviour-Basisklassen sind, gilt generell: Interaktive Elemente werden als **separate Komponente auf einem eigenen (Kind-)GameObject** eingebunden, nie als gemeinsame Basisklasse mit `MachineBase`. Konkret: einzelne Maschinenteile (Bedienknöpfe, Wartungszugänge) und HMI-Elemente tragen `InteractableBase`, die übergeordnete Maschine (`MachineBase`) nicht.
- **Input:** Ausschließlich das neue Unity Input System (`com.unity.inputsystem`), Active Input Handling = "Input System Package (New)"/"Both" – kein Legacy `UnityEngine.Input` in neuem Code.
- **Daten:** Rezepte, Prozessparameter und Toleranzen als ScriptableObjects, nicht hartkodiert – Grundlage für die Balancing-/Debug-Tools.
- **Save-Format:** JSON, versioniert (Schema-Version-Feld im Root-Objekt) für spätere Migrationssicherheit.

---

## 6. Vorlage: Projekt-Status-Datei (zu Beginn jedes neuen Chats einfügen)

```markdown
# PROJECT STATUS – Food Production Simulator (Tiefkühlpizza-Linie)

## Rolle in diesem Chat
[PM / Dev A / Dev B / Dev C] – Schwerpunkt: [siehe Abschnitt 2 im Projektplan]

## Architektur-Konventionen (verbindlich)
- Sprache: Englisch für Code/Kommentare/Bezeichner
- Naming: PascalCase (Klassen/Methoden/Properties/Events), camelCase (Parameter/Locals), _camelCase (private Felder)
- Kern-Interfaces: IMachine, ISensor, IProductProcessor, IConveyor, IQualityCheck, IInteractable/IInteractor
- Assembly-Struktur: Game.Core, Game.Production, Game.Sensors, Game.Quality, Game.HMI, Game.Platform
- Machine-State-Machine: Idle/Starting/Running/Stopping/Stopped, bei Fehler Fault/Maintenance/Running
- Rückweg aus Stopped/Fault/Maintenance immer über explizite Bediener-/HMI-Aktion (ResetToIdle/AcknowledgeFault/CompleteMaintenance) - kein Selbst-Reset
- IMachine-API ohne Unity-Magic-Method-Namen (Start -> StartMachine())
- Produktzustand: RawDough -> ... -> PackagedPizza (zentrales Datenmodell)
- IConveyor transportiert nur physische Last (GameObject), keine ProductInstance - Produktidentität über ProductToken + Sensoren
- IsJammed (Fault) getrennt von IsBackedUp (normale Rückstauung, kein Fehler)
- Komposition statt Mehrfachvererbung: interaktive Elemente als separate Komponente auf eigenem (Kind-)GameObject, nie gemeinsame Basisklasse mit MachineBase
- IInteractable sitzt auf Maschinenteilen (Knöpfe, Wartungszugänge) und HMI-Elementen, nicht auf der Maschine als Ganzes
- Assembly-Abhängigkeitsrichtung: Game.Core -> Game.Sensors -> Game.Production -> Game.Quality -> Game.HMI, nur "nach unten" referenzieren
- ProductToken liegt in Game.Core (nicht Game.Production) - Sensoren liefern nur rohe Messwerte, Stationen korrelieren Messwert+Produktidentität selbst
- Input: nur neues Input System (com.unity.inputsystem), kein Legacy UnityEngine.Input
- Navigation: Punkt-zu-Punkt via interaktiver Pfeile, kein freies Movement
- Content als ScriptableObjects, keine Hardcoded-Werte
- Save-Format: JSON, versioniert

## Aktueller Plan-Tag
Tag [X] von 30 – Woche [Y]
Heutiges Ziel laut Plan: [aus Abschnitt 3 des Projektplans einfügen]

## Fertige Features
- [Liste der abgeschlossenen Features/Systeme]

## In Arbeit
- [Feature]: [kurzer Zwischenstand, letzter bekannter Punkt]

## Bekannte Probleme / offene Bugs
- [Bug/Problem]: [Priorität, Reproduktionsschritte falls bekannt]

## Referenz: Tag-für-Tag-Plan (Kurzfassung)
Woche 1 (T1-5): Fundament – Setup, Interfaces, Machine-State-Machine, Navigation-Pfeile, erste 4 Maschinen
Woche 2 (T6-10): Restliche 4 Maschinen, FaultSystem mit 3-5 Fehlerfällen, QualitySystem, HMI-Statistik
Woche 3 (T11-15): Save/Load, Debug-/Balancing-Tools, Plattform-UX, Meilenstein-Build
Woche 4 (T16-20): Stabilisierung, Bugfixing, MVP-Abnahme
Woche 5 (T21-25): VR-Proof-of-Concept (XR-Bindings, Pfeile als Teleport-Ziele), Performance-Optimierung
Woche 6 (T26-30): Polish, erweiterte QA, Balancing-Tuning, Release

## Heutige Aufgabe (konkret)
[Was genau soll in diesem Chat erarbeitet werden]
```

---

## 7. Kurz-Zusammenfassung der Annahmen (zum Gegenprüfen)

- 6 Wochen = 30 Arbeitstage (Mo–Fr), vollständiger Tag-für-Tag-Plan über alle 30 Tage: Kernproduktion (Tag 1–20) + VR-Vorbereitung/Polish/Puffer (Tag 21–30).
- Fokus liegt gemäß Design-Dokument ausschließlich auf einer Tiefkühlpizza-Linie mit einem Rezept (Margherita) – Mehrfach-Rezepte, mehrere Linien, Wartung, Energieverbrauch, Lagerverwaltung, Schichtbetrieb und wirtschaftliche Simulation sind explizit spätere Erweiterungen (siehe Design-Dokument Abschnitt 10) und nicht Teil dieses Plans.
- Bewegung ausschließlich über klickbare Navigations-Pfeile (Hotspot-Konzept) – kein freier Player-Controller. Dies reduziert Aufwand in Phase 1 und vereinfacht die spätere VR-Teleport-Anbindung in Woche 5.
- PM trägt keine PT-Last in der Feature-Tabelle, arbeitet aber vollzeitig projektbegleitend (Content, Balancing, Assets, QA, Doku).
- Free-Plan-Limits sind inoffizielle Schätzwerte – Budget-Hinweise entsprechend konservativ und als Heuristik zu verstehen, nicht als exakte Kennzahl.
- „VR-erweiterbar" bedeutet hier: sauber abstrahierte Interaction-/Navigation-Schicht + ein validierender Proof-of-Concept in Woche 5, kein vollständiges VR-Feature-Set.
