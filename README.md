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
