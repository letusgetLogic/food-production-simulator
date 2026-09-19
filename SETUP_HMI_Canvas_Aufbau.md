# HMI-Szenenaufbau (Tag 4, v3 – Ambient-Display, Windowed-Popup, Tablet-Auswahl)

Löst die vorherige Fassung ab. Neu gegenüber v2: World-Space-Ambient-Anzeige
pro Maschine, Popup ist nicht mehr fullscreen, Tablet bekommt eine eigene
Maschinen-Auswahlliste statt direkter Bindung.

## 0. Vorbemerkung

Drei Komponenten sind heute neu entstanden, weil die UX-Entscheidung dazu
gerade erst gefallen ist – nicht Teil eurer bereits aktualisierten Skripte:
`MachineAmbientStatusBinder`, `MachineSelectListView`/`MachineSelectRowView`,
und eine Erweiterung von `MachineDetailPanel` um `SetControlEnabled(bool)`.

## 1. MachineOverviewChannel-Asset + MachineOverviewReporter

Unverändert zu vorher: Channel-Asset einmalig anlegen, `MachineOverviewReporter`
auf einem neutralen Szenen-Objekt (z. B. `Systems`), Channel-Referenz reinziehen.

## 2. Pro Maschine: World-Space-Ambient-Anzeige (immer sichtbar)

```
MachineTerminal_<n>            Collider (Layer: Interactable)
                               HmiTerminalInteractable
                                 -> Screen  = <Popup-Canvas, siehe Schritt 3>
                                 -> Binder  = <MachineDetailBinder im Popup>
                                 -> Machine = <MachineBase der Maschine>
                                 -> Panel Id = "machine"
├── AmbientCanvas               Canvas (Render Mode: World Space)
│                               RectTransform: klein, am Terminal-Objekt in der
│                                 Welt positioniert (z. B. 20x10cm), Sortierung
│                                 egal, da nur aus der Nähe sichtbar
│                               CanvasScaler (World Space: feste Skala, kein
│                                 Scale-With-Screen-Size)
│                               GraphicRaycaster (optional – nur nötig, falls
│                                 die Ambient-Anzeige selbst klickbar sein soll,
│                                 aktuell nicht der Fall)
│   └── AmbientRow              MachineStateRow-Prefab (Lampe + Name + Zustand)
│         MachineAmbientStatusBinder
│           -> Machine = dieselbe MachineBase wie oben am Terminal
│           -> Row     = AmbientRow
```

Diese Anzeige läuft komplett unabhängig vom `HmiScreenController`/Popup –
sie ist immer aktiv, sobald die Maschine existiert, nicht erst nach Interact.

## 3. Popup-Canvas (Windowed, nicht fullscreen) – EIN Prefab-Typ für Terminal + Tablet

```
HMI_Popup                      Canvas (Screen Space - Overlay, Sort Order 10)
                               CanvasScaler (Scale With Screen Size, Referenz
                                 ~1280x800, Match 0.5)
                               GraphicRaycaster
                               HmiScreenController
└── ScreenRoot                 (Start: inaktiv)
    RectTransform: NICHT auf volle Canvas-Größe strecken – z. B. Anchor Min
      (0.15, 0.1) / Anchor Max (0.85, 0.9), sodass ringsum Spielwelt sichtbar
      bleibt. Kein blickdichter Vollbild-Hintergrund; höchstens ein leicht
      transparentes Panel-Background hinter ScreenRoot selbst (z. B. Alpha 0.85),
      nicht über den gesamten Screen.
    ├── Header / NavRail       (wie gehabt)
    └── Content
        ├── Panel_Overview     CanvasGroup + OverviewPanel   (Panel Id "overview")
        │     -> Channel = MachineOverviewChannel.asset
        │     -> Row Template / Row Container  (dynamisch, siehe v2)
        ├── Panel_Machine      CanvasGroup + MachineDetailPanel (Panel Id "machine")
        │     -> Command Row  = CommandRow-Objekt (für SetControlEnabled)
        │   MachineDetailBinder (co-located)
        │     -> Detail Panel = dieses Panel_Machine
        └── Panel_Recipe       (unverändert)
```

Dieses eine Prefab instanzierst du zweimal in der Szene – einmal als
Terminal-Popup, einmal als Tablet-UI. Beide sehen gleich aus (windowed,
Umgebung bleibt sichtbar), unterscheiden sich nur in der Verdrahtung:

### 3a. Terminal-Instanz

```
HMI_Popup_Terminal              (Instanz von HMI_Popup)
    Panel_Machine -> MachineDetailBinder -> SetControlEnabled(true) beim Bind
```

Jedes `MachineTerminal_<n>` aus Schritt 2 zeigt in seinem
`HmiTerminalInteractable` auf `HMI_Popup_Terminal` und dessen
`MachineDetailBinder`. Volle Bedienung, da direkt an der Maschine.

### 3b. Tablet-Instanz

```
HMI_Popup_Tablet                (Instanz von HMI_Popup, z. B. am Player-Prefab
                                  oder separat per Tastendruck geöffnet)
    Panel_Machine
        -> MachineDetailBinder -> SetControlEnabled(false) beim Bind (read-only!)
        -> MachineSelectListView
              -> Binder        = derselbe MachineDetailBinder wie oben
              -> Row Template  = MachineSelectRow-Prefab (Name + Button, NICHT
                                  dasselbe Prefab wie MachineStateRow – eigene,
                                  klickbare Variante)
              -> Row Container = leerer Container, füllt sich in Awake() selbst
```

NavRail-Klick auf „Maschine" zeigt also nicht direkt Details, sondern die
`MachineSelectListView` (Liste aller Maschinen zum Antippen). Erst nach Klick
auf eine Zeile bindet `MachineDetailBinder.Bind(machine)` – und weil
`SetControlEnabled(false)` gesetzt ist, bleibt `CommandRow` dabei unsichtbar,
selbst wenn der Bediener eigentlich alle Übergangsbedingungen erfüllen würde.

**Bestätigt: Liste und Details stehen nebeneinander** (Master-Detail), nicht als
Zwei-Schritt-Flow. `MachineSelectListView` bleibt dafür wie oben gebaut – sie
blendet sich beim Klick nicht aus, sondern bleibt permanent sichtbar neben dem
Detailbereich. Praktisch heißt das für `Panel_Machine` auf dem Tablet:

```
Panel_Machine (Tablet)         Horizontal Layout Group
├── MachineSelectPane          feste/relative Breite (~30-35%)
│     MachineSelectListView    (wie in Schritt 3b beschrieben)
└── MachineDetailPane          restliche Breite
      MachineDetailPanel + MachineDetailBinder (SetControlEnabled(false))
```

Auf dem Terminal-Popup bleibt `Panel_Machine` dagegen einspaltig, volle Breite
für Details + Bedienknöpfe – dort existiert keine `MachineSelectListView`, da
die Maschine ja schon durch das jeweilige Terminal feststeht.

## 4. Rezept-Säulen

Unverändert:
```
RecipePillar_<n>               Collider (Layer: Interactable)
                               RecipeTerminalInteractable
                                 -> Screen = HMI_Popup_Terminal/HmiScreenController
                                 -> Panel Id = "recipe"
```

## 5. Prefabs (Übersicht)

- `StatusTile`, `MachineStateRow`, `AlarmEntry`, `RecipeEntryRow` – wie in v2.
- NEU: `MachineSelectRow` (Name-Text + Button, nur fürs Tablet).
- `MachineStateRow` wird jetzt an ZWEI Stellen als Template gebraucht: einmal
  in `Panel_Overview` (Row Template), einmal in jeder `AmbientCanvas` (direkt
  platziert, nicht als Pool-Template, da pro Maschine nur eine Zeile).

## 6. Testreihenfolge

1. Ambient-Anzeigen: Play Mode, an eine Maschine herangehen – Lampe/Zustand
   müssen sofort sichtbar sein, auch ohne jede Interaktion.
2. Terminal anvisieren, interagieren – `HMI_Popup_Terminal` öffnet sich
   windowed, Spielwelt bleibt drumherum sichtbar, Bedienknöpfe sind aktiv.
3. Tablet öffnen (eigener Trigger) – `HMI_Popup_Tablet` zeigt beim Wechsel auf
   „Maschine" zunächst die Auswahlliste, nicht direkt Details.
4. Eine Maschine in der Tablet-Liste antippen – Detailwerte erscheinen, aber
   KEIN Bedienknopf ist sichtbar/klickbar, unabhängig vom Maschinenzustand.
5. Parallel: Terminal-Popup an Maschine A offen (voll bedienbar), Tablet zeigt
   gleichzeitig Maschine B im Detail (read-only) – beide dürfen sich nicht
   gegenseitig beeinflussen.
