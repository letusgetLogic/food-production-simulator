------------------------------------------------------------------------

## Assembly-Architektur

Die Codebasis wurde in mehreren Assemblies aufgeteilt, um die Kompilierzeit zu verringern, die Architektur sauber zu trennen.

![Assemblies-Zusammenhang](Docs/UML/FPS-UML-1-Assemblies.png)


------------------------------------------------------------------------

## Design-Prinzipien

# Maschinen-Architektur 

![Maschinen-Architektur](Docs/UML/FPS-UML-3-Machines.png)

Die Maschinen-Architektur (MachineBase und die Stationen darauf) folgt diesen Prinzipien:
- **Komposition statt Mehrfachvererbung**: IInteractable sitzt auf Maschinenteilen (Knöpfe, Hebel) und HMI-Elementen, nie auf der Maschine selbst.
- **Gemeinsame Basisklasse mit Zustandsautomat**: MachineBase hat eine validierte Übergangstabelle (Ready → Starting → Running → Stopping → Stopped, Störung Fault → Maintenance → Ready) und eine feste API (StartRun(), StopRun(), TriggerFault() …).
- **Interfaces**: IMachineParameterSource (das HMI kennt keine Maschinentypen), IInfeedReadiness, IDownstreamLink, ISaveableState, IProductReceiver, IRideable.
- **Ereignis- und Kanalkommunikation**: StateChanged-Events und ScriptableObject-Channels wie SO_MachineOverviewChannel, über die der Reporter Zustände an das HMI weitergibt.

------------------------------------------------------------------------

