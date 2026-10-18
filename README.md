------------------------------------------------------------------------

# Food Production Simulator

Ein 3D-Unity-Projekt über eine Simulation von einer Lebensmittel-Produktionskette.

Der Nutzer überwacht und steuert eine kleine industrielle Produktionslinie zur Herstellung von Tiefkühlpizza. Die Simulation bildet den Produktionsprozess von der Rohstoffbereitstellung bis zur verpackten Tiefkühlpizza ab. Maschinen, Förderbänder, Sensoren und Steuerungssysteme reagieren auf Produktionszustände und Fehler. Der Schwerpunkt liegt auf der realistischen Darstellung eines automatisierten Produktionsprozesses und einer modularen technischen Umsetzung.

------------------------------------------------------------------------

## Projekt

Entwicklungs-Zeitraum: **15.09.2026 - 14.10.2026**

- MVP-Stand erreicht
- Keine Erweiterungen implementiert, außer Palettierer und Waren-Transport
- Update am 11.10.2026

Ziele des Hobby-Projektes:

- Entwicklung von 3D Umgebung & Interaktions-Systemen
- Entwicklung auf Zielplattformen PC, WebGL und VR
- Kennenlernen von industriellen Lebensmittel-Produktionsprozess
- Workflow mit KI-Agenten

------------------------------------------------------------------------

## MVP

- Ein Rezept: Tiefkühlpizza Margherita
- Eine Produktionslinie
- Teigmischer, Portionierer, Formanlage, Sauce-/Käsedosierung, Ofen, Kühler, Gefriereimhiet, Verpackung
- Förderbänder
- Sensoren (Präsenz-, Füllstand-, Temperatur-)
- Maschinenzuständen mit 3-5 Fehlerfällen
- HMI (Human Machine Interface)
- Produktionsstatistik
- PC und WebGL

------------------------------------------------------------------------

## Erweiterungen

- Material- & Lagerverwaltung
- Kühlkammer
- Mehrere Rezepten & Produktionslinien
- Wartung-Mechaniken
- Wirtschaftliche Simulation (Economy- und Freischalt-Progresssion)
- Autonome NPCs
- VR
- Multiplayer
- Mobile mit klickbaren Navigations-Pfeilen

------------------------------------------------------------------------

## Features

- Bewegung & Interaktion
- Machine-System (State Machine)
- Sensor-System
- Förderband-System
- Rezept-/Produkt-Zustandsmodell
- 8 Produktionsstationen + Palettierer + Palettenlagerung
- Hubwagen
- Fehlersystem
- HMI/UI-System
- Quality-Check
- Save/Load-System
- Balancing-/Debug-Tools
- Tutorial/Onboarding
- Asset-Integration
- Localization

------------------------------------------------------------------------

## Design-Entscheidungen

- Puffer-Förderband vor Teigpresse um automatisches Stoppen zu realisieren
- Palettierer + Waren-Transport + Paletten-Management
- Benutzung von Hubwagen als Scooter
- Tutorial-Schritte wie Verarbeitung von Fehlerfällen und Waren-Transport hinzugefügt

------------------------------------------------------------------------

## Learning

- Assembly-Architektur und Zusammenhang + Unabhängigkeit zwischen Assemblies
- Localization Google Sheet
- Auseinandersetzung zwischen Code-Logik (Daten-Transfer) und Realität-Logik
- Scriptable-Object-Channel-Singelton-Pattern
- Lighting Fehler in WebGL - falsche Lichtberechnung auf großen Boden (zu dunkel) von Unity - Boden in kleinen Teilen 5x5m geteilt

------------------------------------------------------------------------

## Demonstration

[Food Production Simulator by nambition](https://nambition.itch.io/food-production-simulator)
