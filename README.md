# TessitoreGM

> Non racconta storie. Tesse mondi.

TessitoreGM è un motore open source di simulazione per Game Master basato su mondi persistenti, NPC autonomi e narrazione emergente.

Il progetto comprende un kernel di simulazione persistente e una prima
interfaccia locale per il Game Master.

## Provare il Tavolo del GM

Da PowerShell, nella cartella del repository:

```powershell
dotnet run --project src/TessitoreGM.Gm -- village.json
```

Aprire quindi [http://localhost:5074](http://localhost:5074) nel browser del
PC. La prima versione è accessibile soltanto dal computer che la esegue.

La dashboard mostra:

- ora ed eventi del mondo;
- selezione e creazione delle campagne senza riavviare il server;
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

I salvataggi creati dall'interfaccia usano il suffisso `.save.json` e non
vengono inclusi nei commit Git.

### Usare il Tavolo dallo smartphone

Con computer e telefono collegati alla stessa rete Wi-Fi, avviare esplicitamente
la modalità LAN protetta:

```powershell
dotnet run --project src/TessitoreGM.Gm -- village.json --lan
```

Il terminale mostra l'indirizzo da aprire sul telefono e un codice temporaneo
di otto cifre. Il codice cambia a ogni avvio, la sessione scade dopo dodici ore
e cinque tentativi errati bloccano gli accessi per un minuto. Senza `--lan`, il
Tavolo resta accessibile soltanto dal computer locale.

Windows potrebbe chiedere una volta l'autorizzazione per la rete privata. Non
abilitare l'accesso sulle reti pubbliche.

Se la porta predefinita è già occupata, è possibile sceglierne un'altra, per
esempio aggiungendo `--port=5075` al comando di avvio.
- ultimi avvenimenti narrati.

Per arrestarla, premere `Ctrl+C` nel terminale che la sta eseguendo.
