# TessitoreGM Roadmap

## Visione

TessitoreGM è uno strumento per Game Master sostenuto da un motore di
simulazione per mondi persistenti, NPC autonomi e narrazione emergente.

Il Game Master stabilisce il mondo, introduce le azioni dei personaggi
giocanti e mantiene l'autorità sulla campagna. Il motore conserva lo stato,
simula ciò che accade nel tempo e rende visibili conseguenze che il Game
Master non dovrebbe calcolare o ricordare manualmente.

## Principi di sviluppo

- La simulazione precede la narrazione.
- Ogni cambiamento del mondo deriva da un evento.
- Gli stessi eventi devono produrre sempre lo stesso risultato.
- Il mondo continua a evolvere anche fuori dalla scena corrente.
- Un NPC agisce soltanto in base a ciò che può percepire o ricordare.
- Il narratore descrive i fatti senza modificarli.
- Ogni sistema viene prima provato in uno scenario piccolo e verificabile.
- Si preferiscono sistemi semplici e coerenti alla complessità prematura.
- Il Game Master rimane l'autorità finale sulla campagna.

## Stato del progetto

Legenda:

- `[x]` completato e verificato
- `[ ]` da realizzare

## Milestone 1 — Simulation Kernel

**Stato: completato**

Obiettivo: costruire il nucleo deterministico e persistente del motore.

- [x] Specifica e principi fondamentali
- [x] Identificatori tipizzati per entità, luoghi, ordini, oggetti e fatti
- [x] Modello degli eventi del mondo
- [x] Snapshot immutabile del mondo
- [x] Applicazione e replay degli eventi
- [x] Controllo dell'ordine cronologico
- [x] Persistenza JSON autosufficiente
- [x] Regole di simulazione riutilizzabili
- [x] Simulatore capace di applicare eventi proposti dalle regole
- [x] NPC con identità, ruolo e comportamenti
- [x] Memoria personale ricostruita dagli eventi osservabili
- [x] Condivisione persistente delle conoscenze tra NPC
- [x] Narratore deterministico basato sugli eventi
- [x] Test automatici del kernel

Risultato raggiunto: il mondo può essere salvato, ricaricato e ricostruito
senza perdere stato, conoscenze o cronologia.

## Milestone 2 — First Living Scenario

**Stato: completato**

Obiettivo: verificare il kernel con una commissione completa del fabbro.

- [x] Il cliente entra nella forgia
- [x] Il cliente richiede un oggetto parametrico
- [x] Il fabbro accetta l'ordine
- [x] Il cliente versa un anticipo
- [x] Il fabbro raggiunge il luogo di lavoro
- [x] Il fabbro inizia la produzione
- [x] Il tempo di produzione viene rispettato
- [x] L'oggetto viene creato e assegnato al fabbro
- [x] Il saldo viene pagato
- [x] L'oggetto viene consegnato al cliente
- [x] Lo scenario può essere salvato e continuato in sessioni diverse
- [x] La cronaca può essere generata dal registro degli eventi
- [x] Lo scenario funziona con oggetti, prezzi e anticipi differenti
- [x] La sequenza completa è coperta da test automatici

Risultato raggiunto: una commissione può attraversare l'intero ciclo di vita,
dalla richiesta alla consegna, mantenendo stato e cronologia persistenti.

## Milestone 3 — Simulation Runtime

**Stato: completato**

Obiettivo: rendere autonoma la simulazione dello scenario piccolo prima di
aggiungere più NPC e più luoghi.

Il Game Master deve poter chiedere:

```text
Avanza il mondo fino alle 13:00.
```

Il runtime deve individuare e applicare autonomamente tutti gli eventi dovuti
entro quell'orario, senza orari dello scenario codificati nella console.

### 3.1 — Orologio persistente del mondo

- [x] Introdurre un evento esplicito di avanzamento del tempo
- [x] Permettere al mondo di raggiungere un orario anche quando non accade nulla
- [x] Impedire avanzamenti verso il passato
- [x] Salvare e riprodurre l'avanzamento temporale
- [x] Narrare il passaggio del tempo soltanto quando utile

Decisione iniziale: i passaggi interni usati per valutare le regole non devono
riempire il registro. Il registro conserva gli eventi di dominio realmente
accaduti e l'orario finale raggiunto dal mondo.

### 3.2 — Proposte entro un intervallo

- [x] Sostituire la valutazione a un singolo istante con la ricerca del prossimo
      evento applicabile entro un orario limite
- [x] Spostare orari, durate e condizioni dentro comportamenti e regole
- [x] Eliminare dal comando di avanzamento gli orari `08:15`, `08:30` e `12:30`
- [x] Fare in modo che una regola non produca eventi oltre l'orario richiesto
- [x] Fare in modo che una regola non riproponga un evento già soddisfatto

Ogni regola dovrà rispondere alla domanda:

```text
Qual è il prossimo evento che questa regola può produrre
dopo l'ora corrente e non oltre l'ora richiesta?
```

### 3.3 — Ciclo autonomo

- [x] Raccogliere le proposte delle regole attive
- [x] Selezionare la proposta cronologicamente più vicina
- [x] Usare un ordine stabile e documentato in caso di parità
- [x] Validare e applicare l'evento selezionato
- [x] Rivalutare il mondo dopo ogni evento
- [x] Fermarsi quando non esistono altre proposte entro il limite
- [x] Portare infine l'orologio del mondo all'orario richiesto

Algoritmo previsto:

```text
1. Osserva lo stato corrente.
2. Chiedi a ogni regola il prossimo evento entro il limite.
3. Se non esistono proposte, avanza l'orologio al limite e termina.
4. Scegli la proposta più vicina nel tempo.
5. Applica e registra l'evento.
6. Torna al punto 1.
```

### 3.4 — Sicurezza e determinismo

- [x] Imporre un numero massimo di eventi per singolo avanzamento
- [x] Rilevare regole che ripropongono indefinitamente lo stesso evento
- [x] Rifiutare proposte precedenti allo stato corrente
- [x] Rifiutare proposte successive al limite richiesto
- [x] Garantire un risultato identico con lo stesso stato e le stesse regole
- [x] Garantire che due avanzamenti consecutivi equivalgano a uno complessivo
      quando non cambiano input o regole

Esempio di equivalenza richiesta:

```text
Avanza 08:00 → 10:00, poi 10:00 → 13:00

deve produrre lo stesso mondo di:

Avanza 08:00 → 13:00
```

### 3.5 — Comando `advance-to`

- [x] Aggiungere `advance-to <data-ora> [file-eventi]`
- [x] Accettare una data e ora non ambigua
- [x] Mostrare l'intervallo simulato
- [x] Mostrare gli eventi prodotti
- [x] Salvare il registro aggiornato soltanto dopo una simulazione valida
- [x] Lasciare intatto il salvataggio se la simulazione fallisce
- [x] Permettere `replay` e `narrate` sul risultato

Esempio atteso:

```powershell
dotnet run --project src/TessitoreGM.Console -- \
  advance-to "2026-08-03T13:00:00+00:00" world-events.json
```

### 3.6 — Test di accettazione

- [x] Un ordine accettato porta autonomamente il fabbro alla forgia
- [x] Il fabbro inizia il lavoro non appena le condizioni lo consentono
- [x] Il lavoro termina dopo la durata prevista
- [x] Un limite precedente al completamento lascia l'ordine in corso
- [x] Un limite successivo comprende il completamento
- [x] Un intervallo senza eventi aggiorna comunque l'ora del mondo
- [x] Un salvataggio intermedio può essere ricaricato e continuato
- [x] Il replay ricostruisce lo stesso stato finale
- [x] Il narratore produce la stessa cronaca degli eventi generati
- [x] Nessun orario della commissione rimane codificato nel comando di avanzamento

### Definition of Done

Il milestone è completato quando questo unico comando:

```text
advance-to 2026-08-03T13:00:00+00:00
```

porta autonomamente lo scenario del fabbro dallo stato `Accepted` allo stato
`Completed`, registra gli eventi intermedi nel corretto ordine e porta il
mondo esattamente alle 13:00.

## Milestone 4 — Small Living World

**Stato: completato**

Obiettivo: applicare il runtime autonomo a una giornata di villaggio.

- [x] Almeno tre NPC nel primo scenario automatico
- [x] Almeno tre luoghi nel primo scenario automatico
- [x] Routine giornaliere persistenti
- [x] Più attività simultanee e deterministiche
- [x] Spostamenti determinati dalle routine
- [x] Prima decisione basata su una risorsa accessibile all'NPC
- [x] Decisioni e interazioni basate sul luogo corrente
- [x] Decisioni basate sulle conoscenze personali
- [x] Eventi che continuano fuori dalla scena osservata
- [x] Salvataggio e continuazione della giornata
- [x] Cronaca dell'intera giornata

Risultato atteso: il villaggio continua a vivere senza che il Game Master
debba comandare ogni singola azione.

## Milestone 5 — Consequences and Relationships

**Stato: completato**

- [x] Bisogno persistente con intensità limitata tra 0 e 100
- [x] Crescita giornaliera autonoma del bisogno
- [x] Consumo di una risorsa per soddisfare il bisogno
- [x] Il bisogno e le scorte influenzano un'azione concreta
- [x] Scorte persistenti di risorse fungibili
- [x] Scambio atomico di risorse e monete
- [x] Primo commercio autonomo tra NPC compresenti
- [x] Persistenza, replay e narrazione degli scambi
- [x] Produzione semplice di una risorsa
- [x] Ciclo autonomo `bisogno → consumo → acquisto → produzione`
- [x] Fiducia minimale ricostruita dalle interazioni
- [x] Eventi espliciti di aumento e diminuzione della fiducia
- [x] Punteggi di fiducia limitati tra -100 e +100
- [x] Motivazioni persistenti e non ripetibili
- [x] Diffusione locale delle informazioni tra più NPC
- [x] Provenienza e momento dell'apprendimento ricostruibili dagli eventi
- [x] Una notizia appresa modifica una decisione concreta
- [x] Prima conseguenza che influenza la giornata successiva

Risultato raggiunto: il villaggio sostiene un ciclo materiale minimo e le
informazioni passano localmente tra gli NPC, modificando decisioni successive
senza intervento diretto del Game Master.

## Milestone 6 — Game Master Interface

**Stato: in corso**

- [x] Creare e caricare un mondo
- [x] Prima dashboard web locale in sola lettura
- [x] Osservare luoghi, NPC, attività, scorte e bisogni
- [x] Consultare ciò che un NPC sa
- [x] Selezionare un salvataggio dall'interfaccia
- [ ] Accesso protetto dalla rete locale per smartphone
- [ ] Introdurre azioni dei personaggi giocanti ed eventi esterni
  - [x] Primo intervento del GM: spostare un personaggio in un luogo
  - [x] Rivelare una conoscenza a un personaggio
- [x] Avanzare il tempo dal Tavolo del GM e salvare le conseguenze
- [ ] Anteprima delle conseguenze proposte
- [ ] Approvare o rifiutare conseguenze importanti
- [ ] Generare cronache e riepiloghi di sessione

Risultato atteso: TessitoreGM diventa utilizzabile direttamente durante una
campagna senza richiedere conoscenze tecniche del motore.

## Milestone 7 — World Pressure

**Stato: futuro**

- [ ] Clima
- [ ] Scarsità
- [ ] Agricoltura
- [ ] Crimine
- [ ] Fazioni
- [ ] Politica

Questi sistemi vengono introdotti soltanto quando il piccolo mondo, le routine
e le conseguenze persistenti funzionano già.

## Milestone 8 — Framework and 1.0

**Stato: futuro**

- [ ] API pubbliche e stabili
- [ ] Plugin
- [ ] Editor del mondo
- [ ] Documentazione per utenti e sviluppatori
- [ ] Compatibilità e migrazione dei salvataggi
- [ ] Strumenti di diagnostica della simulazione
- [ ] Narratore basato su IA come componente opzionale
- [ ] Prima release stabile

## Prossimo passo

Rendere il Tavolo del GM accessibile in modo protetto dalla rete locale, così
da poterlo usare da smartphone durante una sessione, mantenendo salvataggi e
comandi sotto il controllo del computer che ospita la campagna.

Reputazione, promesse, conflitti, prezzi dinamici e bisogni più complessi
restano intenzionalmente fuori dalla fase completata.
