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

**Stato: completato per il primo uso locale**

- [x] Creare e caricare un mondo
- [x] Prima dashboard web locale in sola lettura
- [x] Osservare luoghi, NPC, attività, scorte e bisogni
- [x] Consultare ciò che un NPC sa
- [x] Selezionare un salvataggio dall'interfaccia
- [x] Accesso protetto dalla rete locale per smartphone
- [x] Introdurre azioni dei personaggi giocanti ed eventi esterni
  - [x] Primo intervento del GM: spostare un personaggio in un luogo
  - [x] Rivelare una conoscenza a un personaggio
  - [x] Registrare nella cronaca un'azione libera di un personaggio giocante
  - [x] Roster persistente dei personaggi giocanti, distinti dagli NPC autonomi
  - [x] Selezionare un PG registrato per azioni e spostamenti
  - [x] Tradurre le conseguenze dell'azione in eventi specifici del mondo
    - [x] Rivelare conoscenze anche ai PG
    - [x] Trasferire monete tra PG e NPC con una motivazione persistente
    - [x] Acquisire, perdere e trasferire risorse tramite eventi espliciti
- [x] Avanzare il tempo dal Tavolo del GM e salvare le conseguenze
- [x] Anteprima delle conseguenze proposte dall'avanzamento temporale
- [x] Approvare o rifiutare l'avanzamento prima del salvataggio
- [x] Generare cronache e riepiloghi della campagna
  - [x] Cronaca completa ricostruita dal registro persistente
  - [x] Riepilogo oggettivo di intervallo, avvenimenti e azioni dei giocatori
  - [x] Vista leggibile e stampabile dal Tavolo del GM

La delimitazione formale delle singole sessioni resta rinviata: non è
necessaria per rendere giocabile il primo ciclo locale.

Risultato raggiunto: il GM può amministrare il mondo, i PG e le conseguenze
principali senza richiedere conoscenze tecniche del motore.

## Milestone 7 — Player Table

**Stato: in corso — ciclo di gioco completato, accesso personale rinviato**

Obiettivo: rendere la campagna giocabile fuori dal pannello di controllo del
GM, mantenendo il GM come autorità e senza mostrare ai giocatori informazioni
che i loro personaggi non possiedono.

### 7.1 — Accesso e punto di vista del PG

- [x] Pagina giocatore separata dal Tavolo del GM
- [ ] Codice o collegamento temporaneo associato a un solo PG *(rinviato)*
- [ ] Selezione del PG autorizzata dal GM *(rinviata)*
- [x] Scheda essenziale con nome, posizione, monete e risorse
- [x] Mostrare soltanto le conoscenze possedute dal PG
- [x] Mostrare personaggi presenti nello stesso luogo
- [x] Nascondere controlli del GM, informazioni segrete e stato fuori scena

### 7.2 — Ciclo dell'azione

- [x] Il giocatore descrive e invia un'azione proposta
- [x] La proposta non modifica direttamente il mondo
- [x] Coda persistente delle proposte in attesa
- [x] Il GM vede autore, testo e momento della proposta
- [x] Il GM può approvare, rifiutare o risolvere la proposta
- [x] L'approvazione registra l'azione nella cronaca
- [x] Il GM applica separatamente le conseguenze meccaniche necessarie
- [x] Il giocatore vede esito narrato e nuovo stato del proprio PG

Flusso minimo:

```text
Giocatore propone un'azione
        ↓
GM la valuta e, se necessario, richiede un tiro
        ↓
azione registrata + conseguenze specifiche
        ↓
il giocatore vede il risultato
```

### 7.3 — Risoluzione d20

Obiettivo: sostenere prove in stile D&D senza incorporare un regolamento
completo e senza lasciare che un tiro modifichi automaticamente il mondo.

- [x] Il GM può richiedere un tiro collegato a un'azione proposta
- [x] Richiesta con descrizione, modificatore e difficoltà opzionale
- [x] Difficoltà pubblica oppure visibile soltanto al GM
- [x] Tiro normale di `1d20 + modificatore`
- [x] Vantaggio: due d20 e mantenimento del risultato maggiore
- [x] Svantaggio: due d20 e mantenimento del risultato minore
- [x] Generazione del tiro sul server, non nel browser del giocatore
- [x] Una richiesta può produrre un solo risultato e non può essere ritirata
- [x] Registrare dadi individuali, modificatore, totale e momento del tiro
- [x] Evidenziare 1 e 20 naturali senza imporre automaticamente un esito
- [x] Il replay usa il risultato registrato e non lancia nuovamente i dadi
- [x] Il GM resta responsabile dell'esito e delle conseguenze sul mondo

Decisione iniziale: il primo taglio supporta soltanto il d20. Dadi generici,
danni, tabelle, iniziativa e formule regolistiche arriveranno solo se un caso
d'uso reale li renderà necessari.

### 7.4 — Scena giocabile

- [x] Vista leggibile del luogo attuale
- [x] Eventi recenti osservabili dal PG
- [x] Narrazione deterministica dei fatti visibili
- [x] Aggiornamento manuale affidabile su PC e smartphone
- [x] Stato chiaro dell'azione: in attesa, tiro richiesto, risolta o rifiutata
- [ ] Interfaccia mobile utilizzabile durante una sessione reale

### 7.5 — Sicurezza e prova di accettazione

- [ ] Un giocatore non può impersonare un altro PG
- [x] Un giocatore non può inviare eventi direttamente al motore
- [ ] Un giocatore non può leggere conoscenze o cronache riservate
- [x] Un giocatore non può alterare, ripetere o sostituire un tiro registrato
- [x] Chiusura o riavvio non perde le azioni in attesa
- [ ] Prova completa con due browser: uno GM e uno giocatore
- [ ] Prova completa dalla rete locale con uno smartphone

### Definition of Done

Il milestone è completato quando, da un secondo dispositivo, un giocatore può
aprire il proprio PG, vedere soltanto la sua scena, proporre un'azione e
ricevere l'esito dopo la decisione del GM, senza accedere al Tavolo del GM e
senza modificare direttamente il mondo.

Non fanno parte di questo milestone: schede regolistiche complete, sistema di
combattimento, danni, iniziativa, chat, IA narrativa e accesso remoto via
Internet.

## Milestone 8 — World Pressure

**Stato: futuro**

- [ ] Clima
- [ ] Scarsità
- [ ] Agricoltura
- [ ] Crimine
- [ ] Fazioni
- [ ] Politica

Questi sistemi vengono introdotti soltanto quando il piccolo mondo, le routine
e le conseguenze persistenti funzionano già.

## Milestone 9 — Framework and 1.0

**Stato: futuro**

- [ ] API pubbliche e stabili
- [ ] Plugin
- [ ] Editor del mondo
- [ ] Documentazione per utenti e sviluppatori
- [ ] Compatibilità e migrazione dei salvataggi
- [ ] Strumenti di diagnostica della simulazione
- [ ] Narratore basato su IA come componente opzionale
- [ ] Hosting remoto opzionale con HTTPS, account, persistenza e backup
- [ ] Prima release stabile

## Prossimo passo

Provare il ciclo completo con due browser e poi da smartphone sulla rete
locale. Il codice personale per ogni PG resta intenzionalmente rinviato.

Reputazione, promesse, conflitti, prezzi dinamici e bisogni più complessi
restano intenzionalmente fuori dalla fase completata.
