# Estendere la simulazione

Il primo punto di estensione stabile di TessitoreGM è una regola del mondo.
Una regola osserva uno `WorldSnapshot` immutabile e può proporre il prossimo
`IWorldEvent` entro l'intervallo richiesto.

```csharp
public sealed class MarketBellRule : IWorldRule
{
    public IWorldEvent? ProposeNext(
        WorldSnapshot world,
        DateTimeOffset until)
    {
        // Restituisce un evento supportato oppure null.
    }
}

var rules = new WorldRuleRegistry()
    .Register("my-module:market-bell", new MarketBellRule());

var result = rules.CreateSimulator().Advance(world, until);
```

## Garanzie del contratto

- l'ordine di registrazione risolve in modo deterministico gli eventi simultanei;
- ogni identificatore è univoco senza distinzione tra maiuscole e minuscole;
- la regola non modifica direttamente il mondo;
- TessitoreGM controlla l'orario e applica ogni evento con il processore ufficiale;
- eventi non supportati o incompatibili con lo stato corrente vengono rifiutati;
- una regola restituisce `null` quando non ha eventi da proporre.

I plugin caricabili da cartella verranno costruiti sopra questo contratto. Non
è prevista l'esecuzione automatica di assembly esterni non attendibili.

## Plugin locali

Un plugin implementa `IWorldPlugin`, espone un identificatore e una versione,
e registra le proprie regole nel registro ricevuto. La cartella `Plugins`
accanto all'applicazione contiene le DLL e i manifesti `*.plugin.json`:

```json
{
  "id": "my-module:market",
  "version": "1.0.0",
  "assembly": "MyMarketPlugin.dll",
  "enabled": false
}
```

Il caricamento richiede `enabled: true`, limita la DLL alla cartella dei plugin
e verifica che identità e versione coincidano. Una registrazione fallita viene
scartata per intero e appare in Diagnostica senza bloccare la campagna.

Una DLL è codice locale con i permessi dell'applicazione: deve essere attivata
soltanto se proviene da una fonte fidata.
