# Ratings & Recommendations per Jellyfin

[![Ultima release](https://img.shields.io/github/v/release/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations)](https://github.com/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations/releases)
[![CI](https://github.com/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations/actions/workflows/ci.yml/badge.svg)](https://github.com/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations/actions/workflows/ci.yml)
![Jellyfin](https://img.shields.io/badge/Jellyfin-10.11.9%2B%20%7C%2012.0%2B-00a4dc)
[![Licenza: GPL-3.0](https://img.shields.io/badge/licenza-GPL--3.0-blue)](LICENSE)

*[Read in English](README.md)*

Sostituisce il cuore dei Preferiti con un voto a 4 livelli — **Detesto (-3)**, **Guardabile (-1)**, **Bello (+1)**,
**Fantastico (+3)** — e propone consigli costruiti su ciò che ti è piaciuto *e* su ciò che non ti è piaciuto.
Tutto viene calcolato sul tuo server.

**Richiede Jellyfin 10.11.9 o successivo, oppure Jellyfin 12.0 o successivo.**

## Funzioni

- Selettore a 4 livelli nel client web al posto del cuore, nella pagina del media: è l'unico punto in cui si aggiunge,
  cambia o toglie un voto. Le locandine mostrano un piccolo badge di sola lettura sui media votati, nulla sugli altri.
- Voti rispecchiati sui campi nativi di Jellyfin `Likes`, `Rating` e Preferiti: non si perdono disinstallando il plugin.
- Consigli personali basati sui contenuti (generi, registi, attori, sceneggiatori, tag, studi, decenni, saghe),
  con spiegazioni come "Perché hai amato *X*".
- Righe in home "Consigliati per te" e "Già visti, da valutare" (con Home Screen Sections, oppure aggiunte dallo script del plugin),
  una pagina "Per te" e una riga "Simili, scelti per te" nella pagina dettaglio.
- Jellyfin 12: provider nativo dei "Simili", così i consigli arrivano anche alle app TV e mobile.
- Jellyfin 10.11: sostituzione opzionale dei suggerimenti di Jellyfin (spenta di default).
- Voti per stagione delle serie TV, in opzione.
- Importazione dei Preferiti come +3; esportazione e importazione dei voti in CSV.

## Compatibilità

| Jellyfin | Build del plugin | Note |
|---|---|---|
| 10.11.0 – 10.11.8 | non supportata | aggiorna Jellyfin almeno alla 10.11.9 |
| 10.11.9 – 10.11.x | `jf10.11` (targetAbi 10.11.9.0) | provato su 10.11.11 |
| 12.0 e successive | `jf12` (targetAbi 12.0.0.0) | provato su 12.4 |

Jellyfin sceglie da solo la build giusta dal repository.

## Installazione

1. **Pannello di controllo → Plugin → Repository** (in 10.11: **Catalogo** → icona impostazioni) → **+**.
   Nome `Ratings & Recommendations`, URL:
   ```
   https://raw.githubusercontent.com/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations/main/manifest.json
   ```
2. Aggiungi il repository di **File Transformation** (serve per i pollici nel client web) e, se vuoi le righe in home,
   di **Home Screen Sections**. Entrambi (e Plugin Pages) stanno nello stesso repository:
   ```
   https://www.iamparadox.dev/jellyfin/plugins/manifest.json
   ```
3. **Catalogo** → installa **File Transformation**, poi **Ratings & Recommendations** (e se vuoi **Home Screen Sections**) → riavvia Jellyfin.
4. Apri **Pannello di controllo → Plugin → Ratings & Recommendations**: la pagina impostazioni mostra se le dipendenze sono state trovate.
5. Ricarica una volta il client web con **Ctrl+F5** (il browser potrebbe tenere in cache la pagina vecchia).

Gli aggiornamenti compaiono da soli nel pannello di controllo.

**Senza File Transformation** l'API, i consigli e il provider di Jellyfin 12 funzionano lo stesso; la pagina impostazioni
spiega come aggiungere lo script a mano.

## Righe in home e pagina "Per te"

### Con Home Screen Sections

Installa **Home Screen Sections** e **Plugin Pages** (Plugin Pages aggiunge la voce *Modular Home* che serve agli
utenti), poi:

1. **Amministratore:** Pannello di controllo → Plugin → **Home Screen Sections** → *Global Settings*:
   - **Enabled by Default** attiva la home modulare per tutti gli utenti (dopo l'installazione è spento, quindi
     altrimenti ogni utente deve attivarla da sé);
   - **Allow User Override** (acceso di default) permette agli utenti di attivarla o spegnerla e di scegliere le sezioni.

   La scheda *Section Settings* sceglie quali sezioni esistono, l'ordine predefinito e se gli utenti possono cambiarle
   una per una ("Let the user override this section"). Salva; ricarica il client web con Ctrl+F5.
2. **Ogni utente** (serve se *Enabled by Default* è spento, o per scegliere le sezioni): apri **Modular Home**:
   - **layout Legacy** (Jellyfin 10.11, oppure 12 con Impostazioni → Schermo → Layout "Desktop (legacy)"): menu laterale;
   - **layout Modern** (predefinito in Jellyfin 12): menu dell'avatar in alto a destra (sul telefono: menu laterale ☰).
3. Spunta **Enable "Modular Home"**, poi sotto **Enabled Sections** spunta **Consigliati per te** e **Già visti, da valutare**
   (e le altre sezioni che vuoi) → **Save** → torna alla home.

### Senza Home Screen Sections

Lo script del plugin aggiunge da solo le due righe in cima alla home (anche quando Home Screen Sections è installato ma
non attivato).

**"Già visti, da valutare"** elenca ciò che hai visto ma non hai ancora votato, anche quello visto prima di installare il plugin
(lo stato "visto" di Jellyfin): film visti, serie con almeno un episodio visto (modalità "Serie intera") e stagioni con
tutti gli episodi visti (modalità "Per stagione"; con "Entrambe" serie e stagioni). Prima i visti più di recente. Apri
il media e votalo dalla sua pagina: uscirà dalla riga al prossimo caricamento. Se non c'è nulla da votare, la riga
non compare.

### Dove trovare la pagina "Per te"

| Layout | Punto di accesso |
|---|---|
| Legacy (10.11, 12 "Desktop (legacy)") | menu laterale, sotto Home |
| Modern, finestra larga (predefinito in 12) | barra in alto, prima delle librerie, e menu dell'avatar |
| Modern, finestra stretta / telefono | menu laterale (☰), sotto Home |

Ci si arriva anche dal titolo delle righe, che porta alla sezione corrispondente (`#/ratingsrecs?section=recommended`
oppure `?section=rate`). Le pagine di film e serie hanno la riga **Simili, scelti per te** sotto
"Altri come questo".

## Jellyfin 12: "Simili" nativi

Su Jellyfin 12 il plugin può ordinare col gusto di ogni utente gli elenchi "Altri come questo" nativi di film e serie,
che compaiono anche nelle app TV e mobile.

1. Pannello di controllo → Plugin → Ratings & Recommendations → **Consegna** → spunta le librerie (film, serie o miste) → Salva.
2. Il plugin mette il suo provider per primo nell'ordine dei provider dei simili di quelle librerie (Pannello di
   controllo → Librerie → libreria → provider dei simili) e lo toglie dalle altre. La pagina impostazioni mostra dove è attivo.
3. Per spegnerlo togli la spunta e salva: tornano gli elenchi di Jellyfin. **Prima di disinstallare il plugin togli tutte
   le spunte e salva.**

Limiti:
- I titoli votati negativamente non compaiono. In una libreria con meno titoli di quanti l'elenco ne chiede finiscono
  in fondo invece di sparire, perché Jellyfin li aggiungerebbe di nuovo in posizioni qualsiasi.
- I consigli film di Jellyfin ("Perché hai guardato…") usano solo il provider di Jellyfin e non cambiano.
- Jellyfin 10.11 non ha questo provider: la riga "Simili, scelti per te" del plugin funziona su entrambe le versioni.

## Jellyfin 10.11: suggerimenti (opzionale)

Jellyfin 10.11 non ha il provider dei simili, quindi le app TV e mobile mostrano i suggerimenti di Jellyfin. Come
opzione **facoltativa e spenta di default** (Consegna → "Jellyfin 10.11: suggerimenti") il plugin può cambiare due
elenchi di Jellyfin per tutti i client:

- `/Items/Suggestions` per film e serie → sostituito con i consigli dell'utente;
- `/Movies/Recommendations` ("Perché hai guardato…") → ogni elenco riordinato col gusto dell'utente, senza i titoli
  votati negativamente.

Funziona modificando le risposte di Jellyfin, quindi dipende da dettagli interni: se qualcosa va storto si spegne da
solo, scrive l'errore nel log e lo mostra nella pagina impostazioni. Su Jellyfin 12 usa invece il provider nativo qui sopra.

## Configurazione

Pannello di controllo → Plugin → Ratings & Recommendations. Gruppi: Voti (nomi e valori dei livelli, Preferiti,
segnali impliciti, modalità di voto delle serie), Motore (pesi per categoria, smorzamento, emivita, coefficienti del
punteggio, diversità), Consegna (righe in home, provider di Jellyfin 12, override dei suggerimenti di 10.11),
Strumenti (ricalcolo, importa Preferiti, CSV, anteprima).

### Voti per stagione: controlla prima i metadati

Le modalità "Per stagione" ed "Entrambe" danno risultati migliori solo se le stagioni hanno **crediti per episodio**
(registi, sceneggiatori, guest star). Con metadati scarsi, frequenti nei DVD rippati e nominati a mano, si comportano
quasi come "Serie intera". Aggiorna i metadati delle serie da TMDb o TVDb prima di attivarle; la pagina impostazioni
mostra la percentuale di stagioni senza crediti per episodio.

Cambiare modalità non cancella mai i voti: quelli che la modalità attiva non usa restano salvati e tornano validi se
torni indietro. Se gli utenti possono scegliere, ognuno sceglie la modalità dalla pagina "Per te". Per le serie
antologiche (ogni stagione è a sé) aggiungi il tag `antologia` (o `anthology`) alla serie dall'editor dei metadati:
i consigli indicheranno da quale stagione partire invece della stagione 1.

## Privacy

Il plugin **non fa chiamate di rete in uscita** e **non raccoglie statistiche d'uso**. I voti restano nella cartella
dati del plugin sul tuo server (un file JSON per utente) e nel database di Jellyfin.

## Migrazione da Jellyfin 10.11 a 12

Jellyfin consiglia di rimuovere i plugin di terze parti prima di un aggiornamento importante:

1. Pagina impostazioni → **Esporta CSV** (copia di tutti i voti).
2. Aggiorna Jellyfin alla 12, poi installa il plugin dal catalogo (la build `jf12` viene scelta da sola).
3. Pagina impostazioni → **Importa CSV**.

I voti sono comunque rispecchiati sui campi `Likes`/`Rating` di Jellyfin, che sopravvivono all'aggiornamento.

## Domande frequenti

**I pollici non compaiono.** Controlla che File Transformation sia installato e attivo, poi ricarica la pagina con
Ctrl+F5. Apri la console del browser (F12) e cerca le righe che iniziano con `[RatingsRecs]`.

**Le app TV e mobile mostrano i pollici?** No: lì resta il cuore, che vale +3. Su Jellyfin 12 gli elenchi "Simili"
di quelle app usano i tuoi voti.

**"Modular Home" (o un'altra voce di Plugin Pages) mostra "Pagina non trovata" e l'indirizzo è
`#/userpluginsettings?pageUrl=…` senza `.html`.** Non dipende da questo plugin. Il browser sta usando una copia
vecchia di `main.jellyfin.bundle.js` senza la pagina aggiunta da Plugin Pages (`userpluginsettings.html`), e il client
web riscrive gli indirizzi `….html` sconosciuti togliendo `.html`. Per qualche secondo dopo ogni avvio di Jellyfin (e
prima di installare Plugin Pages) quel file viene servito non modificato e **senza `Cache-Control`**: il browser può
tenerlo un giorno o più senza richiederlo al server, quindi una ricarica normale non basta e aggiungere `.html` nemmeno.
Soluzione, con Jellyfin avviato del tutto:
- Chrome / Edge: apri gli strumenti per sviluppatori (F12), clic destro sul pulsante di ricarica →
  **Svuota la cache e ricarica forzatamente**;
- oppure cancella i dati del sito per l'indirizzo di Jellyfin nel browser (dovrai accedere di nuovo);
- oppure, nella console degli strumenti per sviluppatori sulla pagina di Jellyfin, esegui:
  ```js
  fetch([...document.scripts].find(s => /main\.jellyfin\.bundle/.test(s.src)).src, { cache: 'reload' }).then(() => location.reload())
  ```
Evita di aprire il client web nei primi secondi dopo il riavvio di Jellyfin.

**Dopo un aggiornamento la pagina impostazioni sembra vecchia o mostra chiavi non tradotte.** Il browser ha tenuto la
pagina precedente: ricarica il pannello di controllo con Ctrl+F5.

**La pagina impostazioni dice "This is the build for Jellyfin 10.11 but the server runs 12".** È stato installato a
mano lo zip per l'altra versione di Jellyfin: rimuovi il plugin e installalo dal catalogo, che sceglie la build giusta.

**Gli altri utenti vedono i miei voti?** No. Ogni utente ha voti e consigli separati; solo gli amministratori possono
esportare i voti degli altri.

## Compilare in locale

Requisiti: .NET SDK 9 e 10.

```bash
dotnet test Jellyfin.Plugin.RatingsRecommendations.sln -c Release
./build.ps1   # Windows PowerShell 5.1 / PowerShell 7
./build.sh    # Linux/macOS (serve zip)
```

Gli zip finiscono in `artifacts/`. Per i server Docker di prova vedi [CONTRIBUTING.md](CONTRIBUTING.md).

## Licenza

[GPL-3.0](LICENSE). Documento di design: [docs/DESIGN.md](docs/DESIGN.md).
