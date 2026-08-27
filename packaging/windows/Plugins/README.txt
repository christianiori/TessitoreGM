PLUGIN LOCALI DI TESSITOREGM

Inserisci in questa cartella la DLL e il manifesto forniti dal creatore del
plugin. Un plugin viene caricato soltanto quando il manifesto termina con
.plugin.json e contiene "enabled": true.

Esempio:
{
  "id": "autore:nome-plugin",
  "version": "1.0.0",
  "assembly": "NomePlugin.dll",
  "enabled": false
}

Attiva soltanto plugin provenienti da persone fidate: una DLL è codice locale
e dispone degli stessi permessi dell'applicazione. Riavvia TessitoreGM dopo
ogni modifica e controlla la pagina Diagnostica.
