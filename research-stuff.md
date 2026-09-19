Die Recherche dient zur Erweiterung des Wissens und zur Realisierung von der Produktion.

8 Produktionsstationen = Teigmischer, Portionierer, Formanlage, Dosierstation, Ofen, Kühleinheit, Schockfroster, Verpackung

Teigknetmaschine
https://taizyfoodmachinery.com/de/dough-kneading-machine-dough-mixer/

Teigportionier
https://www.ggmgastro.com/de-de-eur/teigportionier-er-inkl-teigabrunder-und-untergestell-gewichtsbereich-50-300g-tpp300n-tap3-uttp?channable=423d39736b75005450503330304e23544150332355545450a9&utm_source=google&utm_medium=cpc&utm_campaign=kuechengeraete&utm_content=google&utm_term=TPP300N%23TAP3%23UTTP&gad_source=5&gad_campaignid=22574020964&gclid=EAIaIQobChMI0YjgiujflgMVo6ODBx00QAdiEAQYASABEgI4NfD_BwE

Pizzateig-Formpresse
https://www.gastore.de/pizzateigpresse-fuer-pizzen-o-35-cm-OEM.html

## Produktionsstationen

### Rohstofflager

Verwaltung von:

* Mehl
* Wasser
* Hefe
* Salz
* Tomatensauce
* Käse
* Verpackungsmaterial

Das Lager besitzt Bestände und kann die Produktion stoppen, wenn benötigte Materialien fehlen.

### Teigmischer

Mischt die benötigten Zutaten nach einem Rezept.

Simulierte Parameter:

* Mischdauer
* benötigte Zutaten
* Mischstatus
* Maschinenstatus

### Portionierer

Teilt den fertigen Teig in einzelne Portionen.

Eine Waage überprüft das Gewicht.

```text
Sollgewicht: 300 g
Toleranz:    ±10 g
```

Außerhalb der Toleranz wird die Portion als fehlerhaft markiert.

### Formanlage

Formt die Teigportion zu einem Pizzaboden.

Der Boden wird anschließend auf das Förderband übergeben.

### Dosierstation

Automatische Dosierung von:

* Tomatensauce
* Käse
* optional weiteren Zutaten

Die Dosiermenge kann überwacht werden.

### Backofen

Simuliert einen kontinuierlichen oder chargenweisen Backprozess.

Parameter:

* Temperatur
* Backzeit
* Durchlaufzeit
* Ofenstatus

### Kühleinheit

Die Pizza wird nach dem Backen kontrolliert heruntergekühlt.

### Schockfroster

Die Pizza wird auf eine niedrige Temperatur gebracht.

Die Simulation konzentriert sich auf:

* Temperatur
* Durchlaufzeit
* Anlagenstatus

### Verpackungsanlage

Die fertige Pizza wird verpackt und mit Produkt-/Chargendaten versehen.

#### Rechtliche Kennzeichnung (Verpackung)
Für die Füllmengenangabe auf verpackten Lebensmitteln gilt in Deutschland die Fertigpackungsverordnung (FPackV).  Flüssige Lebensmittel werden in der Regel nach Volumen (Liter, Milliliter) gekennzeichnet, während feste Lebensmittel nach Gewicht (Gramm, Kilogramm) angegeben werden.  Es gibt jedoch wichtige Ausnahmen:

- Nach Gewicht gekennzeichnet: Honig, Sirupe, Milcherzeugnisse (wie Joghurt), Essigessenz und Würzen. 
- Nach Volumen gekennzeichnet: Feinkostsoßen, Senf und Speiseeis. 
- Grenzfälle: Hier entscheidet die „allgemeine Verkehrsauffassung“. Ist die flüssige Beschaffenheit charakteristisch (z. B. Nudelsuppe), wird nach Volumen gekennzeichnet. Ist der feste Bestandteil wertgebend (z. B. Fisch in Soße), wird nach Gewicht gekennzeichnet. 

### Qualitätskontrolle

Überprüft beispielsweise:

* Gewicht
* Temperatur
* Produktionsstatus
* Verpackungsstatus

Fehlerhafte Produkte werden ausgeschleust.

---


Fachwörter:
HMI-Bedienteil = Human Machine Interface
