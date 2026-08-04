# TessitoreGM

> Non racconta storie. Tesse mondi.

TessitoreGM è un motore open source di simulazione per Game Master basato su mondi persistenti, NPC autonomi e narrazione emergente.

Il progetto comprende un kernel di simulazione persistente e una prima
interfaccia locale per il Game Master.

## Provare il Tavolo del GM

Da PowerShell, nella cartella del repository:

```powershell
dotnet run --project src/TessitoreGM.Console -- create-village village.json
dotnet run --project src/TessitoreGM.Gm -- village.json
```

Aprire quindi [http://localhost:5074](http://localhost:5074) nel browser del
PC. La prima versione è intenzionalmente in sola lettura e accessibile soltanto
dal computer che la esegue.

La dashboard mostra:

- ora ed eventi del mondo;
- posizione, denaro, scorte e bisogni degli NPC;
- conoscenze personali;
- ultimi avvenimenti narrati.

Per arrestarla, premere `Ctrl+C` nel terminale che la sta eseguendo.
