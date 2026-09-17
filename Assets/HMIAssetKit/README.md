# HMI Asset-Kit für Unity 6

Ein wiederverwendbares UI-Toolkit für eine industrielle Produktionssimulation. Das Design orientiert sich an den HMI-Mockups **Linienübersicht** und **Maschinendetailansicht**.

## Inhalt

- `Runtime/UI/HMIOverview.uxml` – Linienübersicht
- `Runtime/UI/HMIMachineDetail.uxml` – Maschinendetailansicht
- `Runtime/UI/HMITheme.uss` – globale Farben und Layoutregeln
- `Runtime/UI/HMIComponents.uss` – wiederverwendbare UI-Komponenten
- `Runtime/Scripts/HMITheme.cs` – ScriptableObject-Farbsystem
- `Runtime/Scripts/HMIThemeInstaller.cs` – Theme-Werte auf USS-Variablen anwenden
- `Runtime/Scripts/HMIProductionModel.cs` – Beispielmodell für Maschinen und Alarme
- `Runtime/Scripts/HMIOverviewController.cs` – Controller der Linienübersicht
- `Runtime/Scripts/HMIMachineDetailController.cs` – Controller der Detailansicht
- `Runtime/Resources/HMITheme.asset` – Beispiel-Theme

## Installation

1. Entpacke das Archiv.
2. Kopiere den Ordner `HMIAssetKit` vollständig nach `Assets/` in dein Unity-6-Projekt.
3. Öffne oder erstelle eine Szene.
4. Erstelle ein leeres GameObject, z. B. `HMI_Root`.
5. Füge die Komponente `UIDocument` hinzu.
6. Ziehe `Runtime/UI/HMIOverview.uxml` in das Feld **Visual Tree Asset**.
7. Füge `HMIOverviewController` und `HMIThemeInstaller` am selben GameObject hinzu.
8. Weise bei Bedarf `Runtime/Resources/HMITheme.asset` im Theme-Feld zu.
9. Starte die Szene.

## Maschinendetailansicht

1. Erstelle ein zweites GameObject mit `UIDocument`.
2. Weise `Runtime/UI/HMIMachineDetail.uxml` als Visual Tree Asset zu.
3. Füge `HMIMachineDetailController` hinzu.
4. Weise eine Maschine aus `HMIProductionModel` zu oder setze sie im Inspector.

Für eine Umschaltung zwischen Übersicht und Detailansicht können beide `UIDocument`-Komponenten über `GameObject.SetActive` aktiviert bzw. deaktiviert werden.

## Theme verwenden

`HMITheme` ist ein ScriptableObject. Lege bei Bedarf über den Project-Browser ein eigenes Theme an:

**Create → HMI → Theme**

Die wichtigsten Farben sind:

- Primary / Running: Grün
- Warning: Gelb/Orange
- Error / Fault: Rot
- Idle / Neutral: Graublau
- Surface: dunkle Panel-Farbe
- Text: helles Grau

Das Theme wird über `HMIThemeInstaller` in USS-Custom-Properties übertragen. Dadurch können mehrere HMI-Dokumente dieselben Farben verwenden.

## Produktionsdaten anschließen

Das Beispielmodell enthält Maschinenstatus, Messwerte und Alarme. Für die eigentliche Simulation ersetzt oder erweitert du das Modell durch deine Produktionslogik.

Empfohlener Ablauf:

1. Die Maschinenlogik aktualisiert Temperatur, Füllstand, Zykluszeit und Status.
2. Das Modell löst bei Änderungen ein Update-Event aus.
3. Der jeweilige UI-Controller liest die Werte aus dem Modell.
4. Die UI aktualisiert nur die betroffenen Labels und Statusklassen.

Die Beispielmaschinen sind:

- Teigmischer
- Portionierer
- Former
- Dosierstation

## Eigene Maschine ergänzen

1. Erstelle eine neue Maschine im Datenmodell.
2. Vergib eine eindeutige ID und einen Anzeigenamen.
3. Setze den Startstatus auf `Idle`, `Running`, `Warning` oder `Fault`.
4. Ergänze die Messwerte und Grenzwerte.
5. Weise die Maschine der Übersicht oder Detailansicht zu.

## Fehler und Bedienung

Ein Alarm sollte mindestens eine Meldung, eine betroffene Maschine und einen Status enthalten. Die Quittierung darf in der vollständigen Simulation erst dann den Fehler entfernen, wenn die zugrunde liegende Ursache behoben wurde.

Die Bedienknöpfe im Kit sind UI-Ausgangspunkte. Die tatsächliche Start-, Stop-, Reset- oder Wartungslogik muss mit deiner Maschinensteuerung verbunden werden.

## Häufige Probleme

### UI bleibt leer

- Prüfe, ob das `Visual Tree Asset` im `UIDocument` gesetzt ist.
- Prüfe, ob die USS-Dateien im UXML referenziert werden.
- Prüfe die Console auf UXML- oder USS-Fehler.

### Theme wird nicht angewendet

- Prüfe, ob `HMIThemeInstaller` aktiv ist.
- Weise ein `HMITheme.asset` zu.
- Prüfe, ob die USS-Variablen mit `--hmi-` beginnen und im Stylesheet verwendet werden.

### Controller findet Elemente nicht

- Vergleiche die Element-IDs im UXML mit den IDs im C#-Controller.
- Achte auf Groß- und Kleinschreibung.
- Stelle sicher, dass der Controller nach dem Laden des `UIDocument` ausgeführt wird.

## Erweiterungen

Für die nächste Ausbaustufe bieten sich an:

- Maschinen-Symbolik und 3D-Kamerafokus
- Trenddiagramme für Temperatur und Durchsatz
- Rezeptverwaltung für verschiedene Pizzaprodukte
- Benutzerrollen und Bedienberechtigungen
- Ereignisprotokoll und Schichtreport
- Signal-Ampel für Betriebszustände
- Verbindung zu Unity Events oder einer zentralen Produktionssimulation


## USS-Hinweis

Unity UI Toolkit unterstützt die CSS-Eigenschaft `gap` nicht in allen Unity-6-Versionen. Das Kit verwendet deshalb `padding-right` und `padding-bottom` für Abstände in flexiblen Reihen und Raster-Containern.
