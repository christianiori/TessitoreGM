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

**Stato: completato**

Obiettivo: rendere la campagna giocabile fuori dal pannello di controllo del
GM, mantenendo il GM come autorità e senza mostrare ai giocatori informazioni
che i loro personaggi non possiedono.

### 7.1 — Accesso e punto di vista del PG

- [x] Pagina giocatore separata dal Tavolo del GM
- [x] Codice o collegamento temporaneo associato a un solo PG
- [x] Selezione del PG autorizzata dal GM
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
- [x] Aggiornamento automatico senza perdere un'azione in scrittura
- [x] Stato chiaro dell'azione: in attesa, tiro richiesto, risolta o rifiutata
- [x] Messa a fuoco di un luogo nella vista del GM
- [x] Personaggi presenti e cronaca filtrati sulla scena scelta
- [x] Azione rapida per portare un personaggio nella scena
- [x] Azione rapida per spostare un presente in un altro luogo
- [x] Trasferimento rapido di monete tra personaggi presenti
- [x] Acquisizione o perdita rapida di risorse nella scena
- [x] Rivelazione rapida di conoscenze a un personaggio presente
- [x] Interfaccia mobile utilizzabile durante una sessione reale
  - [x] Navigazione fissa tra scena, azioni, presenti e registro
  - [x] Strumenti completi raccolti in un pannello mobile richiudibile
  - [x] Anteprima delle conseguenze mantenuta aperta quando necessaria
  - [x] Moduli della scena disposti su una colonna con controlli tattili
  - [x] Collaudo durante una sessione reale

### 7.5 — Sicurezza e prova di accettazione

- [x] Un giocatore non può impersonare un altro PG
- [x] Un giocatore non può inviare eventi direttamente al motore
- [x] Un giocatore non può leggere conoscenze o cronache riservate
- [x] Un giocatore non può alterare, ripetere o sostituire un tiro registrato
- [x] Chiusura o riavvio non perde le azioni in attesa
- [x] Prova completa con due browser: uno GM e uno giocatore
- [x] Prova completa dalla rete locale con uno smartphone

### Definition of Done

Il milestone è completato quando, da un secondo dispositivo, un giocatore può
aprire il proprio PG, vedere soltanto la sua scena, proporre un'azione e
ricevere l'esito dopo la decisione del GM, senza accedere al Tavolo del GM e
senza modificare direttamente il mondo.

Non fanno parte di questo milestone: schede regolistiche complete, sistema di
combattimento, danni, iniziativa, chat, IA narrativa e accesso remoto via
Internet.

## Milestone 8 — World Pressure

**Stato: rinviato — fondazione climatica conservata, nuove pressioni congelate**

- [x] Clima
  - [x] Stato climatico persistente nel mondo
  - [x] Ciclo giornaliero configurabile e deterministico
  - [x] Cambiamenti registrati come eventi e ricostruibili dal replay
  - [x] Narrazione del cambiamento climatico
  - [x] Clima corrente ed eventi visibili al GM e ai giocatori
  - [x] Copertura automatica di ciclo, persistenza e determinismo
- [ ] Scarsità
- [ ] Agricoltura
- [ ] Crimine
- [ ] Fazioni
- [ ] Politica

Questi sistemi vengono introdotti soltanto quando il piccolo mondo, le routine
e le conseguenze persistenti funzionano già.

Decisione di progetto: il clima resta per ora uno stato autonomo e narrativo.
I suoi effetti su produzione, raccolti e scorte appartengono a una fase avanzata
da progettare insieme a scarsità e agricoltura.

Scarsità, agricoltura, crimine, fazioni e politica non vengono sviluppati finché
TessitoreGM non è stato consolidato come applicazione autonoma, distribuibile e
recuperabile in caso di errore. Anche il collaudo esteso del clima è rinviato a
quella fase.

## Milestone 9 — Framework and 1.0

**Stato: in corso — priorità a stabilità e uso autonomo**

### 9.1 — Distribuzione locale autonoma

- [x] Portare tutti i progetti a una versione .NET supportata
- [ ] Produrre un pacchetto Windows autosufficiente che non richieda SDK o Git
  - [x] Script ripetibile di pubblicazione self-contained `win-x64`
  - [ ] Collaudo del pacchetto estratto su Windows
- [ ] Avviare il Tavolo del GM senza comandi PowerShell
  - [x] Avvio locale e LAN tramite collegamenti a doppio clic
  - [x] Apertura automatica del browser
  - [ ] Collaudo dell'avvio a doppio clic
- [x] Separare programma e campagne dell'utente
- [x] Fornire una campagna dimostrativa al primo avvio
- [x] Rendere ripetibile e documentata la creazione del pacchetto

### 9.2 — Stabilità dei dati e diagnostica

- [ ] Compatibilità e migrazione dei salvataggi
- [x] Backup automatici prima di ogni modifica persistente
  - [x] Copia valida precedente conservata per ogni salvataggio
  - [x] Scrittura temporanea verificata prima della sostituzione
  - [x] Massimo di venti copie automatiche per campagna
- [x] Recupero guidato dopo un salvataggio non leggibile
  - [x] Selezione del backup dal Tavolo del GM
  - [x] Conservazione del file problematico prima del ripristino
- [ ] Strumenti di diagnostica della simulazione
  - [x] Prima pagina diagnostica per salvataggio, eventi, backup e percorsi
  - [ ] Analisi delle regole e degli eventi proposti
- [ ] Messaggi di errore utilizzabili senza conoscenze tecniche
  - [x] Pagina di recupero per campagne non leggibili
  - [ ] Revisione sistematica degli altri errori operativi

### 9.3 — Framework estensibile

- [ ] API pubbliche e stabili
- [ ] Plugin
- [ ] Editor del mondo

### 9.4 — Modalità AI Game Master

- [x] Usare la memoria persistente di Tessitore come fondamento della modalità
  - [x] Ricostruire a ogni turno il dossier dai file canonici della campagna
  - [x] Tenere separate la cronaca completa e la memoria osservata da ogni attore
  - [x] Non considerare mai la memoria della conversazione IA come stato di gioco
- [x] Definire una modalità alternativa e opzionale al GM umano
- [x] Stabilire che l'AI non decide mai le azioni dei giocatori umani
  - [x] Ogni turno AI risponde a un'azione già dichiarata da un giocatore
  - [x] Nessun comando AI può creare o sostituire un'azione del giocatore
- [x] Configurazione del fornitore separata dai salvataggi della campagna
  - [x] Modalità AI disattivata per impostazione predefinita
  - [x] Attivazione esplicita e indipendente per ogni campagna
  - [x] Metadati di fornitore e modello conservati fuori dal salvataggio
  - [x] Credenziali escluse dal formato di configurazione
- [x] Contratto indipendente dal fornitore IA
- [x] Protocollo JSON chiuso per contesto e piani tipizzati
- [x] Contesto di turno limitato a regole, stato canonico, memoria e cronaca
- [x] Proposte di conseguenza tipizzate: mai modifiche dirette ai file JSON
- [x] Validazione delle proposte da parte del motore di Tessitore
- [x] Politica deterministica per distinguere conseguenze ordinarie e importanti
- [x] Applicazione automatica delle conseguenze ordinarie valide
- [x] Coda persistente di conferma per le conseguenze importanti
  - [x] Approvazione e rifiuto atomici con nuova validazione
  - [x] Controlli della coda nel Tavolo del GM
- [x] Registro persistente di proposte, approvazioni e rifiuti
- [ ] Costruzione del contesto dalla prospettiva autorizzata del personaggio
- [ ] Gestione narrativa delle scene e dei PNG
- [ ] Richiesta di tiri e difficoltà entro limiti configurabili
- [x] Primo collegamento a un fornitore IA scelto dal GM
  - [x] Adattatore locale Ollama senza chiavi API
  - [x] Risposte JSON strutturate secondo il protocollo chiuso di Tessitore
  - [x] Dossier compatto per ridurre memoria e contesto richiesti al portatile
  - [x] Configurazione del modello dal Tavolo del GM
  - [x] Esecuzione automatica dopo una nuova azione umana persistita
  - [ ] Adattatore OpenAI opzionale
- [x] Fallback sicuro quando il servizio IA non è disponibile
  - [x] Nessuna modifica al mondo in caso di timeout o errore del fornitore
  - [x] Piano invalido rifiutato e azione lasciata al GM umano
- [ ] Interfaccia di sessione ispirata ai GM virtuali conversazionali

### 9.5 — Documentazione e release

- [ ] Documentazione per utenti e sviluppatori
- [ ] Prima release stabile

## Prossimo passo

Raffinare la gestione narrativa di scene e PNG sopra il primo adattatore
locale Ollama, mantenendo invariati il confine deterministico, la conferma delle
conseguenze importanti e il divieto assoluto di decidere azioni dei giocatori.
L'adattatore OpenAI resta il secondo fornitore previsto; tiri e difficoltà
proposti dall'IA verranno aggiunti dopo il primo ciclo narrativo stabile.

Reputazione, promesse, conflitti, prezzi dinamici e bisogni più complessi
restano intenzionalmente fuori dalla fase completata.
