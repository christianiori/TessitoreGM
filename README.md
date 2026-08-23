# TessitoreGM

> Non racconta storie. Tesse mondi.

TessitoreGM è un motore open source di simulazione per Game Master basato su mondi persistenti, NPC autonomi e narrazione emergente.

Il progetto comprende un kernel di simulazione persistente e una prima
interfaccia locale per il Game Master.

## Requisiti

- .NET 8 SDK

## Creare il pacchetto Windows autonomo

Il pacchetto destinato all'uso normale include il runtime .NET e non richiede
Git, Visual Studio o il .NET SDK sul computer che lo esegue. Da PowerShell,
nella cartella del repository, crearlo con:

```powershell
.\scripts\publish-windows.ps1
```

Il file risultante è `artifacts\TessitoreGM-win-x64.zip`. Dopo averlo estratto,
si può avviare con un doppio clic su:

- `Avvia TessitoreGM.cmd` per usarlo soltanto sul PC;
- `Avvia TessitoreGM in rete locale.cmd` per collegare anche smartphone e altri
  dispositivi della rete privata.

Il browser si apre automaticamente. Al primo avvio viene creata una campagna
dimostrativa in `Documenti\TessitoreGM\Campagne`: aggiornare o sostituire i file
del programma non cancella quindi le campagne dell'utente. La finestra di
TessitoreGM deve rimanere aperta durante l'uso; per arrestarlo è sufficiente
chiuderla.

### Backup, recupero e diagnostica

Prima di modificare una campagna esistente, TessitoreGM conserva
automaticamente la versione precedente nella cartella `Backups` accanto ai
salvataggi. Mantiene le venti copie più recenti per ogni campagna e verifica sia
il nuovo file sia la copia di sicurezza prima della sostituzione.

Se una campagna non è più leggibile, il Tavolo mostra i backup disponibili e
permette al GM di ripristinarne uno. Anche il file problematico viene conservato
prima del recupero. La pagina **Diagnostica** riporta leggibilità, ora del mondo,
numero di eventi, copie disponibili e percorsi effettivamente utilizzati.

### Modalità AI Game Master

La modalità AI è opzionale, disattivata per impostazione predefinita e si
seleziona separatamente per ogni campagna dal Tavolo del GM. Lo stato del mondo,
le regole e la memoria restano nei file canonici di Tessitore: l'AI non può
modificare direttamente i salvataggi e non decide mai le azioni dei giocatori.
L'associazione tra una risposta e l'azione umana corrente viene imposta da
Tessitore: il modello non genera e non può sostituire l'identificatore
dell'azione.

La selezione della modalità e i soli metadati di fornitore e modello vengono
conservati fuori dalle campagne, in
`Documenti\TessitoreGM\Configurazione\ai-gm.json`. Il file non contiene chiavi,
token o altre credenziali.

Il primo motore supportato è **Ollama**, eseguito sullo stesso PC di Tessitore:
non richiede una chiave API e il dossier della campagna non viene inviato a un
servizio cloud. Dopo aver installato Ollama, preparare il modello consigliato
da PowerShell:

```powershell
ollama pull qwen2.5:7b
```

Su un portatile con poca memoria si può iniziare con `qwen2.5:3b`. Lasciare
Ollama in esecuzione, aprire il Tavolo del GM, confermare il nome del modello
nel pannello **Modalità di conduzione** e selezionare **Modalità AI**. Da quel
momento ogni nuova azione dichiarata da un giocatore viene risolta localmente.
Se Ollama è spento, il modello non è installato o la risposta non supera i
controlli, l'azione rimane nella normale coda del GM umano senza modificare il
mondo.

Il dossier fornito al modello è ricostruito a ogni turno dai salvataggi. Lo
spazio di lavoro privato del GM contiene stato canonico, memorie degli attori,
cronaca recente e catalogo della campagna. Una sezione separata
`authorizedPerspective` contiene invece soltanto ciò che il PG può usare come
punto di vista narrativo: il proprio stato, il luogo e i personaggi visibili,
le conoscenze personali, gli eventi osservati e gli scambi già ricevuti nella
scena. Un adattatore OpenAI opzionale è previsto come passo successivo, senza
sostituire il funzionamento locale.

Gli scambi narrativi completati nella stessa scena vengono reinseriti nei turni
successivi, così PNG e dialoghi mantengono continuità senza trasformare ogni
dettaglio narrato in una modifica meccanica del mondo. Ogni PG può avere una
sola azione irrisolta: se Ollama non risponde, il GM può riprovare dal pulsante
**Affida a Ollama** nella coda oppure risolverla manualmente.

Il dossier contiene inoltre il catalogo canonico corrente della campagna:
personaggi registrati con ruolo e posizione, luoghi, risorse, bisogni e fatti
conosciuti dal motore. Questo permette a Ollama di usare identificatori validi
anche quando, per esempio, propone di spostare un PNG verso un luogo diverso.
Il catalogo è conoscenza del Game Master AI e non diventa automaticamente
informazione disponibile al personaggio del giocatore. Le istruzioni del
protocollo impongono di costruire la risposta narrativa soltanto dalla
prospettiva autorizzata. Se nel turno un PNG comunica un nuovo fatto canonico,
il piano deve anche proporne la rivelazione persistente al PG; la rivelazione è
una conseguenza importante e attende quindi la conferma del GM.

## Provare il Tavolo del GM

Da PowerShell, nella cartella del repository:

```powershell
dotnet run --project src/TessitoreGM.Gm -- village.json
```

Aprire quindi [http://localhost:5074](http://localhost:5074) nel browser del
PC. La prima versione è accessibile soltanto dal computer che la esegue.

La dashboard mostra:

- ora, clima ed eventi del mondo;
- selezione e creazione delle campagne senza riavviare il server;
- messa a fuoco di un luogo come scena corrente del GM, filtrando personaggi
  presenti e cronaca locale senza modificare lo stato del mondo;
- azioni rapide per portare personaggi nella scena selezionata o spostarli in
  un altro luogo tramite eventi persistenti;
- conseguenze rapide nella scena per monete, risorse e conoscenze, limitate ai
  personaggi effettivamente presenti;
- navigazione mobile fissa tra scena, azioni, presenti e registro, con gli
  strumenti completi del GM raccolti in un pannello richiudibile;
- posizione, denaro, scorte e bisogni degli NPC;
- conoscenze personali;
- controllo per avanzare il mondo di 1, 6 o 24 ore, applicando le regole
  autonome, mostrando prima le conseguenze e salvandole soltanto dopo
  l'approvazione del GM;
- comando del GM per spostare un personaggio in un luogo e registrare
  l'intervento nel mondo persistente;
- rivelazione di informazioni agli NPC, utilizzabile dalle loro decisioni
  autonome, scegliendo un fatto esistente o definendone un nuovo identificatore;
- registrazione libera delle azioni dei personaggi giocanti nella cronaca,
  separata dalle conseguenze meccaniche applicate al mondo.
- roster persistente dei personaggi giocanti, selezionabili per azioni e
  spostamenti ma esclusi dalle decisioni autonome del motore;
- conseguenze persistenti sui PG: apprendimento di informazioni e
  trasferimenti motivati di monete con gli NPC;
- acquisizione, perdita e trasferimento motivato delle risorse possedute.
- cronaca completa della campagna con riepilogo oggettivo e vista stampabile.
- ciclo climatico giornaliero deterministico, persistente e narrato nelle viste
  del GM e dei giocatori.

I salvataggi creati dall'interfaccia usano il suffisso `.save.json` e non
vengono inclusi nei commit Git.

### Vista del giocatore

Ogni scheda PG nel Tavolo del GM contiene il collegamento **Apri vista
giocatore**. La pagina separata mostra soltanto nome, luogo, clima, monete, risorse,
conoscenze del PG, personaggi compresenti ed eventi recenti osservabili. Non
espone controlli del GM, cronaca globale o informazioni fuori scena.

Il giocatore può proporre un'azione senza modificare direttamente il mondo.
La proposta resta salvata nella coda del GM, che può richiedere un tiro d20
normale, con vantaggio o svantaggio, e infine approvare o rifiutare l'azione
scrivendone l'esito. Ogni richiesta produce un solo tiro generato sul server;
risultato ed esito sopravvivono alla chiusura dell'applicazione.

La vista controlla automaticamente ogni cinque secondi se il mondo è cambiato.
Se il giocatore non sta scrivendo, mostra subito le novità; se esiste una bozza
non inviata, la conserva e propone un aggiornamento manuale.

Il Game Master genera dalla scheda del PG un codice personale monouso di otto
cifre. Il giocatore sceglie **Accedi al tuo personaggio** nella pagina iniziale
e inserisce quel codice: la sessione risultante può aprire soltanto il PG
autorizzato e non consente l'accesso al Tavolo del GM o alla cronaca globale.
La generazione di un nuovo codice revoca il precedente accesso del PG.

### Usare il Tavolo dallo smartphone

Con computer e telefono collegati alla stessa rete Wi-Fi, avviare esplicitamente
la modalità LAN protetta:

```powershell
dotnet run --project src/TessitoreGM.Gm -- village.json --lan
```

Il terminale mostra l'indirizzo da aprire sul telefono e un codice temporaneo
di otto cifre riservato al Game Master. Il codice cambia a ogni avvio, la
sessione scade dopo dodici ore e cinque tentativi errati bloccano gli accessi
per un minuto. I giocatori usano invece il proprio codice monouso generato dal
GM. Senza `--lan`, il Tavolo resta accessibile soltanto dal computer locale.

Windows potrebbe chiedere una volta l'autorizzazione per la rete privata. Non
abilitare l'accesso sulle reti pubbliche.

Se la porta predefinita è già occupata, è possibile sceglierne un'altra, per
esempio aggiungendo `--port=5075` al comando di avvio.
- ultimi avvenimenti narrati.

Per arrestarla, premere `Ctrl+C` nel terminale che la sta eseguendo.
