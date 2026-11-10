# Jellyfin Ratings & Recommendations — Documento di design del plugin

Nov 10, 2026 · @Davide

## 1. Panoramica

**Ratings & Recommendations** è un plugin per Jellyfin 10.11.11 e Jellyfin 12.x che sostituisce il cuore dei Preferiti con una valutazione a 4 livelli e usa quei voti per ordinare i consigli di visione di ogni utente.

**Il problema.** Jellyfin è pensato per chi carica solo media che conosce e che gli piacciono. Una libreria nata dal ripping dei DVD e Blu-ray di genitori, parenti e amici arriva a quasi 2000 film e serie, di cui circa il 90% sconosciuti. Oggi l'unico segnale esplicito è il cuore: dice "mi piace", ma non esiste un modo per dire "non mi è piaciuto", quindi un film detestato pesa sui consigli quanto uno mai visto.

**La soluzione in una frase.** Ogni media visto riceve un voto da -3 a +3; il plugin costruisce un profilo di gusto per utente (generi, registi, attori, decenni…) e propone per primi i media non visti più vicini a ciò che è piaciuto e più lontani da ciò che è stato bocciato.

**Che documento è questo.** Per le app e i plugin l'equivalente del Game Design Document si chiama **PRD** (Product Requirements Document, il *cosa* e il *perché*) unito a un **TDD** (Technical Design Document, il *come*). Questo file li fonde: le sezioni 2–5 e 7 sono il PRD, le sezioni 6, 8 e 9 sono il TDD. Va messo nel repository come `docs/DESIGN.md` e dato a Claude Code insieme al prompt.

> Nota sulla documentazione ufficiale: Jellyfin non ha una guida completa per i plugin. Il punto di partenza ufficiale è il [jellyfin-plugin-template](https://github.com/jellyfin/jellyfin-plugin-template); il resto si ricava dal codice del server e dai plugin open source citati nelle fonti.

## 2. Obiettivi, non-obiettivi e metriche

Il plugin ha successo se, dopo circa 30 voti, almeno metà dei primi 10 consigli sono media che l'utente avrebbe voglia di guardare.

**Obiettivi (v1)**

1. Valutare film, serie TV e, in opzione, singole stagioni con 4 livelli: Detesto (-3), Guardabile (-1), Bello (+1), Fantastico (+3), più "nessun voto".
2. Sostituire il cuore dei Preferiti nel client web (pagina dettaglio, e card dove possibile) con il selettore a 4 pollici.
3. Ordinare i consigli per utente usando i voti, con i voti negativi che spingono in basso i media simili.
4. Mostrare i consigli dove l'utente li vede davvero: riga "Consigliati per te" in home, pagina dedicata, e su Jellyfin 12 i "Simili" nativi.
5. Un solo codice sorgente, due build: Jellyfin 10.11.x (.NET 9) e Jellyfin 12.x (.NET 10).
6. Nessun servizio esterno: tutto calcolato sul server, nessun dato esce.

**Non-obiettivi (v1)**

- Voti sui singoli episodi (le stagioni invece si possono votare in opzione, vedi 4.1).
- Modificare le app native (Android TV, iOS, Swiftfin, Findroid, Roku): ricevono i consigli ma non mostrano i pollici.
- Filtraggio collaborativo tra utenti ("chi ha amato X ha amato Y"): con pochi utenti in famiglia non ha dati sufficienti; possibile v2.
- Consigliare titoli che non sono in libreria (integrazione Jellyseerr): fuori ambito.
- Musica, libri, foto.

**Metriche di successo**

| Metrica | Obiettivo | Come si misura |
| --- | --- | --- |
| Precisione dei primi 10 consigli | ≥ 50% "lo guarderei" dopo 30 voti | Test manuale: l'utente segna i primi 10 |
| Tempo per votare | ≤ 2 clic dalla pagina dettaglio | Prova d'uso |
| Tempo di calcolo consigli | < 2 s per utente su 2000 media | Log del task pianificato |
| Risposta dell'endpoint consigli | < 200 ms (da cache) | Log / strumenti del browser |
| Media votati negativamente nei consigli | 0 | Test automatico |

## 3. Utente tipo e casi d'uso

L'utente tipo è il proprietario di un server domestico con una libreria grande ed "ereditata", più qualche familiare con un proprio account.

**Persona.** Studente, gestisce Jellyfin a casa, ha \~2000 media rippati da dischi di famiglia e amici, ne conosce il 10%. Guarda soprattutto dal browser e dal PC; altri in casa usano la TV.

**User stories**

1. Come utente, finito un film, voglio dargli un voto da -3 a +3 con un clic dalla pagina del film, così Jellyfin impara cosa mi piace.
2. Come utente, voglio cambiare o togliere un voto in qualsiasi momento.
3. Come utente, voglio una riga "Consigliati per te" in home che cambia man mano che voto.
4. Come utente, voglio che un film che ho detestato non mi venga più proposto, e che i film molto simili scendano in classifica.
5. Come utente nuovo con pochi voti, voglio una riga "Valuta questi" con media vari e già visti (o noti) per calibrare il profilo in fretta.
6. Come utente, voglio vedere perché un media mi viene consigliato ("Perché hai amato *Alien*", "Regista: Ridley Scott").
7. Come utente che guarda dalla TV, voglio che il cuore che uso lì conti comunque come voto positivo.
8. Come amministratore, voglio regolare i pesi (generi, registi, attori…) e i valori dei voti dalla pagina impostazioni del plugin.
9. Come amministratore, voglio importare i Preferiti esistenti come voti +3 e poter esportare/importare i voti in CSV (backup e migrazione 10.11 → 12).
10. Come amministratore, voglio che ogni utente abbia voti e consigli separati e che restino valide le restrizioni di libreria e il parental control.

## 4. Sistema di valutazione

Ogni coppia utente–media ha al massimo un voto tra quattro; il voto è salvato dal plugin e rispecchiato nei campi nativi di Jellyfin, così non si perde se il plugin viene disinstallato.

| Icona | Nome (configurabile) | Valore nel profilo | Rispecchiato su `Likes` | Rispecchiato su `Rating` (0–10) | Effetto sui Preferiti |
| --- | --- | --- | --- | --- | --- |
| 👎👎 | Detesto | -3 | false | 2 | rimuove dai Preferiti |
| 👎 | Guardabile | -1 | false | 4 | rimuove dai Preferiti |
| 👍 | Bello | +1 | true | 7 | nessuno |
| 👍👍 | Fantastico | +3 | true | 10 | aggiunge ai Preferiti (opzione, attiva di default) |
| — | Nessun voto | 0 (escluso) | null | null | nessuno |

**Regole**

- **Cosa si vota:** film (`Movie`) e serie (`Series`). Le stagioni (Season) si votano solo nelle modalità "Per stagione" ed "Entrambe" (sezione 4.1); gli episodi non si votano e lì il pulsante non compare.
- **Cambio voto:** cliccare un altro livello sostituisce il voto; cliccare il livello già attivo lo toglie.
- **Timestamp:** ogni voto salva data e ora (UTC) di creazione e di ultima modifica; servono per il decadimento nel tempo (sezione 5).
- **Segnali impliciti** (pesi minori, configurabili): media visto fino in fondo senza voto = +0,5; media abbandonato (avanzamento < 20% e fermo da 30+ giorni) = -0,5; Preferito impostato da un client senza pollici (es. app TV) e senza voto = +3.
- **Priorità:** un voto esplicito vince sempre su un segnale implicito per lo stesso media.
- **Valori e nomi** sono modificabili dall'amministratore; i valori di default sono quelli della tabella.
- **Perché si rispecchia sui campi nativi:** `UserItemData.Likes` e `UserItemData.Rating` esistono già nell'API di Jellyfin (`POST /UserItems/{itemId}/UserData`). Rispecchiarli rende i voti visibili ad altri plugin che leggono i rating (es. Reco+) e li conserva nel database di Jellyfin. La fonte di verità resta però l'archivio del plugin, perché i campi nativi non hanno data e possono essere sovrascritti da altri client.

### 4.1 Modalità di voto delle serie TV

L'amministratore sceglie come si votano le serie; il default è **Serie intera**, come su Netflix e Prime Video.

| Modalità | Dove compare il selettore | Voto che usa il motore per una stagione |
| --- | --- | --- |
| **Serie intera** (default) | Pagina della serie | Il voto della serie, uguale per tutte le stagioni |
| **Per stagione** | Pagina di ogni stagione e card delle stagioni nella pagina della serie; non sulla serie | Il voto di quella stagione; stagioni senza voto escluse dal profilo |
| **Entrambe** | Pagina della serie e pagine delle stagioni | Il voto della stagione se c'è, altrimenti quello della serie |

- **Scelta per utente:** opzione "Gli utenti possono scegliere la propria modalità" (default sì). L'utente la cambia dalle preferenze della pagina "Per te"; senza scelta vale quella dell'amministratore.
- **Cambio di modalità:** i voti non vengono mai cancellati. Quelli che la modalità attiva non usa (es. voti di stagione in "Serie intera") restano salvati e tornano validi se si cambia di nuovo. Passando a "Per stagione", la pagina della serie offre un pulsante "Copia il voto della serie sulle stagioni non votate".
- **Segnali impliciti per stagione:** stagione vista tutta senza voto = +0,5; stagione abbandonata (meno del 20% degli episodi visti e ferma da 30+ giorni) = -0,5.
- **Archivio:** i voti di stagione usano lo stesso schema degli altri, con `itemType: "Season"` e in più `seriesId`.
- **Avviso sui metadati:** la qualità della modalità per stagione dipende dai crediti per episodio (registi, sceneggiatori, guest star). La pagina impostazioni mostra la percentuale di stagioni senza crediti (es. "il 40% delle stagioni non ha crediti per episodio"); il README spiega il limite e consiglia di aggiornare i metadati da TMDb o TVDb prima di usare questa modalità.

## 5. Algoritmo di raccomandazione

Il motore è **content-based**: trasforma i voti in un profilo di gusto fatto di "caratteristiche" (generi, persone, decenni…) con un peso positivo o negativo, poi dà un punteggio a ogni media non visto sommando i pesi delle sue caratteristiche. Il codice del motore non dipende da Jellyfin: riceve liste di oggetti semplici e restituisce una classifica, così si testa in isolamento.

### 5.1 Caratteristiche di un media

| Categoria | Da dove (Jellyfin) | Peso categoria α (default) | Limite |
| --- | --- | --- | --- |
| Generi | `Genres` | 1,0 | tutti |
| Regista | `People` con tipo Director | 1,2 | tutti |
| Attori | `People` con tipo Actor | 0,8 | primi 5 in ordine di cast |
| Sceneggiatori | `People` con tipo Writer | 0,6 | primi 3 |
| Tag / parole chiave | `Tags` | 0,5 | tutti |
| Studio / rete TV | `Studios` | 0,3 | primi 2 |
| Decennio | `ProductionYear` → "1980s" | 0,4 | 1 |
| Saga / collezione | `BoxSet` che contiene il media | 1,0 | tutte |

Ogni caratteristica ha anche un peso di rarità (IDF): una caratteristica presente in pochi media (un regista) conta più di una presente ovunque ("Dramma").

```latex
\mathrm{idf}(f) = \ln\left(\frac{N}{1 + n_f}\right) + 1
```

N = media totali nella libreria dell'utente, n\_f = media che hanno la caratteristica f.

### 5.2 Profilo di gusto

Per ogni caratteristica f il profilo è la media "prudente" dei voti dei media che la contengono. La costante k (default 2) evita che un solo voto renda estremo un attore visto una volta; d(t) dimezza il peso di un voto ogni 365 giorni (configurabile, 0 = nessun decadimento).

```latex
P(f) = \frac{\sum_{i \ni f} v_i \cdot d(t_i)}{\sum_{i \ni f} d(t_i) + k}, \qquad d(t) = 0{,}5^{\,\text{giorni}(t)/365}
```

v\_i = valore del voto (o del segnale implicito) del media i.

### 5.3 Punteggio di un candidato

Candidati = film e serie a cui l'utente ha accesso, non visti (`Played = false`; per le serie: nessun episodio visto) e senza voto. Per ogni categoria c si sommano i pesi delle caratteristiche del candidato, divisi per la radice del loro numero (così un film con 30 tag non vince solo per quantità):

```latex
S_{\text{gusto}}(x) = \sum_{c} \alpha_c \cdot \frac{\sum_{f \in x_c} P(f) \cdot \mathrm{idf}(f)}{\sqrt{|x_c|}}
```

Il punteggio finale combina gusto, qualità generale ed esplorazione; i tre termini sono portati tra 0 e 1 prima della somma:

```latex
S(x) = 0{,}75 \cdot \hat{S}_{\text{gusto}}(x) + 0{,}15 \cdot \frac{\text{CommunityRating}(x)}{10} + 0{,}10 \cdot \varepsilon_{\text{giorno}}(x)
```

ε è un numero casuale stabile per utente e giorno (seed = utente + data), così la lista non cambia a ogni refresh ma si rinnova ogni giorno. Tutti i coefficienti sono impostazioni.

### 5.4 Regole dopo il punteggio

1. **Esclusioni dure:** media votati, media visti, media fuori dalle librerie o dal parental control dell'utente, altri capitoli di una saga votata -3 (opzione).
2. **Penalità di vicinanza:** se un candidato condivide regista o saga con un media votato -3, il punteggio di gusto è moltiplicato per 0,5.
3. **Diversità (MMR):** riordino dei primi 50 con λ = 0,8, così la top 20 non è fatta di un solo genere; massimo 2 media della stessa saga nei primi 20.
4. **Spiegazione:** per ogni consiglio si salvano le 2 caratteristiche che hanno contribuito di più ("Regista: Ridley Scott", "Perché hai amato *Alien*").

### 5.5 Partenza a freddo

Con meno di 5 voti espliciti il punteggio usa 0,3 di gusto, 0,5 di qualità e 0,2 di esplorazione, e la home mostra la riga **"Valuta questi"**: 20 media scelti per coprire generi diversi, prima quelli già visti (`Played = true`) ma non votati, poi i più noti (CommunityRating e numero di voti alti).

### 5.6 Esempio numerico

L'utente ha votato *Alien* +3 (fantascienza, horror; regia Ridley Scott), *Prometheus* -1 (fantascienza, horror; Ridley Scott) e *Mamma Mia!* -3 (musical, commedia). Per semplicità: IDF = 1, nessun decadimento, k = 2.

| Caratteristica | Calcolo | P(f) |
| --- | --- | --- |
| Fantascienza | (3 − 1) / (2 + 2) | +0,50 |
| Ridley Scott | (3 − 1) / (2 + 2) | +0,50 |
| Musical | −3 / (1 + 2) | −1,00 |
| Commedia | −3 / (1 + 2) | −1,00 |

*Blade Runner* (fantascienza, thriller; Ridley Scott): generi 1,0 × (0,5 + 0) / √2 = 0,35; regia 1,2 × 0,5 = 0,60; gusto = **+0,95**. Un altro musical-commedia: 1,0 × (−1 − 1) / √2 = **−1,41**, finisce in fondo. Il voto -1 su *Prometheus* ha abbassato Ridley Scott da +1,0 a +0,5 senza cancellarlo: è esattamente l'effetto che il cuore da solo non permetteva.

### 5.7 Motore con le stagioni

Nelle modalità "Per stagione" ed "Entrambe" il motore tratta ogni stagione come un media a sé: il profilo impara da singole stagioni e una serie candidata viene giudicata dalle sue stagioni migliori. Film, serie e stagioni condividono un unico profilo (un regista apprezzato in un film conta anche per gli episodi che ha diretto).

**Caratteristiche di una stagione**

| Categoria | Origine | Peso α (default) |
| --- | --- | --- |
| Generi, tag, studio | Dalla serie | come in 5.1 |
| Cast principale | Dalla serie (primi 5) | 0,8 |
| Registi | Unione dei registi degli episodi della stagione | 1,2 |
| Sceneggiatori | Unione degli sceneggiatori degli episodi | 1,0 (più alto che per i film: nelle serie spesso guidano lo stile) |
| Guest star | Le 5 più presenti negli episodi della stagione | 0,6 |
| Decennio | `ProductionYear` della stagione o data del primo episodio | 0,4 |
| Qualità | Media del `CommunityRating` degli episodi | usata nel termine qualità di 5.3 |

Se una stagione non ha crediti per episodio, le sue caratteristiche sono quelle della serie: in pratica quella stagione si comporta come in modalità "Serie intera".

**Punteggio di una serie candidata.** Si calcola S(x) per ogni stagione non vista con le formule di 5.3, poi:

```latex
S_{\text{serie}} = 0{,}6 \cdot \max_{s} S(s) + 0{,}4 \cdot \frac{1}{n}\sum_{s} S(s)
```

Così una serie con anche una sola stagione molto vicina ai gusti sale, ma una serie con una sola stagione buona su otto non batte una serie buona dall'inizio alla fine. I pesi 0,6 / 0,4 sono impostazioni.

**Spiegazione.** Per la stagione candidata migliore si cerca la stagione votata +1 o +3 più simile (similarità del coseno sui vettori di caratteristiche) e si mostra, ad esempio: "La stagione 1 di *X* somiglia alla stagione 2 di *Y*, che hai trovato Fantastica — stesso sceneggiatore".

**Da dove partire.** Di default si consiglia sempre di iniziare dalla stagione 1. Solo per le serie antologiche (tag "anthology" o "antologia", o opzione impostata a mano sulla serie) la spiegazione indica la stagione con il punteggio più alto come punto di partenza.

**Esempio.** L'utente vota la stagione 2 di una serie poliziesca +3 (sceneggiatore A, regista B) e la stagione 4 della stessa serie -1 (sceneggiatore C). Generi e cast sono gli stessi per entrambe e si compensano; restano A e B positivi, C negativo. Una serie diversa la cui stagione 1 è scritta da A sale in classifica, anche se di genere diverso.

## 6. Architettura tecnica

Un plugin .NET lato server fa tutto il lavoro (archivio voti, motore, API, consegna dei consigli); uno script JavaScript iniettato nel client web disegna i pollici. Lo stesso codice si compila due volte, una per Jellyfin 10.11 e una per Jellyfin 12.

&#91;embedded content: architettura · dal voto ai consigli\]

Il voto entra dall'API, viene salvato e rispecchiato nei dati nativi; il motore ricalcola la cache, che alimenta riga in home, pagina "Per te" e, solo su Jellyfin 12, i "Simili" nativi che arrivano anche alle app.

### 6.1 Versioni e build

| Server | .NET | Pacchetti NuGet | `targetAbi` | Simbolo di compilazione |
| --- | --- | --- | --- | --- |
| Jellyfin 10.11.x (test su 10.11.11) | net9.0 | `Jellyfin.Controller` / `Jellyfin.Model` 10.11.\* | 10.11.0.0 | `JF10_11` |
| Jellyfin 12.x (test su 12.0–12.2) | net10.0 | `Jellyfin.Controller` / `Jellyfin.Model` 12.\* | 12.0.0.0 | `JF12` |

- Le build sono **due zip distinti**. Un plugin per 10.11 non si carica su 12: Jellyfin 12 è passato a .NET 10 e richiede di ricompilare i plugin.
- Il `manifest.json` del repository dei plugin elenca entrambe le versioni con il rispettivo `targetAbi`; Jellyfin installa da solo quella giusta.
- I riferimenti ai pacchetti Jellyfin sono **solo in compilazione** (`ExcludeAssets="runtime"`): nello zip va solo la DLL del plugin.
- Le differenze di API tra 10.11 e 12 si isolano dietro interfacce interne (`ILibraryGateway`, `IUserDataGateway`) con due implementazioni sotto `#if JF12`. Esempio noto: in 12 `IUserManager.Users` diventa il metodo `GetUsers()`.

### 6.2 Struttura della soluzione

```
Jellyfin.Plugin.RatingsRecommendations.sln
├─ src/RatingsRecs.Engine/            motore puro (.NET, nessuna dipendenza Jellyfin)
├─ src/Jellyfin.Plugin.RatingsRecs/   integrazione: Plugin, API, archivio, task, provider, web
│   ├─ Web/ratings.js, ratings.css    script iniettato nel client web
│   └─ Configuration/configPage.html  pagina impostazioni
├─ tests/RatingsRecs.Engine.Tests/    xUnit sul motore
├─ tests/RatingsRecs.Plugin.Tests/    xUnit sull'integrazione (archivio, API)
├─ docker/compose.10.11.yml, compose.12.yml   server di prova
├─ build.yaml, manifest.json, docs/DESIGN.md, CLAUDE.md
```

### 6.3 Componenti

| Componente | Responsabilità | Interfaccia Jellyfin usata |
| --- | --- | --- |
| `Plugin` | Identità, configurazione, pagina impostazioni | `BasePlugin<PluginConfiguration>`, `IHasWebPages` |
| `ServiceRegistrator` | Registra i servizi nel container DI | `IPluginServiceRegistrator` |
| `RatingStore` | Legge/scrive i voti per utente, scrittura atomica | file nella cartella dati del plugin |
| `NativeMirror` | Copia il voto su `Likes`, `Rating`, `IsFavorite` | `IUserDataManager` |
| `RatingsController` | API REST dei voti e dei consigli | `ControllerBase` ASP.NET Core, `[Authorize]` |
| `RecommendationService` | Prepara i dati, chiama il motore, tiene la cache | `ILibraryManager`, `IUserManager` |
| `RecomputeTask` | Ricalcolo pianificato (ogni 6 h) e al cambio voto | `IScheduledTask` |
| `WebInjector` | Inserisce lo script nel client web | plugin File Transformation (via reflection) |
| `HomeSectionsBridge` | Registra le righe home | plugin Home Screen Sections (opzionale) |
| `SimilarItemsProvider` (solo 12) | "Simili" nativi e consigli film basati sul gusto | `ILocalSimilarItemsProvider` |
| `UserDataListener` | Intercetta cuore e "visto" da altri client | evento `IUserDataManager.UserDataSaved` |

### 6.4 Archiviazione

- File JSON per utente: `<cartella dati plugin>/ratings/<userId>.json`, scrittura atomica (file temporaneo + rename) e lock per utente. Con 2000 media e pochi utenti un file è più semplice e robusto di un database.
- Schema di un voto: `{ itemId, itemType, value, source: "explicit"|"implicit", createdUtc, updatedUtc }`, più `schemaVersion` a livello di file per migrazioni future.
- Cache dei consigli: in memoria, per utente, con hash dei voti; invalidata quando cambia un voto o finisce una scansione della libreria.
- Rimozione di un media dalla libreria: il voto resta nel file (serve per il profilo) ma il media non compare più nei candidati.

### 6.5 API REST (prefisso `/RatingsRecs`)

| Metodo e percorso | Uso |
| --- | --- |
| `GET /RatingsRecs/Ratings/{itemId}` | Voto corrente dell'utente autenticato |
| `GET /RatingsRecs/Ratings?itemIds=a,b,c` | Voti in blocco (per le card) |
| `PUT /RatingsRecs/Ratings/{itemId}` body `{ "value": -3 }` | Imposta o cambia il voto |
| `DELETE /RatingsRecs/Ratings/{itemId}` | Toglie il voto |
| `GET /RatingsRecs/Recommendations?limit=20&type=Movie` | Consigli con spiegazione |
| `GET /RatingsRecs/Onboarding?limit=20` | Riga "Valuta questi" |
| `GET /RatingsRecs/Export` · `POST /RatingsRecs/Import` | CSV dei voti (solo admin per altri utenti) |
| `POST /RatingsRecs/ImportFavorites` | Converte i Preferiti in voti +3 |

Endpoint aggiuntivo per la riga della pagina dettaglio: `GET /RatingsRecs/Similar/{itemId}?limit=12` restituisce i media simili a quello aperto, ordinati col profilo di gusto dell'utente.

Tutte le chiamate richiedono autenticazione; l'utente viene dal token, mai da un parametro, salvo per gli admin. Le risposte dei consigli restituiscono `BaseItemDto` standard (tramite `IDtoService`), così il client web li disegna come card normali.

### 6.6 Come arrivano i consigli all'utente

1. **Riga "Consigliati per te" in home** — Home Screen Sections non ha una sezione equivalente tra quelle predefinite, ma permette ad altri plugin di registrarne di proprie. Il plugin registra due sezioni nuove, "Consigliati per te" e "Valuta questi"; l'utente le aggiunge dalle impostazioni di Home Screen Sections e sceglie in che posizione mostrarle (es. sotto "Continua a guardare"). Funziona su 10.11 e 12. Senza Home Screen Sections, lo script web aggiunge da solo le righe in cima alla home.
2. **Pagina "Per te"** — pagina a tutto schermo del plugin, con schede Film / Serie e la spiegazione di ogni consiglio. Si raggiunge da tre punti: una voce "Per te" che lo script aggiunge al menu laterale; il titolo della riga in home, reso cliccabile; oppure, se installato, il plugin Plugin Pages (stesso autore di Home Screen Sections), che aggiunge al menu le pagine dei plugin.
3. **Riga "Simili, scelti per te" nella pagina dettaglio (pensata per 10.11)** — lo script aggiunge, sotto la riga standard "Altri come questo", una riga di 12 media vicini al media aperto, riordinati col profilo di gusto ed esclusi quelli votati negativamente. Usa l'endpoint `GET /RatingsRecs/Similar/{itemId}`, calcolato al volo con lo stesso motore limitando i candidati ai 200 media con più caratteristiche in comune. Su Jellyfin 12, con il provider nativo attivo (punto 4), la riga aggiunta si nasconde perché "Altri come questo" è già personalizzata; un'opzione permette di tenerle entrambe.
4. **Solo Jellyfin 12: provider nativi** — Jellyfin 12 ha introdotto i provider di similarità e raccomandazione (`ISimilarItemsProvider` e derivati, scelti per libreria con `LibraryOptions.SimilarItemProviders`); `ISimilarItemsManager` alimenta anche i consigli film. Il plugin registra un `ILocalSimilarItemsProvider` che riordina "Simili" e "Perché hai guardato…" col profilo di gusto. Questo è l'unico canale che arriva anche alle app TV e mobile.
5. **Solo 10.11, opzionale e disattivato di default** — un filtro ASP.NET Core sulle risposte di `/Items/Suggestions` e `/Movies/Recommendations` per sostituirne il contenuto. È fragile (dipende da dettagli interni), quindi va dietro un'opzione e si disattiva da solo se fallisce.

Su 10.11 i punti 1–3 sono visibili solo nel client web: Home Screen Sections e lo script iniettato non arrivano alle app TV e mobile.

### 6.7 Iniezione nel client web

Jellyfin non offre un'API per aggiungere pulsanti all'interfaccia. Il metodo usato dai plugin attuali è il plugin **File Transformation**, che modifica al volo i file serviti da jellyfin-web senza toccare i file su disco: il nostro plugin vi registra, via reflection, una trasformazione che aggiunge `<script src="/RatingsRecs/Web/ratings.js">` prima di `</body>` in `index.html`. File Transformation diventa una dipendenza dichiarata. Se manca, la pagina impostazioni lo segnala e mostra il tag da incollare a mano nel "Custom HTML" o in JavaScript Injector.

## 7. Interfaccia utente

Nel client web il cuore sparisce e al suo posto compare un solo pulsante "pollice" che, al passaggio del mouse o al tocco, apre una piccola barra con i 4 livelli, come su Netflix.

### 7.1 Selettore di voto

- **Stato chiuso:** un'icona che mostra il voto attuale (pollice vuoto se nessun voto; 👎👎, 👎, 👍 o 👍👍 colorati altrimenti). Tooltip con il nome del livello.
- **Stato aperto:** barra orizzontale con 4 pulsanti in ordine 👎👎 · 👎 · 👍 · 👍👍, ciascuno con etichetta sotto (Detesto, Guardabile, Bello, Fantastico). Si apre con hover (desktop), tap (touch) o Invio/Spazio (tastiera); si chiude con Esc o clic fuori.
- **Clic:** salva subito (aggiornamento ottimistico, ritorno allo stato precedente con un toast se la chiamata fallisce). Clic sul livello già attivo = togli voto.
- **Icone:** SVG inline o Material Symbols già caricati da jellyfin-web (`thumb_up`, `thumb_down`; il doppio pollice = due icone affiancate). Colori dalle variabili CSS del tema, così funziona con tutti i temi.
- **Accessibilità:** `role="radiogroup"`, ogni livello `role="radio"` con `aria-checked` ed etichetta; navigazione con frecce sinistra/destra.

### 7.2 Dove compare

| Posizione | v1 | Note |
| --- | --- | --- |
| Pagina dettaglio film/serie, al posto del cuore | Sì | Obbligatorio, layout Legacy e Modern |
| Overlay delle card (hover in griglia) | Sì, se il layout ha il cuore nell'overlay | Usa la lettura in blocco dei voti |
| Fine riproduzione (schermata "prossimo" o uscita dal player) | v2 | Popup "Com'era?" |
| App native (TV, mobile) | No | Lì resta il cuore, letto come +3 |

Il cuore va **nascosto**, non cancellato dal DOM: altre parti di jellyfin-web potrebbero cercarlo. Il codice deve trovarlo con più selettori di riserva (il layout Legacy usa i pulsanti `emby-ratingbutton`; il layout Modern di Jellyfin 12 è React e va ispezionato) e un `MutationObserver`, perché jellyfin-web è una single-page app che ridisegna la pagina senza ricaricarla.

Nelle modalità "Per stagione" ed "Entrambe" il selettore compare anche nella pagina di ogni stagione e, in versione compatta, sulle card delle stagioni nella pagina della serie (sezione 4.1).

### 7.3 Righe e pagine

- **"Consigliati per te"** — riga orizzontale di 20 card, con sotto ogni card una riga breve di spiegazione; il titolo della riga apre la pagina "Per te".
- **"Valuta questi"** — riga di 20 card con il selettore di voto sempre visibile; sparisce quando l'utente ha almeno 15 voti (soglia configurabile).
- **"Simili, scelti per te"** — nella pagina dettaglio di film e serie, sotto "Altri come questo": 12 card con spiegazione breve.
- **Pagina "Per te"** — griglia con schede Film / Serie, ordinamento per punteggio, spiegazione completa; voce "Per te" nel menu laterale.

### 7.4 Pagina impostazioni (Dashboard → Plugin → Ratings & Recommendations)

| Gruppo | Impostazioni |
| --- | --- |
| Voti | Nomi e valori dei 4 livelli; 👍👍 imposta Preferito (sì/no); peso segnali impliciti; cuore da altri client = +3 (sì/no); modalità di voto delle serie: Serie intera (default) / Per stagione / Entrambe; gli utenti possono scegliere la propria modalità (sì/no); pesi 0,6 / 0,4 del punteggio serie da stagioni; avviso sulla copertura dei crediti per episodio |
| Motore | Pesi α per categoria; k; emivita in giorni; coefficienti gusto/qualità/esplorazione; λ diversità; penalità di vicinanza |
| Consegna | Riga home (auto / Home Screen Sections / script / off); provider "Simili" (solo 12); override suggerimenti 10.11 (off) |
| Strumenti | Ricalcola ora; Importa Preferiti come +3; Esporta / Importa CSV; Anteprima consigli per un utente; stato delle dipendenze (File Transformation, Home Screen Sections) |

Lingua dell'interfaccia: italiano e inglese, secondo la lingua dell'utente in Jellyfin.

## 8. Compatibilità, rischi e limiti

Il rischio principale è l'interfaccia web: è l'unica parte che dipende da dettagli non garantiti di jellyfin-web, quindi va scritta per degradare senza rompere nulla.

| Rischio | Probabilità | Impatto | Mitigazione |
| --- | --- | --- | --- |
| Un aggiornamento di jellyfin-web cambia il DOM e i pollici spariscono | Alta | Medio | Più selettori di riserva; se nessuno trova il cuore, lo script non fa nulla e lo scrive in console; il cuore originale resta usabile |
| Layout Modern (default in 12) diverso dal Legacy | Certa | Medio | Due adattatori DOM separati nello script, testati entrambi |
| API interne di Jellyfin cambiano tra 12.0, 12.1, 12.2 | Media | Alto | Controlli all'avvio di ogni aggancio (provider, filtro, bridge): se uno fallisce si disattiva da solo e lo segnala in log e nella pagina impostazioni |
| File Transformation non installato o incompatibile | Media | Medio | Avviso in pagina impostazioni + istruzioni per l'inserimento manuale dello script |
| App native non mostrano i pollici | Certa | Basso | Cuore letto come +3; su Jellyfin 12 i "Simili" nativi portano i consigli anche lì |
| Migrazione 10.11 → 12 | Certa (prima o poi) | Medio | Jellyfin consiglia di rimuovere i plugin di terze parti prima di aggiornare: esportare i voti in CSV, aggiornare, installare la build 12, importare. I voti sono comunque rispecchiati su `Likes`/`Rating` |
| Prestazioni su librerie grandi | Bassa | Medio | Calcolo in background con cache; mai calcolo sincrono in una richiesta HTTP |
| Conflitto con altri plugin di raccomandazione (Reco+, LocalRecs) | Bassa | Medio | La pagina impostazioni rileva i plugin noti e avvisa |

**Limiti accettati in v1:** niente voti sugli episodi; niente filtraggio collaborativo; i consigli di 10.11 sulle app TV restano quelli standard di Jellyfin (salvo override opzionale).

## 9. Piano di sviluppo

Otto milestone in sequenza; ognuna si chiude solo quando i suoi criteri passano su **entrambi** i server di prova (10.11.11 e 12.x).

1. **M0 — Scheletro e doppia build.** Repository pubblico NotEnjoyingThis/jellyfin-plugin-ratings-recommendations con licenza GPL-3.0 e GUID del plugin generato una volta; progetto dal jellyfin-plugin-template, due build (net9.0 + net10.0), `build.yaml`, `manifest.json`, `docker/compose.10.11.yml` e `compose.12.yml`, workflow ci.yml, file per la community della sezione 10.5 (anche in forma iniziale).
   - Accettazione: il plugin compare come "Attivo" in Dashboard → Plugin su entrambi i server; la pagina impostazioni si apre.
2. **M1 — Archivio voti e API.** `RatingStore`, `NativeMirror`, `RatingsController`, import Preferiti, export/import CSV.
   - Accettazione: test unitari verdi; `PUT` → `GET` restituisce il voto; dopo riavvio il voto c'è ancora; `Likes`, `Rating` e `IsFavorite` cambiano come da tabella della sezione 4; un utente non può leggere i voti di un altro.
3. **M2 — Pollici nel client web.** `WebInjector` + `ratings.js`/`ratings.css`, adattatori Legacy e Modern, card overlay.
   - Accettazione: sulla pagina dettaglio di un film il cuore non si vede, il selettore salva e mostra il voto, funziona dopo navigazione interna senza ricaricare, funziona con tastiera; se il cuore non si trova non compaiono errori bloccanti.
4. **M3 — Motore e consigli.** `RatingsRecs.Engine` (sezione 5 completa), `RecommendationService`, cache, `RecomputeTask`, endpoint `Recommendations` e `Onboarding`, pagina "Per te".
   - Accettazione: test con il caso della sezione 5.6 che riproducono i numeri; nessun media votato o visto nei risultati; un media votato -3 non compare mai; 2000 media calcolati in < 2 s; risposta da cache < 200 ms.
5. **M4 — Riga in home.** `HomeSectionsBridge` (Home Screen Sections) + riga iniettata dallo script come alternativa; riga "Valuta questi"; voce "Per te" nel menu laterale; riga "Simili, scelti per te" nella pagina dettaglio con l'endpoint Similar.
   - Accettazione: le sezioni compaiono nell'elenco di Home Screen Sections e, una volta aggiunte, in home su entrambe le versioni; senza Home Screen Sections le righe compaiono comunque; si aggiornano entro 1 minuto da un nuovo voto; la voce "Per te" apre la pagina; la riga "Simili, scelti per te" appare sotto "Altri come questo" e non contiene media votati negativamente.
6. **M5 — Provider nativo (solo Jellyfin 12).** `SimilarItemsProvider` su `ILocalSimilarItemsProvider`, attivabile per libreria.
   - Accettazione: con il provider attivo, "Simili" su un film mette in testa i media col punteggio di gusto più alto e non mostra media votati -3; con il provider spento torna il comportamento standard.
7. **M6 — Rifinitura.** Override opzionale 10.11, controlli di compatibilità all'avvio, traduzioni IT/EN, README con installazione, avvertenza sui metadati delle stagioni (4.1), istruzioni di migrazione 10.11 → 12, workflow release.yml (sezione 10.4), logo, release v1.0.0.
   - Accettazione: creando il tag v1.0.0 la pipeline pubblica la Release con i due zip e aggiorna da sola il manifest; su un'installazione pulita di 10.11.11 e di 12, aggiungendo solo l'URL del manifest (e quello di File Transformation) il plugin si installa dal catalogo, si attiva e funziona; una seconda release di prova (v1.0.1) compare come aggiornamento; README verificato seguendolo da zero.

**M3b — Voti per stagione (opzione).** Da fare dopo M3 e prima di M4. Modalità di voto delle serie (4.1) nelle impostazioni e nelle preferenze utente, selettore nella pagina e sulle card delle stagioni, caratteristiche di stagione aggregate dagli episodi, punteggio serie da stagioni, spiegazioni stagione–stagione, avviso sulla copertura dei metadati (5.7).

- Accettazione: in modalità "Serie intera" i risultati sono identici a quelli di M3; in "Per stagione" il selettore compare sulle stagioni e non sulla serie; test: due stagioni della stessa serie con voti opposti spostano il profilo solo su sceneggiatori e registi; una serie con una stagione molto affine sale in classifica; una stagione senza crediti usa le caratteristiche della serie; cambiando modalità nessun voto va perso; la pagina impostazioni mostra la percentuale di stagioni senza crediti.

Versione: SemVer del plugin nella forma `1.0.0.0` (Jellyfin usa 4 numeri), uguale per le due build; cambia solo `targetAbi`.

## 10. Distribuzione e pubblicazione su GitHub

Il plugin si pubblica come repository GitHub pubblico che fa anche da **repository di plugin per Jellyfin**: chiunque aggiunge un URL nella dashboard e installa o aggiorna il plugin dal catalogo, senza scaricare nulla a mano.

### 10.1 Identità del progetto

| Voce | Valore |
| --- | --- |
| Proprietario GitHub | NotEnjoyingThis |
| Repository | [NotEnjoyingThis/jellyfin-plugin-ratings-recommendations](https://github.com/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations) |
| Nome nel catalogo Jellyfin | Ratings & Recommendations |
| Nome tecnico / assembly | `Jellyfin.Plugin.RatingsRecs` |
| GUID del plugin | Generato una sola volta in M0 e **mai più cambiato**: è ciò che fa riconoscere a Jellyfin gli aggiornamenti come lo stesso plugin |
| Categoria | General |
| Licenza | GPL-3.0 (file `LICENSE` nella radice) |
| URL del repository da aggiungere in Jellyfin | `https://raw.githubusercontent.com/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations/main/manifest.json` |

### 10.2 Come installa un utente qualsiasi

1. Dashboard → Plugin → Repository (in 10.11 "Catalogo" → icona impostazioni) → **+**, nome "Ratings & Recommendations", URL del manifest della tabella sopra.
2. Aggiunge allo stesso modo il repository di **File Transformation** (necessario per i pollici) e, se vuole le righe in home, quello di **Home Screen Sections**: `https://www.iamparadox.dev/jellyfin/plugins/manifest.json` (lo stesso per entrambi e per Plugin Pages).
3. Catalogo → installa File Transformation, poi Ratings & Recommendations (e opzionalmente Home Screen Sections) → riavvia Jellyfin.
4. Jellyfin sceglie da solo la build giusta in base a `targetAbi`: un server 10.11 vede solo le versioni per 10.11, un server 12 quelle per 12.
5. Gli aggiornamenti compaiono da soli nella dashboard quando esce una nuova versione.

### 10.3 Il manifest (`manifest.json`)

È un array con un solo plugin e l'elenco di **tutte** le versioni pubblicate, per entrambe le versioni di Jellyfin. Le versioni vecchie non si tolgono mai: chi resta su 10.11 deve continuare a vedere l'ultima build per 10.11 anche dopo nuove release per 12.

```json
[
  {
    "guid": "<GUID-fisso>",
    "name": "Ratings & Recommendations",
    "description": "Thumbs ratings that drive personalised recommendations.",
    "overview": "Replace the favourite heart with a 4-level rating and get recommendations based on what you liked and disliked.",
    "owner": "NotEnjoyingThis",
    "category": "General",
    "imageUrl": "https://raw.githubusercontent.com/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations/main/assets/logo.png",
    "versions": [
      {
        "version": "1.0.0.0",
        "changelog": "First release.",
        "targetAbi": "12.0.0.0",
        "sourceUrl": "https://github.com/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations/releases/download/v1.0.0/ratings-recommendations_1.0.0.0_jf12.zip",
        "checksum": "<md5 dello zip>",
        "timestamp": "2026-12-01T00:00:00Z"
      },
      {
        "version": "1.0.0.0",
        "changelog": "First release.",
        "targetAbi": "10.11.0.0",
        "sourceUrl": "https://github.com/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations/releases/download/v1.0.0/ratings-recommendations_1.0.0.0_jf10.11.zip",
        "checksum": "<md5 dello zip>",
        "timestamp": "2026-12-01T00:00:00Z"
      }
    ]
  }
]
```

Le date e gli MD5 sono esempi: li scrive la pipeline di rilascio. Il formato esatto va verificato sul manifest di un plugin ufficiale prima del primo rilascio.

### 10.4 Pipeline di rilascio (GitHub Actions)

- **`ci.yml`** — a ogni push e pull request: build `net9.0` e `net10.0`, test, controllo formattazione. Nessuna pubblicazione.
- **`release.yml`** — parte quando viene creato un tag `vX.Y.Z`:
  1. compila le due build in modalità Release, in modo riproducibile (`Deterministic`, `ContinuousIntegrationBuild`);
  2. crea i due zip, ciascuno con la sola DLL del plugin e `meta.json`;
  3. calcola gli MD5 (per il manifest) e gli SHA-256 (pubblicati accanto agli zip per chi vuole verificare);
  4. crea la GitHub Release con i due zip, i file `.sha256` e le note prese dalla sezione della versione in `CHANGELOG.md`;
  5. aggiunge le due nuove voci in cima a `versions` nel `manifest.json` e fa il commit su `main` con l'utente bot di GitHub Actions;
  6. scarica di nuovo gli zip dagli URL pubblici e ne verifica l'MD5 rispetto al manifest.
- Rilasciare una versione = aggiornare `CHANGELOG.md`, creare il tag, nient'altro. Il manifest non si modifica mai a mano.
- Versione: tag `v1.2.3` → versione plugin `1.2.3.0`, uguale per le due build.

### 10.5 File del repository per la community

| File | Contenuto |
| --- | --- |
| `README.md` (inglese) + `README.it.md` | Cosa fa, screenshot, requisiti, installazione passo per passo (10.2), configurazione, FAQ, compatibilità con le versioni, avvertenza sui metadati delle stagioni (4.1), privacy, migrazione 10.11 → 12, come compilare in locale |
| `LICENSE` | Testo GPL-3.0 |
| `CHANGELOG.md` | Formato Keep a Changelog; usato per le note di rilascio e il campo `changelog` del manifest |
| `CONTRIBUTING.md` | Come compilare, testare, avviare i server Docker, convenzioni di commit, come proporre traduzioni |
| `.github/ISSUE_TEMPLATE/bug_report.yml` | Campi obbligatori: versione di Jellyfin, versione del plugin, layout web (Legacy/Modern), browser, plugin dipendenti installati, log del server e della console del browser |
| `.github/ISSUE_TEMPLATE/feature_request.yml` | Proposta di funzionalità |
| `.github/pull_request_template.md` | Checklist: build verdi, test aggiunti, CHANGELOG aggiornato |
| `SECURITY.md` | Come segnalare vulnerabilità in privato |
| `assets/logo.png` | Immagine del catalogo (16:9, almeno 640×360) |
| `docs/DESIGN.md` | Questo documento |

Il README ha in alto i badge di ultima release, stato della CI, versioni di Jellyfin supportate e licenza.

### 10.6 Esperienza di chi installa senza le dipendenze

- Se File Transformation manca, la pagina impostazioni mostra un riquadro con il link al suo repository e le istruzioni; il resto del plugin (API, consigli, provider 12) continua a funzionare.
- Se Home Screen Sections manca, le righe in home le aggiunge lo script, senza errori.
- Ogni messaggio d'errore rivolto all'utente dice cosa fare, non solo cosa è andato storto.

### 10.7 Privacy

Il plugin non fa chiamate di rete in uscita e non raccoglie statistiche d'uso. Tutti i voti restano nella cartella dati del server. Il README lo dichiara in una sezione apposita.

## 11. Glossario e fonti

| Termine | Significato |
| --- | --- |
| PRD / TDD | Documento dei requisiti di prodotto / documento di design tecnico: l'equivalente del GDD per app e plugin |
| Content-based | Consigli basati sulle caratteristiche dei media (generi, persone…), non sul comportamento di altri utenti |
| IDF | Inverse Document Frequency: peso più alto alle caratteristiche rare |
| MMR | Maximal Marginal Relevance: riordino che bilancia pertinenza e varietà |
| Cold start | Partenza a freddo: utente con pochi voti, profilo ancora vuoto |
| `targetAbi` | Versione minima di Jellyfin dichiarata dal plugin nel manifest |
| DI | Dependency Injection: il container di servizi di ASP.NET Core usato da Jellyfin |
| File Transformation | Plugin di terze parti che permette ad altri plugin di modificare i file serviti da jellyfin-web |
| Home Screen Sections | Plugin di terze parti che aggiunge righe personalizzate alla home |

**Fonti consultate (novembre 2026)**

- [jellyfin-plugin-template](https://github.com/jellyfin/jellyfin-plugin-template) — punto di partenza ufficiale per i plugin
- [Note di rilascio Jellyfin 12.0](https://github.com/jellyfin/jellyfin/releases/tag/v12.0) — .NET 10, provider di similarità/raccomandazione, modifiche alle API per plugin
- [Ricetta di build multi-ABI per plugin Jellyfin](https://gist.github.com/IDisposable/31b194e3f6dc5acbb0e08009b6c800bd) — riferimenti NuGet solo in compilazione
- [jellyfin-plugin-file-transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation) — iniezione nel client web
- [Home Screen Sections 3.0.0](https://github.com/IAmParadox27/jellyfin-plugin-home-sections/releases/tag/3.0.0.0) — righe home, supporto Jellyfin 12
- [Reco+](https://github.com/crocodile13/jellyfin-plugin-recoplus) — plugin di raccomandazione per Jellyfin 12.2, utile come riferimento architetturale
- [Jellyfin TypeScript SDK — UpdateUserItemDataDto](https://typescript-sdk.jellyfin.org/interfaces/generated-client.UpdateUserItemDataDto.html) — campi nativi `Likes` e `Rating`
- [Pre-Transcode (release)](https://github.com/mugurc/jellyfin-plugin-pre-transcode/releases) — esempio di doppia build 10.11 / 12 con `targetAbi` distinti
