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
| Bewegung/Navigation | Freies 3D-Movement (First-Person) mit Aim-Point-Interaktion (Entscheidung Tag 3; Pfeil-Navigation verworfen) |
| Assets | Placeholder (Start) → Store-Assets + KI-generierte Inhalte (laufend ersetzt) |
| Zusatzfeatures | Save/Load, plattformspezifisches UX, Balancing-/Debug-Tools, Tutorial/Onboarding |
| AI-Plan | Alle 4 Accounts: Claude Free, nur Chat (claude.ai), kein Claude Code, kein API-Zugang |
| Gesamtdauer | 6 Wochen = 30 Arbeitstage, voller Tag-für-Tag-Plan über alle 30 Tage: Tag 1–20 Kernproduktion, Tag 21–30 VR-PoC/Polish/Puffer |

**Änderungen gegenüber der Vorversion:** Das Design-Dokument ersetzt das generische "Ketten/Economy/Progression"-Konzept durch eine spezifische industrielle Produktionslinie mit Maschinen-Zustandsautomaten, Sensorik, Rezept-/Produkt-Zustandsmodell und Fehlersystem. Economy- und Freischalt-Progression entfallen; an ihre Stelle tritt die Produktionsstatistik (produzierte Einheiten/Ausschuss) aus dem HMI. Die Bewegung erfolgt über klickbare Navigations-Pfeile statt freier Spielerbewegung – das reduziert den Aufwand für den Player-Controller und passt architektonisch gut zur späteren VR-Teleport-Navigation (Wiederverwendung des gleichen Hotspot-Konzepts).

**Hinweis zu AI-Limits:** unverändert gegenüber der Vorversion – siehe Abschnitt 4.

**Änderungen im Projektverlauf (Stand 01.10.2026):**
- **Bewegung:** Pfeil-Navigation mit Kamera-Übergängen verworfen → freies First-Person-Movement + Aim-Interaktion (Dev C, Tag 3). Woche-5-VR-Plan entsprechend auf Teleport/Locomotion für freies Movement umgestellt.
- **Assemblies:** `Game.Sensors` entfällt (Zyklus mit `Game.Production`); Sensoren und Produkt-Typen (`ProductToken` etc.) liegen in `Game.Production`. Neu: `Game.Editor` (nur Editor-Tools).
- **Materialfluss:** `IConveyor`/`ILoadReceiver`/Slot-Conveyor entfallen. Förderbänder sind `ConveyorBelt : MachineBase` (melden Störungen ans HMI), Produkte werden physikalisch (Rigidbody) transportiert. Bänder laufen kontinuierlich; vor taktenden Maschinen sitzt ein Puffer-Band mit Einzelplatz-Zonen (`AccumulationZone`, 4 Plätze); ist er voll, pausieren Zulaufband und Portionierer automatisch, bleibt er zu lange voll → Fault. Warnungen (amber) als separates Signal auf `MachineBase`, kein neuer `MachineState`.
- **Nicht jede Strecke ist ein Band:** Mixer → Portionierer per Topf (Arbeiter trägt ihn, Lift kippt ihn in den Trichter).
- **Stationen:** Formanlage heißt im Code `PressMachine`; Mixer, Portionierer und Presse sind Quell-/Physikstationen ohne `IProductProcessor`. Ofen/Kühlung/Froster werden als Durchlaufstationen gebaut (`ContinuousProcessStation`: Verweilzeit = Zonenlänge / Bandgeschwindigkeit).
- **Rezept:** wird nicht an Maschinen gebunden; eigene Rezept-Terminals an Säulen, Bediener stellt Werte manuell ein.
- **Konvention:** ScriptableObject-Klassen mit Präfix `SO_`.
- **Fortschritt 01.10.:** Produktion Mixer → Portionierer → Presse läuft durchgängig bis `FormedPizza`. Offen: Maschinen-Terminals für Portionierer und Presse, danach Dosierstation.

---

## 1. Feature-Liste mit Aufwandsschätzung

Kapazität Kernphase (Tag 1–20): 3 Entwickler × 20 Tage = 60 PT. Kapazität Phase 2 (Tag 21–30): 3 Entwickler × 10 Tage = 30 PT.

### Phase 1 – Kernproduktion (Tag 1–20)

| # | Feature | Beschreibung | Aufwand (PT) |
|---|---|---|---|
| 1 | Projekt-Setup & Architektur | Repo, Unity-Projekt, Assembly Definitions, Coding-Konventionen, Paket-Setup (URP, Input System) | 3 |
| 2 | Interaction-Abstraction-Layer | `IInteractable`/`IInteractor` für Navigations-Pfeile und HMI-Bedienelemente, entkoppelt vom konkreten Input | 2 |
| 3 | Bewegung & Interaktion | Freies First-Person-Movement, Aim-Point-Interaktion, Tragen von Objekten (Topf) – ersetzt die ursprünglich geplante Pfeil-Navigation | 3 |
| 4 | Conveyor-System | `ConveyorBelt` (`MachineBase`): physikalischer Transport, Puffer-Band vor taktenden Maschinen, Linien-Controller, Stau-/Puffer-Fault | 3 |
| 5 | Machine-System | `IMachine` + generische State Machine (Idle/Starting/Running/Stopping/Stopped, Fault/Maintenance) | 5 |
| 6 | Sensor-System | `ISensor`: Presence-, Weight-, Temperature-, Level-, Pot-Sensor (Jam-Erkennung im Band selbst) | 3 |
| 7 | Recipe-System | ScriptableObject-Rezeptdefinition (Margherita: Mengen, Zeiten, Temperaturen) | 3 |
| 8 | Product-State-System | `IProductProcessor`: Zustandskette RawDough → … → PackagedPizza | 4 |
| 9 | 8 Produktionsstationen | Teigmischer, Portionierer, Formanlage (Presse), Dosierstation, Ofen, Kühleinheit, Schockfroster, Verpackung als `MachineBase`-Implementierungen (Ofen/Kühlung/Froster als Durchlaufstationen) – Stand 01.10.: Teigmischer, Portionierer, Presse fertig | 7 |
| 10 | Fault-System | 3–5 Fehlerfälle: Material fehlt, Förderband blockiert, Ofentemp. zu niedrig, falsches Gewicht, Sensorfehler | 3 |
| 11 | Quality-Check | `IQualityCheck`: Gewichts-/Temperaturprüfung → Ausschuss/Ausschleusen | 1 |
| 12 | HMI/UI-System | Industrielles Bedienpanel: Status, Maschinenzustände, Temperaturen, Füllstände, Produktionsgeschwindigkeit, produzierte Einheiten, Ausschuss, aktive Fehler | 5 |
| 13 | Save/Load-System | JSON-Serialisierung von Linien-, Maschinen- und Statistikzustand | 3 |
| 14 | Balancing-/Debug-Tools | Editor-Tuning via ScriptableObjects, Debug-Menü (Fehler/Sensorwerte manuell auslösen) | 3 |
| 15 | Plattform-UX (Browser) | WebGL-Build-Konfiguration, Loading-Screen, UI-Skalierung | 3 |
| 16 | Tutorial/Onboarding | Einführung in Bewegung/Interaktion und HMI-Ablesen | 2 |
| 17 | Asset-Integration | Store-Assets + KI-generierte Inhalte (Fabrikhalle, Maschinenmodelle), Placeholder ersetzen | 3 |
| 18 | Audio (Basis) | Maschinengeräusche, Alarm-Sound | 1 |
| 19 | QA/Bugfixing (laufend) | Rollierende Qualitätssicherung während Phase 1 | 3 |
| 20 | Puffer | Unvorhergesehenes | 2 |
| | **Summe** | | **~62 PT** (Kapazität 60 PT – PM federt Spitzen ab, siehe Hinweis) |

*Hinweis:* Die Summe liegt leicht über der reinen 3-Entwickler-Kapazität – genau wie in der Vorversion durch PM-Unterstützung (Content-/Datenaufgaben) und Tag 16–19 als Verschiebe-Puffer abgefangen.

### Phase 2 – VR-Vorbereitung, Polish, Puffer (Tag 21–30)

| # | Feature | Beschreibung | Aufwand (PT) |
|---|---|---|---|
| 21 | VR-Interaction-Proof-of-Concept | XR Interaction Toolkit an Interaction-Abstraction anbinden; VR-Locomotion/Teleport für das freie Movement | 6 |
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
| **Dev B** | Daten & Materialfluss | Förderbänder/Puffer, Sensor-, Recipe-, Product-State-System, Save/Load |
| **Dev C** | Bewegung, HMI & Plattform | First-Person-Movement/Aim-Interaktion, HMI/UI, Plattform-UX, Audio, Tutorial |

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
| 22 | Balancing-Feedback aus MVP-Playtest sammeln | XR-Bindings für `IInteractable`/`IInteractor` (HMI-Buttons in VR bedienbar) | VR-Locomotion/Teleport für das freie Movement adaptieren | Draw-Call-Reduktion (Batching, Material-Konsolidierung) |
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
- **ScriptableObjects:** Klassen mit Präfix `SO_` (z. B. `SO_PressConfig`, `SO_ConveyorConfig`).
- **Kern-Interfaces:** `IMachine`, `ISensor`, `IProductProcessor` (nur für klassische Ein-/Ausgabestationen), `IQualityCheck` – zusätzlich `IInteractable`/`IInteractor` für Interaktion und HMI-Bedienung. `IConveyor` entfällt (Förderbänder sind `MachineBase`).
- **Struktur:** Assembly Definitions je Modul: `Game.Core` (Interaction-Layer, Channels, Lokalisierung), `Game.Production` (Maschinen, Förderbänder/Puffer, Sensoren, Rezept, Produkt-Typen, die 8 Stationen), `Game.Quality` (QualitySystem, FaultSystem), `Game.HMI` (UI/Bedienpanel), `Game.Platform` (Movement, Input, WebGL, später Save/Load, Debug-Tools), `Game.Editor` (nur Editor-Tools, z. B. Linien-Setup).
- **Interaction-Abstraction:** Kein Code darf direkt gegen Desktop-Input oder XR-Input koppeln. Interaktive Objekte und HMI-Bedienelemente laufen über `IInteractable`/`IInteractor`, damit Woche 5 die VR-Grab-/Teleport-Bindings ohne Redesign andocken kann. **`IInteractable` sitzt nicht auf der Maschine als Ganzes**, sondern auf einzelnen Maschinenteilen (z. B. Bedienknöpfen, Wartungszugängen) und auf HMI-Elementen – die Maschine selbst (`MachineBase`) bleibt ohne `InteractableBase`.
- **Assembly-Abhängigkeitsrichtung (verbindlich):** `Game.Production → Game.Core`, `Game.Quality → Core, Production`, `Game.HMI → Core, Production`, `Game.Platform → Core` – nie zurück. Gemeinsam benötigte Typen wandern in die niedrigere Schicht, statt eine Rückreferenz einzuführen; HMI ↔ Platform koppeln nur über ScriptableObject-Channels in `Game.Core`.
- **Produktidentität auf physischen Objekten:** `ProductToken` (Bindeglied physisches Objekt ↔ `ProductInstance`) liegt zusammen mit den Sensoren in `Game.Production`. Sensoren liefern ausschließlich Rohwerte (Anwesenheit, Gewicht, Temperatur, Füllstand); die Bewertung und die Korrelation mit der Produktidentität übernimmt die Station. `PresenceSensor` reagiert standardmäßig nur auf Produkte („Products Only“).
- **Maschinen-Zustandsautomat:** Einheitlich `Ready → Starting → Running → Stopping → Stopped`, bei Störung `Fault → Maintenance → Ready`, implementiert in `MachineBase`, von der alle Stationen und Förderbänder erben. Rückweg aus `Stopped`/`Fault`/`Maintenance` läuft **immer** über explizite Bediener-/HMI-Aktion (`ResetToIdle()`, `AcknowledgeFault()`, `CompleteMaintenance()`) – kein Selbst-Reset. Warnungen (HMI amber) sind ein separates Signal (`SetWarning()`/`WarningChanged`), kein eigener Zustand.
- **Naming-Sonderregel `IMachine`:** Keine Unity-Magic-Method-Namen in der API (aktuell `StartRun()`/`StopRun()`/`RequestRun()`), da diese sonst mit dem MonoBehaviour-Lifecycle kollidieren – vor jeder neuen Interface-Methode gegenprüfen.
- **Produktzustand:** `RawDough → MixedDough → PortionedDough → FormedPizza → SaucedPizza → ToppedPizza → BakedPizza → CooledPizza → FrozenPizza → PackagedPizza` als zentrales Datenmodell (`IProductProcessor`), das jede Station validiert und weiterreicht.
- **Förderbänder:** `ConveyorBelt : MachineBase`. Produkte sind Rigidbodies, das Band setzt deren horizontale Geschwindigkeit (nur für Produkte, deren Mitte über dem Band liegt); Teig liegt nie auf einem Band, das unter ihm durchläuft. Rückstau ist **kein** Fehler (Band pausiert, Warning); Fault nur bei echtem Stau (`Jam`) oder zu lange vollem Puffer (`BufferFull`). Puffer vor taktenden Maschinen über `AccumulationZone` (Einzelplatz-Zonen), Linien-Start/-Stop/-Geschwindigkeit über `ConveyorLineController`.
- **Terminal-Werte (Sollwerte/Ist-Werte):** Maschinen implementieren `IMachineParameterSource` (`Game.Production`) und liefern `MachineParameter` (Sollwert mit Min/Max/Schritt aus ihrem `SO_*Config`) und `MachineReadout` (Ist-Wert mit `MachineValueLevel` für die HMI-Farbe, optional `MachineReadoutSlot` für die festen Kacheln). Das HMI (`MachineParameterListView`) baut die Zeilen zur Laufzeit und kennt keine konkreten Maschinentypen. Rezeptwerte werden weiterhin nicht im Code gebunden – der Bediener stellt sie am Terminal ein.
- **Durchlaufstationen** (Dosierstation, Ofen, Kühlung, Froster): Station besitzt ihr eigenes Band (`_belt`, wird mit der Station gestartet/gestoppt), Verarbeitung beim Durchfahren einer `ProcessZone`; Aufnahmebereitschaft über `IInfeedReadiness`, das vorgelagerte Band wartet. Im `ConveyorLineController` steht die Station, nicht ihr Band.
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
- ScriptableObject-Klassen mit Präfix SO_
- Kern-Interfaces: IMachine, ISensor, IProductProcessor, IQualityCheck, IInteractable/IInteractor (kein IConveyor mehr)
- Assembly-Struktur: Game.Core, Game.Production (inkl. Sensoren/Produkt), Game.Quality, Game.HMI, Game.Platform, Game.Editor
- Machine-State-Machine: Ready/Starting/Running/Stopping/Stopped, bei Fehler Fault/Maintenance/Ready; Warning als separates Signal
- Rückweg aus Stopped/Fault/Maintenance immer über explizite Bediener-/HMI-Aktion (ResetToIdle/AcknowledgeFault/CompleteMaintenance) - kein Selbst-Reset
- IMachine-API ohne Unity-Magic-Method-Namen (StartRun/StopRun/RequestRun)
- Produktzustand: RawDough -> ... -> PackagedPizza (zentrales Datenmodell)
- Förderbänder = ConveyorBelt : MachineBase, physikalischer Transport; Rückstau = Warning, Jam/BufferFull = Fault
- Terminal-Werte über IMachineParameterSource (Sollwerte mit Min/Max/Schritt aus SO_*Config, Ist-Werte mit MachineValueLevel); HMI kennt keine konkreten Maschinentypen
- Durchlaufstationen (Dosierung/Ofen/Kühlung/Froster) besitzen ihr eigenes Band, Verarbeitung per ProcessZone, IInfeedReadiness für Rückstau
- Komposition statt Mehrfachvererbung: interaktive Elemente als separate Komponente auf eigenem (Kind-)GameObject, nie gemeinsame Basisklasse mit MachineBase
- IInteractable sitzt auf Maschinenteilen (Knöpfe, Wartungszugänge) und HMI-Elementen, nicht auf der Maschine als Ganzes
- Assembly-Abhängigkeitsrichtung: Production -> Core; Quality/HMI -> Core, Production; Platform -> Core - nur "nach unten" referenzieren
- ProductToken liegt in Game.Production - Sensoren liefern nur rohe Messwerte, Stationen korrelieren Messwert+Produktidentität selbst
- Input: nur neues Input System (com.unity.inputsystem), kein Legacy UnityEngine.Input
- Bewegung: freies First-Person-Movement mit Aim-Interaktion (keine Pfeil-Navigation)
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
Woche 1 (T1-5): Fundament – Setup, Interfaces, Machine-State-Machine, Bewegung/Interaktion, erste 4 Maschinen
Woche 2 (T6-10): Restliche 4 Maschinen, FaultSystem mit 3-5 Fehlerfällen, QualitySystem, HMI-Statistik
Woche 3 (T11-15): Save/Load, Debug-/Balancing-Tools, Plattform-UX, Meilenstein-Build
Woche 4 (T16-20): Stabilisierung, Bugfixing, MVP-Abnahme
Woche 5 (T21-25): VR-Proof-of-Concept (XR-Bindings, Teleport/Locomotion für freies Movement), Performance-Optimierung
Woche 6 (T26-30): Polish, erweiterte QA, Balancing-Tuning, Release

## Heutige Aufgabe (konkret)
[Was genau soll in diesem Chat erarbeitet werden]
```

---

## 7. Kurz-Zusammenfassung der Annahmen (zum Gegenprüfen)

- 6 Wochen = 30 Arbeitstage (Mo–Fr), vollständiger Tag-für-Tag-Plan über alle 30 Tage: Kernproduktion (Tag 1–20) + VR-Vorbereitung/Polish/Puffer (Tag 21–30).
- Fokus liegt gemäß Design-Dokument ausschließlich auf einer Tiefkühlpizza-Linie mit einem Rezept (Margherita) – Mehrfach-Rezepte, mehrere Linien, Wartung, Energieverbrauch, Lagerverwaltung, Schichtbetrieb und wirtschaftliche Simulation sind explizit spätere Erweiterungen (siehe Design-Dokument Abschnitt 10) und nicht Teil dieses Plans.
- Bewegung über freies First-Person-Movement mit Aim-Interaktion (ursprünglich Pfeil-Navigation, an Tag 3 geändert). Die VR-Anbindung in Woche 5 braucht dafür Locomotion/Teleport statt fester Teleport-Ziele.
- PM trägt keine PT-Last in der Feature-Tabelle, arbeitet aber vollzeitig projektbegleitend (Content, Balancing, Assets, QA, Doku).
- Free-Plan-Limits sind inoffizielle Schätzwerte – Budget-Hinweise entsprechend konservativ und als Heuristik zu verstehen, nicht als exakte Kennzahl.
- „VR-erweiterbar" bedeutet hier: sauber abstrahierte Interaction-/Navigation-Schicht + ein validierender Proof-of-Concept in Woche 5, kein vollständiges VR-Feature-Set.
