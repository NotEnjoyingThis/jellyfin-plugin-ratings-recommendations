# Note tecniche

Scoperte fatte durante lo sviluppo (firme API, differenze 10.11/12, selettori DOM). Aggiornato a ogni milestone.

## Deviazioni dal design

Ogni scelta che cambia o precisa `docs/DESIGN.md`. Da tenere aggiornata.

| # | Design | Scelta | Motivo | Milestone |
|---|---|---|---|---|
| 1 | Build 10.11: pacchetti `10.11.*`, `targetAbi 10.11.0.0` | Pacchetti fissati a **10.11.9**, `targetAbi 10.11.9.0` | `IUserManager.Users` è stato sostituito da `GetUsers()` a partire da 10.11.9: compilando contro 10.11.11 il plugin si romperebbe su 10.11.0–10.11.8. Versioni fissate = build riproducibili. Requisito dichiarato: Jellyfin 10.11.9+ | M0 |
| 2 | Build 12: pacchetti `12.*` | Pacchetti fissati a **12.0.0** | Compilare contro il minimo supportato garantisce compatibilità con 12.0+; build riproducibili | M0 |
| 3 | Test su 12.0–12.2 | Server di prova su **12.4** (ultima stabile al 10/11/2026); `JF12_TAG` permette 12.0/12.2 | 12.3 e 12.4 sono uscite dopo la stesura del design | M0 |
| 4 | Motore in progetto separato + "nello zip solo la DLL del plugin" | Progetto `RatingsRecs.Engine` separato (usato dai test); il plugin compila gli stessi sorgenti al suo interno | Unico modo per avere entrambe le cose senza strumenti di merge | M0 |
| 5 | Stessa versione per le due build | Confermato; nel manifest la voce `jf12` va **sempre prima** di `jf10.11` | Il server installa qualunque versione con `targetAbi ≤` versione server; a parità di versione vince la prima in elenco (ordinamento stabile). Da verificare in M6 con installazione dal catalogo | M0 / M6 |
| 6 | `LibraryOptions.SimilarItemProviders` | In 12.4 l'elenco è `TypeOptions.SimilarItemProviders` / `SimilarItemProviderOrder` (`string[]`, per tipo di media); i provider **locali sono sempre attivi** | Verificato nel codice `v12.4` (`MediaBrowser.Model/Configuration/TypeOptions.cs`, `SimilarItemsManager.cs`). "Attivabile per libreria": vedi 34 (scelta dell'utente) | M5 |
| 7 | Righe Home Screen Sections con 20 card | HSS **ignora `limit`** (forza 1 nel registro, la quantità la decide il risultato), mostra le sezioni esterne **solo in landscape** e richiede che l'utente attivi "Modular Home" | Verificato nel codice HSS 2.5.11 e 3.0.4 (`PluginDefinedSection`). Da confermare in M4 | M4 |
| 8 | Script per Docker in `.ps1`/`.sh` | Script dei server di prova solo in PowerShell (5.1 e 7, anche `pwsh` su Linux/macOS); `build.sh` resta per CI/release | Evita di duplicare la libreria di esempio in bash senza `jq` | M0 |
| 9 | Voti in `<cartella dati del plugin>/ratings/<userId>.json` | Percorso `<PluginConfigurationsPath>/Jellyfin.Plugin.RatingsRecs/ratings/<userId>.json` (es. `/config/plugins/configurations/…`) | `BasePlugin.DataFolderPath` è la cartella **versionata** del plugin (`plugins/RatingsRecs_1.0.0.0`), sostituita a ogni aggiornamento: i voti andrebbero persi. `configurations/` sopravvive ad aggiornamenti e disinstallazione | M1 |
| 10 | "Valori modificabili dall'amministratore" | Archivio e API usano sempre il **livello canonico** (-3, -1, 1, 3); il valore configurato (`ValueHate`…`ValueAmazing`) lo applica solo il motore | Cambiare i valori nelle impostazioni non deve riscrivere né invalidare i voti salvati, e l'API resta stabile (`{ "value": -3 }`) | M1 |
| 11 | Segnali impliciti via `UserDataSaved` | Il listener gestisce solo il **Preferito da un client senza pollici** (implicito +3 salvato con `source: "implicit"`, tolto se il Preferito viene tolto). "Visto senza voto" (+0,5) e "abbandonato" (−0,5) si ricavano dai dati utente nativi al momento del calcolo (M3) | Visto/abbandonato dipendono dal tempo (30+ giorni fermo) e sono già nei dati nativi; salvarli duplicherebbe stato. Solo per il Preferito serve sapere *chi* l'ha impostato | M1 / M3 |
| 12 | API sezione 6.5 | Dettagli: `GET Ratings/{id}` risponde 200 con `value: null` se non votato; `GET Ratings?itemIds=` restituisce solo i media votati (max 500 id); `userId` opzionale su tutti gli endpoint, accettato solo per amministratori/API key (403 altrimenti); `PUT` su media non visibile all'utente → 404, su episodi → 400; voti di stagione accettati dall'API solo da M3b (l'import CSV li conserva già) | Contratto preciso per lo script web e per i test | M1 |

| 13 | API "esattamente" quelle della sezione 6.5 | In più: `GET /RatingsRecs/Web/ratings.js` e `ratings.css` (anonimi, richiesti dal prompt: un tag `<script>` non manda il token; nessun dato utente) e `GET /RatingsRecs/Status` (solo amministratori: stato degli agganci per la pagina impostazioni) | Servono allo script iniettato e alla pagina impostazioni ("mostrarlo nella pagina impostazioni") | M2 |
| 14 | Nomi dei livelli configurabili | Lo script riceve nomi personalizzati e testi IT/EN tramite un prologo `window.RatingsRecsBootstrap` aggiunto a `ratings.js` dal server (nessun endpoint in più); lingua = `lang` di jellyfin-web, poi browser, poi inglese | Evita una chiamata in più a ogni caricamento | M2 |

| 15 | "I tre termini sono portati tra 0 e 1" (5.3) | Gusto: normalizzazione **min-max sui candidati dell'utente** (tutti uguali → 0,5); qualità = `CommunityRating/10` (mancante → media della libreria); ε già in [0,1) | Il design non dice come normalizzare; min-max conserva l'ordine del gusto e rende confrontabili i tre termini | M3 |
| 16 | Penalità di vicinanza: "punteggio di gusto × 0,5" (5.4) | Applicata al gusto **normalizzato** | Moltiplicare per 0,5 un gusto grezzo negativo lo alzerebbe (−1 → −0,5), l'opposto dell'intento | M3 |
| 17 | "Altri capitoli di una saga votata -3 (opzione)" | Opzione `ExcludeHatedCollections`, **attiva** di default | È elencata tra le esclusioni dure; il default non è indicato | M3 |
| 18 | "Esclusioni: media votati" | Esclusi anche i media con un segnale implicito (Preferito da altri client, visto, abbandonato) | Sono già visti o iniziati; un Preferito implicito equivale a un voto | M3 |
| 19 | "Valuta questi": i più noti per "CommunityRating e numero di voti" | ~~Ordine: visti non votati, poi `CommunityRating`; giro a turno per genere principale~~ **Sostituita dalla 44** (solo media visti e non votati) | Jellyfin non ha il numero di voti; il giro a turno realizza "coprire generi diversi" | M3 |
| 20 | Serie "abbandonata" | Quota di episodi visti < 20% e ultimo episodio visto da 30+ giorni (stessa regola della stagione in 4.1) | Le serie non hanno una posizione di riproduzione | M3 |
| 21 | Risposta consigli = `BaseItemDto` + "un campo con la spiegazione" | `{ Items: BaseItemDto[], Explanations: [...] }` con le spiegazioni nello stesso ordine, più `Status`, `TotalRecordCount`, `IsColdStart` | Aggiungere un campo dentro `BaseItemDto` richiederebbe di riserializzarlo; così i `BaseItemDto` restano standard | M3 |
| 22 | Pagina "Per te" a tutto schermo | Disegnata dallo script nella vista "pagina non trovata" di jellyfin-web sul percorso `#/ratingsrecs` (`?type=Movie|Series`), con intestazione e menu normali | jellyfin-web non permette di aggiungere percorsi; il percorso sconosciuto mostra `#fallbackPage` senza reindirizzare, in 10.11 e 12 (Legacy e Modern) | M3 |
| 23 | Ricalcolo "dopo un cambio voto" | Debounce di 30 s per utente; ricalcolo di tutti dopo la scansione della libreria (`RefreshLibrary`) e 20 s dopo l'avvio; senza cache l'endpoint risponde `Status: "Computing"` e avvia il calcolo in background | Mai calcoli pesanti nella richiesta HTTP | M3 |

| 24 | Modalità personale "modificabile dalle preferenze della pagina Per te" | Preferenza salvata nel file voti dell'utente (`"preferences": { "seriesMode": ... }`, campo facoltativo, `schemaVersion` invariato); endpoint in più: `GET/PUT /RatingsRecs/Preferences` e `POST /RatingsRecs/Ratings/{seriesId}/CopyToSeasons` | Servono alla pagina "Per te" e al pulsante "Copia il voto della serie sulle stagioni non votate" | M3b |
| 25 | Stagioni votabili "solo nelle modalità che lo prevedono" | L'API rifiuta (400) il voto a una stagione in "Serie intera" e il voto alla serie in "Per stagione"; togliere un voto è sempre possibile; i voti non usati restano salvati | Coerenza tra interfaccia e API | M3b |
| 26 | Antologia: tag "anthology"/"antologia" "o opzione impostata a mano sulla serie" | L'opzione manuale è aggiungere uno di quei tag alla serie dall'editor dei metadati di Jellyfin | Evita un elenco di serie da mantenere nelle impostazioni del plugin | M3b |
| 27 | Serie candidata in modalità per stagione | Esclusa se votata o se una sua stagione è votata o se un suo episodio è visto (come 5.3); il punteggio usa tutte le sue stagioni. Conteggio dei voti per la partenza a freddo: "Serie intera" esclude i voti di stagione, "Per stagione" esclude i voti delle serie divise in stagioni, "Entrambe" li conta tutti | Il design non precisa i casi misti | M3b |
| 28 | Spiegazione stagione–stagione "stesso sceneggiatore" | Si indica la caratteristica in comune più pesante, preferendo sceneggiatori, registi, guest star e cast; se non ce ne sono (stagioni senza crediti) può essere il decennio o il genere | Con metadati scarsi la somiglianza nasce da altre caratteristiche | M3b |

| 29 | Titolo della riga in home cliccabile (HSS) | Le sezioni sono registrate **senza `route`**; il titolo lo rende cliccabile lo script (link a `#/ratingsrecs`), che lo rietichetta anche nella lingua dell'utente (HSS registra un solo testo, scelto con la lingua del server) e aggiunge la spiegazione sotto le card | `route` in HSS passa da `appRouter.getRouteUrl()`, che non conosce percorsi di plugin | M4 |
| 30 | "Riga home: auto / Home Screen Sections / script / off" | Con "Auto" o "Home Screen Sections" lo script disegna comunque le righe **se la home non è resa da HSS** (nessuna sezione HSS `[data-page]` nella pagina): HSS non installato, disattivato o non funzionante; se la home è di HSS non aggiunge nulla (le sezioni le aggiunge l'utente) | Degradare senza rompere | M4 |
| 31 | "Simili": "candidati limitati ai 200 media con più caratteristiche in comune, poi punteggio col profilo di gusto" | Profilo e indice tenuti in cache dal calcolo in background (nessuna lettura della libreria nella richiesta); i 200 candidati con più caratteristiche in comune sono ordinati con la formula di 5.3 (gusto normalizzato sui 200, penalità di vicinanza, qualità, esplorazione); esclusi il media aperto e i media votati negativamente (anche dopo l'ultimo calcolo); vista "Serie intera" anche in modalità per stagione | Mai calcoli pesanti nella richiesta HTTP | M4 |
| 32 | Plugin Pages | La pagina registrata è `/RatingsRecs/Web/foryou.html`; quando Plugin Pages apre `#/userpluginsettings.html?pageUrl=…foryou…` lo script porta a `#/ratingsrecs` | Plugin Pages carica pagine HTML; la pagina "Per te" è disegnata dallo script | M4 |
| 33 | Voce "Per te" "nel menu laterale" | Oltre al menu laterale (Legacy e Modern stretto): pulsante nella barra in alto del Modern largo (prima delle librerie) e voce nel menu dell'avatar (se c'è Plugin Pages la sua voce viene rietichettata nella lingua dell'utente, altrimenti lo script ne aggiunge una dopo "Impostazioni"); nel menu laterale la copia di Plugin Pages è nascosta. Tutte rispettano l'opzione "voce di menu" | Il Modern largo di Jellyfin 12 non ha menu laterale | M4 (fix) |

| 34 | Provider "attivabile per libreria" (`LibraryOptions.SimilarItemProviders`) | **Interruttore nel plugin** (scelto dall'utente): elenco delle librerie film/serie/miste nella pagina impostazioni (`SimilarProviderLibraryIds`, vuoto di default). Al salvataggio e all'avvio il plugin mette "Ratings & Recommendations" **per primo** in `TypeOptions.SimilarItemProviderOrder` (tipi `Movie`/`Series`) delle librerie scelte e lo **toglie** dalle altre, con `CollectionFolder.GetLibraryOptions()/UpdateLibraryOptions()` (API del server); nelle librerie non scelte il provider non restituisce nulla. Non tocca `SimilarItemProviders`. Se una libreria non ha `TypeOptions` per il tipo non le crea (un blocco vuoto disattiverebbe i downloader di metadati) e lo segnala nello stato | In Jellyfin 12.4 i provider locali sono sempre attivi e conta solo l'ordine; il primo che riempie il limite ferma gli altri | M5 |
| 35 | "Non mostra media votati -3" | Vale quando la libreria ha abbastanza titoli. Se ne ha meno di quelli richiesti, Jellyfin completa la lista col proprio provider (che non conosce i voti, e mescolerebbe i suoi risultati ai nostri per punteggio): per evitarlo il nostro provider restituisce tutti i titoli dello stesso tipo — prima i simili ordinati col gusto, poi gli altri per gusto, e **in fondo** quelli votati negativamente (prima -1, poi -3). Proposto all'utente, nessuna obiezione | Limite del `SimilarItemsManager` di 12.4 | M5 |
| 36 | Provider "riordina 'Simili' e 'Perché hai guardato…'" | Solo **"Simili"** (`/Items/{id}/Similar`, anche nelle app). `/Movies/Recommendations` ("Perché hai guardato…", "Con il regista…") usa solo il **primo** `IBatchLocalSimilarItemsProvider` registrato (quello di Jellyfin) e ignora l'ordine delle librerie: verificato sul server 12.4 (stesso ordine standard col nostro provider attivo) | Limite di Jellyfin 12.4; nessun aggancio supportato | M5 |
| 38 | Override 10.11 "per sostituirne il contenuto" | `/Items/Suggestions`: sostituito con i consigli **solo** se la richiesta chiede film e/o serie (altri tipi, es. episodi, invariati); DTO costruiti con le opzioni del plugin (campi forse diversi da quelli chiesti dal client). `/Movies/Recommendations`: le categorie di Jellyfin restano ("Perché hai guardato X"), ogni elenco viene **riordinato** col gusto e perde i titoli votati negativamente (`TasteOrder`); categorie rimaste vuote tolte. Solo risposte 200 già prodotte da Jellyfin, solo con hook Active; in errore si spegne fino al riavvio | Sostituire le categorie con i consigli renderebbe falsi i titoli "Perché hai guardato X" | M6 |
| 39 | "Traduzioni IT/EN" | Pagina impostazioni con chiavi `data-rr-i18n` e file `Web/i18n/config-<lingua>.json` (trattino, non punto: `config.it.json` diventerebbe un assembly satellite), servito da `GET /RatingsRecs/Web/i18n/config.{lingua}.json` (anonimo, nessun dato utente; 404 per lingue senza file). I **messaggi** di stato degli agganci restano in inglese (sono gli stessi del log); nomi degli agganci e stati sono tradotti | Separati da `en.json`/`it.json` per non appesantire `ratings.js`, caricato da ogni utente | M6 |
| 40 | "Controlli di compatibilità all'avvio" | Oltre ai controlli dei singoli agganci: hook `Compatibility` che confronta la linea della build (10.11 / 12) con la versione del server (10.11.9+ / 12+) e segnala lo zip sbagliato | Jellyfin carica un plugin su qualunque server con versione ≥ `targetAbi`: la build 10.11 si caricherebbe anche su 12 | M6 |
| 41 | `release.yml` (10.4) | Aiuti in `scripts/release.mjs` (Node, nessuna dipendenza; test con `node --test scripts/` anche in CI). In più: il tag deve puntare a un commit di `main`, i test girano prima del rilascio, la sezione del CHANGELOG è obbligatoria. Formato del manifest verificato sui modelli di Jellyfin 12.0.0 (`PackageInfo`, `VersionInfo`: stessi nomi JSON) | Sul PC di sviluppo non ci sono Python né PowerShell 7; Node c'è (ed è sui runner di GitHub) | M6 |
| 42 | `CHANGELOG.md` | La sezione `Unreleased` è stata riscritta come note di rilascio per gli utenti (prima era la cronologia dello sviluppo) | Diventa il testo della Release e del campo `changelog` del manifest | M6 |
| 37 | "Simili" nativi | Stesso tipo del media aperto (film → film, serie → serie, come il provider di Jellyfin); la riga "Simili, scelti per te" dello script resta mista. Nessuna cache lato Jellyfin (`CacheDuration = null`): tutto dalla cache dei consigli | Coerenza con i "Simili" standard | M5 |

| 43 | Selettore "nelle pagine dettaglio e sulle card" (M2) | **Il voto si dà solo dalla pagina del media** (richiesta dell'utente prima del rilascio): il selettore con popup a 4 livelli resta solo lì (clic sul livello attivo = togli il voto). Su **tutte** le card (home, librerie, "Per te", "Simili", "Valuta questi", Home Screen Sections) nessun pollice sui media non votati e, su quelli votati, un **badge di sola lettura** in alto a destra (cerchio scuro semitrasparente, `title` e `aria-label` "Il tuo voto: …", non focalizzabile). Il clic sul badge apre il media come il resto della card. Sui media votabili il cuore della card resta nascosto (varrebbe come voto); episodi e musica tengono il cuore | Votare dalle locandine era troppo facile per errore; la pagina del media è il punto unico | Rilascio 1.0 |
| 44 | "Valuta questi" = onboarding a freddo con media noti e vari, nascosto dopo N voti (5.5) | **Media visti e non ancora votati** dallo stato "visto" di Jellyfin (comprende la cronologia precedente all'installazione): film `Played`; serie con almeno un episodio visto ("Serie intera", "Entrambe"); stagioni con tutti gli episodi visti ("Per stagione", "Entrambe"); esclusi i media con un voto (anche implicito). Ordine: `LastPlayedDate` più recente prima, quelli senza data in fondo per titolo. Calcolata in background con i consigli (`RateTheseList`) e filtrata al momento della richiesta dai voti dati dopo il calcolo; un "visto" (fine riproduzione o segna come visto) fa ricalcolare l'utente dopo 30 s. Riga nascosta se vuota; nella pagina "Per te" messaggio "Non hai media visti da valutare…". Tolti la soglia `OnboardingHideAfter` (15 voti) e la selezione `Onboarding` del motore. Endpoint rinominato `GET /RatingsRecs/RateThese` (`/Onboarding` non esiste più, nessuna versione pubblicata lo usava), con `type=Movie` o `type=Series` (serie e stagioni) usato dalle schede della pagina "Per te". Nome della riga: **"Già visti, da valutare"** / "Watched, to rate" (prima "Valuta questi"). Sostituisce la 19 | Richiesta dell'utente: votare ciò che si è visto davvero. Il segnale implicito "visto senza voto = +0,5" resta | Rilascio 1.0 |
| 45 | Colori dei livelli (rosso, arancione, verde, blu) | **Icone monocromatiche**: pollice a solo contorno spesso (`stroke` 2,3; 2,8 nei badge), senza riempimento e senza colori; doppio pollice affiancato con poca sovrapposizione per restare leggibile piccolo. Nel badge bianco su cerchio `rgba(0,0,0,0.6)`; nella pagina del media e nel popup il colore del testo del tema (bianco nei temi scuri di Jellyfin, scuro in quelli chiari). Livello non votato: stesso contorno attenuato | Richiesta dell'utente | Rilascio 1.0 |

## Provider "Simili" di Jellyfin 12 (M5)
- `SimilarItemsManager` (12.4, `Emby.Server.Implementations/Library/SimilarItems/`): i provider **locali** (`ILocalSimilarItemsProvider`)
  sono sempre usati, filtrati solo da `Supports(item.GetType())`; i remoti solo se elencati in `TypeOptions.SimilarItemProviders`.
  Ordine: `SimilarItemProviderOrder` (se vuoto `SimilarItemProviders`), chiave = `Name` del provider (senza distinzione di maiuscole);
  i nomi assenti vanno in fondo (ordine di registrazione). Esecuzione **in sequenza**, ognuno con `Limit = richiesto - già trovati`;
  si ferma appena raggiunge il limite (`limit ?? 50`). Duplicati tolti per id e `PresentationUniqueKey`. Punteggio per i locali
  `1 - posizione*0,02` + bonus per l'ordine, poi ordinamento decrescente e filtro di accesso. Eccezione di un provider → avviso nel
  log e si passa al successivo. `CacheDuration` usata solo per i remoti.
- Provider di Jellyfin: tutti con nome **"Local Genre/Tag"**; quello dei film pesa generi, tag, studi, registi, attori; serie/musica in
  ordine casuale. Nella pagina della libreria (`GET /Libraries/AvailableOptions`) il nostro compare accanto agli altri
  (`MetadataPluginType.LocalSimilarityProvider`, attivo di default); togliere la spunta a un provider locale non ha effetto.
- Il nostro provider viene trovato da Jellyfin con la scansione degli export (`GetExports<ISimilarItemsProvider>()`): classe pubblica
  con costruttore DI, nessuna registrazione nostra. All'avvio `SimilarProviderSync` controlla
  `ISimilarItemsManager.GetSimilarItemsProviders<Movie>()`; se manca, stato `Failed` e nessuna modifica alle librerie.
- `/Movies/Recommendations` usa `GetBatchSimilarItemsAsync` del primo `IBatchLocalSimilarItemsProvider` (il provider film di Jellyfin)
  senza try/catch e senza ordine delle librerie (deviazione 36).
- Verifica sul server 12.4 (`rrfamily`, "Neon Rain", voto -3 a "Orbit of Glass"): spento → 6 titoli per genere/tag con "Orbit of Glass"
  primo; acceso → ordine del gusto (come la riga "Simili, scelti per te" filtrata ai film), con limite 5 nessun titolo negativo, con
  limite 12 i titoli negativi in fondo (libreria di 12 film); spento di nuovo → ordine standard. Pagina dettaglio "Altri come questo"
  con l'ordine nostro in Modern e Legacy. Controlli ripetuti in `docker/test-api.ps1` (solo 12).

## Voto solo dalla pagina del media, badge e "Valuta questi" (prima del rilascio 1.0)
- **Card di jellyfin-web 12**: l'immagine (`.cardImageContainer` dentro `.cardContent`) ha `contain: strict`, che crea un contesto di
  sovrapposizione chiuso; sopra c'è il link trasparente `.cardOverlayContainer > a.cardImageContainer`. Un elemento dentro l'immagine
  (anche gli indicatori di Jellyfin, `z-index: 1`) non riceve mai il mouse: per avere il tooltip il badge è aggiunto a `.cardScalable`
  dopo il link, in alto a destra, spostato a sinistra degli indicatori di Jellyfin (spunta "visto", numero di episodi) misurandoli; il suo
  clic viene passato al link della card (`openCardFromBadge`). Verificato su 12 Modern, 12 Legacy e 10.11: badge sopra il link (tooltip),
  nessuna sovrapposizione con gli indicatori (4 px di distanza, centrati), clic = pagina del media, nessun popup.
- `UserDataSaveReason.PlaybackFinished` / `TogglePlayed` → ricalcolo dell'utente (debounce 30 s): sui server di prova la riga si è
  riempita 9–15 s dopo aver segnato i media come visti (il primo ricalcolo era già in coda).
- Prove sui server di prova (`rrfamily`, film segnati visti con `datePlayed` diversi + un episodio di "Harbour Precinct", "Laughing Gas"
  visto e votato): riga = The Quiet Harbour (10/11), Harbour Precinct (9/11), Neon Rain (8/11), senza Laughing Gas; votato "Bello" dalla
  pagina del media, The Quiet Harbour esce dalla riga al ricaricamento; con tutto votato la riga sparisce dalla home e la pagina "Per te"
  mostra il messaggio. Tastiera nella pagina del media (simulata: Invio = clic con `detail 0`, frecce, Esc): apertura col fuoco sul livello
  corrente, frecce, Invio salva e riporta il fuoco, Esc chiude senza cambiare. Il pannello del browser era nascosto: niente screenshot,
  controlli fatti su DOM e geometria.

### Prove per ogni uso ("Già visti, da valutare")
- **Jellyfin 10.11: il filtro `IsPlayed=true` ignora stagioni e serie** (le restituisce vuote anche con tutti gli episodi visti), mentre
  `UserData.Played` della singola stagione è giusto; su 12.4 il filtro funziona per le stagioni. Il plugin usava quel filtro: su 10.11
  le stagioni viste per intero non entravano nella riga e il segnale implicito "serie vista fino alla fine" non scattava. Ora "vista per
  intero" si calcola dagli episodi (tutti visti, esclusi quelli mancanti `IsVirtualItem`), uguale sulle due versioni.
- Cambio di modalità delle serie: il ricalcolo arriva dopo 30 s, quindi la richiesta toglie subito serie o stagioni che la modalità
  corrente non vota; le stagioni passando a "Per stagione" compaiono col ricalcolo.
- Card delle nostre righe: per una stagione il titolo è il nome della serie e sotto la stagione ("Harbour Precinct" / "Season 1").
- `docker/test-api.ps1` ora prova anche: le tre modalità (episodio visto → serie; stagione vista per intero → stagione; "Entrambe"),
  voto alla stagione che toglie solo la stagione, "non visto" che toglie serie e stagione, filtro `type`, `type` non valido (400), permessi
  (403 per un altro utente, admin ammesso, 401 senza token), cuore da un'altra app (voto implicito) che toglie il media, sezione di
  Home Screen Sections uguale all'API. Tutto verde su 10.11.11 e 12.4.
- Nel browser (12 Modern e Legacy, 10.11): pagina della serie in "Per stagione" (badge sulla stagione votata, cuori delle stagioni
  nascosti, episodi col cuore), pagina della stagione col selettore, schede Film/Serie di "Per te" filtrate, cambio di modalità dal
  selettore di "Per te" (serie di nuovo votabile, stagioni col cuore di Jellyfin, voti delle stagioni conservati), larghezza telefono
  (375 px: badge dentro l'immagine, nessuna sovrapposizione), titolo "Già visti, da valutare" con link alla sezione.

## Rifinitura (M6)
- **Override 10.11**: azioni verificate nei metadati di `Jellyfin.Api.dll` 10.11.11: `SuggestionsController.GetSuggestions` /
  `GetSuggestionsLegacy(userId, mediaType, type, startIndex, limit, enableTotalRecordCount)` e
  `MoviesController.GetMovieRecommendations(userId, parentId, fields, categoryLimit, itemLimit)`. Il filtro è un `IAsyncResultFilter`
  globale registrato con `services.Configure<MvcOptions>(o => o.Filters.AddService<...>())` nel `IPluginServiceRegistrator`:
  funziona (verificato sul server 10.11.11). All'avvio e a ogni salvataggio `SuggestionsOverrideStatus` cerca le due azioni in
  `IActionDescriptorCollectionProvider`. Verifica: con l'opzione accesa `/Items/Suggestions?type=Movie` = `/RatingsRecs/Recommendations`,
  le richieste di episodi restano di Jellyfin, la categoria "Neon Rain" di `/Movies/Recommendations` perde i titoli votati -3.
  Controlli ripetuti in `docker/test-api.ps1` (solo 10.11).
- **Risorse incorporate**: un nome come `config.it.json` viene letto da MSBuild come risorsa della cultura `it` e finisce in un assembly
  satellite (`bin/.../it/*.resources.dll`): per questo i file sono `config-it.json`. `build.ps1`/`build.sh` falliscono se nello zip
  finirebbe un'altra DLL.
- **Pagina impostazioni in cache**: `/web/configurationpage` viene servita senza `Cache-Control`; dopo un aggiornamento il browser può
  mostrare la pagina vecchia finché non si ricarica con Ctrl+F5 (FAQ nei README).
- **Avvisi di Home Screen Sections nel log** ("No translation found for key 'RatingsRecsRecommended' in language 'it'"): HSS cerca
  una traduzione propria dell'id della sezione e poi usa il testo che registriamo; innocui (lo script rietichetta comunque i titoli).
- **Pipeline di rilascio**: provata a secco con gli zip reali (`node scripts/release.mjs manifest` su una copia del repository: due voci,
  prima jf12, MD5 uguali ai file `.md5`, seconda esecuzione sulla stessa versione rifiutata). Il workflow vero (Release su GitHub, commit
  del manifest, verifica degli MD5 scaricati) si prova solo creando il tag.

## Stato attuale (11/11/2026)

Milestone completate e committate: M0, M1, M2, M3, M3b, M4 (`2cd8810`), M5 (`a01ddd0`), M6 (`7855508`). Rilascio v1.0.0 **preparato**
(sezione `[1.0.0] - 2026-11-11` nel CHANGELOG, nota "in sviluppo" tolta dai README); restano push di `main` e tag `v1.0.0`
(decisi dall'utente) con il controllo di accettazione di M6: installazione dal catalogo su server puliti 10.11 e 12, e una
seconda release di prova (v1.0.1) che compare come aggiornamento.

Esito delle prove manuali dell'utente su M4:
- **10.11 (Legacy)**: righe in home, "Per te" nel menu laterale, "Simili, scelti per te": tutto funziona.
- **12 (Modern, layout predefinito)**: le righe e "Simili, scelti per te" funzionano, ma **nel layout Modern l'utente non ha il menu
  laterale**, quindi la voce "Per te" non è raggiungibile. Nelle mie prove il drawer MUI esisteva nel DOM (probabilmente solo in certe
  larghezze o aperto dall'icona ☰): serve un punto di accesso sempre visibile nel Modern (barra in alto e/o menu utente) — **da correggere**.
- **Home Screen Sections**: attivato dall'amministratore senza problemi su entrambi, ma come `rrfamily` **la voce "Modular Home" non
  compare** — **da indagare** (quale opzione la rende disponibile, come si raggiunge nel Modern) e da documentare nel README.

Stato dei server di prova: File Transformation, Plugin Pages e Home Screen Sections installati (HSS reinstallato e attivato dall'utente).

**Correzioni dopo le prove (verificate nel browser come `rrfamily`):**
- Modern largo (12, 1280 px): nessun drawer; "Per te" ora è un pulsante nella barra in alto (copia del pulsante "Preferiti") e una voce del
  menu dell'avatar `#app-user-menu`; il clic porta a `#/ratingsrecs`. Modern stretto (375 px): voce nel drawer. 12 Legacy e 10.11: voce nel
  menu laterale, copia "For you" di Plugin Pages nascosta.
- **"Modular Home" esiste, ma nel Modern largo è nel menu dell'avatar** (insieme a "For you"), non in un menu laterale: è la voce aggiunta
  da Plugin Pages, che quindi serve. Nel Legacy è nel menu laterale. `/PluginPages/User` la elenca per `rrfamily` su entrambi i server.
- Opzioni HSS (pagina impostazioni, *Global Settings*): **Enabled by Default** (chiave `Enabled`, `false` dopo l'installazione — così è
  su entrambi i server di prova) attiva Modular Home per tutti; **Allow User Override** (`AllowUserOverride`, `true`) permette all'utente di
  attivarla da sé. Scheda *Section Settings*: sezioni, ordine, "Let the user override this section". Pagina utente
  (`/ModularHomeViews/settings`): `Enable "Modular Home"`, `Enabled Sections`, `Save`. Passaggi documentati nei README.

**Seconda tornata di prove (11/11/2026):** "Per te" e la pagina funzionano su 10.11 e 12; i titoli delle righe portano ora alla
sezione giusta (`#/ratingsrecs?section=recommended|rate`, scroll con `scroll-margin-top`), verificato su 10.11, 12 Legacy e 12 Modern.
Corretto anche il titolo "Pagina non trovata" nell'intestazione Legacy (`.skinHeader .pageTitle`).

**"Modular Home" → "Pagina non trovata" su 12 Modern (`#/userpluginsettings?pageUrl=…` senza `.html`): non dipende da noi.**
- Versioni su entrambi i server: Plugin Pages **3.0.3.0**, Home Screen Sections **3.0.4.0**, File Transformation 3.0.3.0.
- Plugin Pages genera sempre `#/userpluginsettings.html?pageUrl=…` (`/PluginPages/inject.js`, menu utente Modern e menu laterale) e
  aggiunge la rotta `userpluginsettings.html` modificando `main.jellyfin.bundle.js` (presente sia nell'app Legacy sia in quella Modern).
- jellyfin-web 10.11 e 12 hanno un `FallbackRoute`: un indirizzo `….html` che non corrisponde a nessuna rotta viene riscritto **senza
  `.html`** ("deprecated URL format") e poi mostra "Pagina non trovata" (provato: `#/qualcosa.html?x=1` → `#/qualcosa?x=1`). Quindi
  l'indirizzo visto dall'utente compare solo se **nel browser manca la rotta di Plugin Pages**.
- **Causa confermata (dati dal browser dell'utente, Chrome 157):** `main.jellyfin.bundle.js` caricato con `transferSize=0` (dalla cache,
  senza richiesta al server) e senza la rotta, mentre il server la serve (`serverBundleHasRoute: true`).
- Meccanismo: per ~3 s a ogni avvio (misurato da 6 a 8 s dopo `docker restart`) il bundle è servito **non modificato, senza
  `Cache-Control` e con `Last-Modified`** (come tutti i file del client web che nessun plugin trasforma, es. `1133.bundle.js`). Senza
  `Cache-Control` Chrome usa la freschezza euristica (~10% dell'età del file: file del 26/10 → ~1,5 giorni) e lo riusa senza
  rivalidare, anche con F5; aggiungere `.html` non serve perché la rotta manca. Dopo la registrazione delle trasformazioni il file è
  servito con `Cache-Control: no-cache` e ETag del contenuto modificato (rivalidazione corretta, 304/200). `index.html` nella finestra
  ha già `no-cache` e il nostro script.
- Riprodotto nel browser di prova (fetch del bundle durante il riavvio → poi `transfer=0`, indirizzo senza `.html`, "Pagina non
  trovata"); risolto sostituendo la copia in cache (`fetch(url, {cache:'reload'})` + ricarica). Ctrl+F5 inviato al pannello del browser
  non ha forzato la ricarica: non verificato su Chrome reale. **L'utente ha confermato che dopo aver svuotato la cache "Modular Home"
  si apre su 12.**
- È un limite di Jellyfin/File Transformation (nessun `Cache-Control` sui file del client web prima che le trasformazioni siano
  registrate), non di Plugin Pages né nostro.
- Con il nostro plugin **disattivato** (12, Modern e Legacy) "Modular Home" funziona; con il nostro plugin attivo pure. Il nostro script
  non tocca quei link (rietichetta solo la voce di Plugin Pages del nostro "Per te").
- Rimedio documentato nei README (FAQ): con il server avviato, "Svuota la cache e ricarica forzatamente" (F12, clic destro sul
  pulsante di ricarica), oppure cancellare i dati del sito, oppure il comando `fetch(..., {cache:'reload'})` nella console. Il primo
  rimedio scritto (Ctrl+F5 o aggiungere `.html`) era sbagliato: l'utente ha confermato che `.html` non funziona. Non modifichiamo il
  comportamento di Plugin Pages / HSS.

**Home modulare vuota (rotellina) sui server di prova: problema di Home Screen Sections.** Su 10.11 `rrfamily` ha Modular Home attiva
(MyMedia, ContinueWatching, LatestMovies + le nostre due sezioni). `GET /HomeScreen/Sections` risponde alla prima chiamata dopo l'avvio
(`Items: []`) e dalla seconda resta appeso (> 20 s) **anche togliendo le nostre sezioni** (provato riavviando con il file
`ModularHomeSettings.json` senza di esse, poi ripristinato). Le singole sezioni (`/HomeScreen/Section/{id}`) rispondono subito (nostre ~60 ms).
Per provare le righe dello script su 10.11 usare `rradmin` (Modular Home non attiva).

## Home, menu e righe (M4)
- **File Transformation raggruppa le trasformazioni per testo del pattern** e cerca prima la chiave esatta (`WebFileTransformationService.RunTransformation`):
  HSS e Plugin Pages usano `index.html`, quindi il nostro pattern deve essere **esattamente `index.html`** (con `index\.html$` la nostra
  trasformazione non veniva più eseguita quando quei plugin erano installati). Lo stato mostra anche `lastInjectedUtc`.
- **Home Screen Sections 3.0.4.0** (stessa API su 10.11.11 e 12.4): `PluginInterface.RegisterSection(JObject)`; la classe dei risultati è creata
  con DI (`ActivatorUtilities`), metodo d'istanza con parametro `{ UserId, AdditionalData }` e ritorno `QueryResult<BaseItemDto>`.
  Le sezioni compaiono in `GET /ModularHomeViews/Sections` e nelle impostazioni utente "Modular Home"; `GET /HomeScreen/Section/{id}` restituisce
  i nostri risultati (verificato su entrambi i server). Per vederle in home servono: "Modular Home" attiva per l'utente (dall'amministratore con
  *Enabled by Default*, chiave `Enabled`, di default `false`; oppure dall'utente se *Allow User Override*) e le nostre sezioni spuntate.
- Sui server di prova la home modulare di HSS restava vuota/in caricamento anche con le sole sezioni predefinite (anche svuotando la cache del
  browser): non è stato possibile vedere la resa grafica delle nostre sezioni dentro HSS → verifica manuale. Con HSS installato e utente con
  "Modular Home" attivo, la home standard non compare più (è HSS a disegnarla).
- Disattivare un plugin dalla dashboard lo può rimuovere (visto con File Transformation in M2); disinstallare HSS libera la home standard.
- Home standard: `.homeSectionsContainer` con `.verticalSection.section0…` (10.11 e 12). Le righe dello script vanno in cima.
- Menu laterale: Legacy `.mainDrawer a.navMenuOption[href="#/home"]` (icone `material-icons <nome>`); Modern `.MuiDrawer-root a[href="#/home"]`
  (`a.MuiListItemButton-root` con `.MuiListItemIcon-root` e `.MuiListItemText-primary`): la voce "Per te" è una copia senza gestori React.
  Il drawer Modern esiste solo a finestra stretta; a finestra larga i pulsanti delle librerie sono
  `.MuiAppBar-root .MuiToolbar-root .MuiStack-root a.MuiButton-root` (es. `#/home?tab=1` Preferiti) e il menu dell'avatar è `#app-user-menu`
  (Profilo, `#/mypreferencesmenu` Impostazioni, voci di Plugin Pages, Connessione rapida, Esci). Le copie sono link semplici: il router
  segue l'hash.
- Pagina dettaglio: "Altri come questo" = `#similarCollapsible`; contenitore 10.11 `.detailPageContent`, 12 `.detailPageSecondaryContainer`.

## Stagioni (M3b)
- `Season.SeriesId`, `Season.IndexNumber`, `Episode.SeasonId`, `PersonKind.GuestStar`, `BaseItem.PremiereDate`: uguali in 10.11.9 e 12.0.
- `BasePlugin<T>.ConfigurationChanged` è una **proprietà** delegato (`EventHandler<BasePluginConfiguration>`), non un evento: `+=` funziona lo stesso;
  usata per ricalcolare tutti 10 s dopo il salvataggio delle impostazioni.
- Libreria di prova: 4 stagioni, 1 senza crediti per episodio ("Tales from Nowhere" stagione 1) → rapporto 25% su entrambi i server.
- La riga dei pulsanti della pagina dettaglio (`.detailRibbon`) è flex e affiancata alla copertina: per aggiungere un pulsante su una
  nuova riga si mette `flex-wrap: wrap` sulla riga e `flex-basis: 100%` sull'elemento.

## Motore e consigli (M3)
- Persone: `ILibraryManager.GetPeople(BaseItem)` in entrambe; 12 ha anche `GetPeopleByItems(ids)` (usato sotto `#if JF12`).
- Saghe: `BoxSet` (è un `Folder`) → `GetLinkedChildren()`.
- Visti: `InternalItemsQuery { IsPlayed = true }` per film, serie ed episodi (una serie con un episodio visto non è candidata); in corso: `IsResumable = true`.
- Fine scansione libreria: `ITaskManager.TaskCompleted` con chiave `RefreshLibrary` (uguale in 10.11.9 e 12.0).
- Task pianificato: `IScheduledTask` scoperto da Jellyfin, chiave `RatingsRecsRecompute`, `IntervalTrigger` 6 h.
- Tempi misurati sui server di prova (14 media): motore 1–170 ms, totale 0,2–2 s (il primo calcolo dopo l'avvio include il JIT);
  endpoint da cache 20–45 ms. Test con 2000 media sintetici: ~0,5 s.

## Client web: DOM verificato (M2)

Ispezionato dal vivo su 10.11.11 (Legacy), 12.4 Legacy (`desktop-legacy`) e 12.4 Modern (`auto`).

- **Pagina dettaglio** (film, serie, stagione, episodio): stesso markup legacy in tutti e tre i casi, anche nel Modern.
  Cuore: `.mainDetailButtons button[is="emby-ratingbutton"].btnUserRating` con `data-id`, `data-isfavorite`, `data-likes`
  (nessun `data-itemtype`: il tipo si legge con `ApiClient.getItem`). Contenitore: `.page.itemDetailPage` dentro `.mainAnimatedPages`.
  Le pagine visitate restano nel DOM con classe `.hide`: cercare sempre in `.page:not(.hide)`.
- **Card**: `.card[data-id][data-type]` → `.cardOverlayContainer` → `.cardOverlayButton-br button[is="emby-ratingbutton"]`
  con `data-id` e `data-itemtype`. Il contenitore dell'overlay ha `data-action="link"`: i clic nel selettore vanno fermati
  (`stopPropagation`) o aprono il media. Le classi `itemAction` non vanno copiate.
- jellyfin-web mette `hide` sul cuore mentre carica l'elemento: copiando le classi del cuore va esclusa.
- **Layout**: Modern = `.MuiAppBar-root` (header MUI, drawer `.MuiDrawer-root`); Legacy = `.skinHeader` + `.mainDrawer`.
  In 12 la scelta è in Impostazioni → Schermo → "Modalità di visualizzazione" (`auto` = Modern, `desktop-legacy`, `mobile-legacy`, `tv`),
  salvata in `localStorage.layout`; serve ricaricare la pagina.
- **Navigazione**: nel Modern `hashchange`/`popstate` **non** scattano; `viewbeforeshow`/`viewshow`/`pageshow` sì (sul `document`, fase di cattura)
  in tutti i layout. Lo script usa `viewshow` + `MutationObserver` (+ `hashchange` per sicurezza).
- **Tema**: variabili `--jf-palette-*` su `:root, [data-theme]` (es. `--jf-palette-primary-main`, `-error-main`, `-success-main`,
  `-warning-main`, `-background-paper`, `-text-primary`, `-action-hover`, `-action-selected`), `--jf-shape-borderRadius`. Presenti in 10.11 e 12.
- `window.ApiClient` esiste in tutti i layout: `accessToken()`, `getCurrentUserId()`, `getUrl()`, `getItem()`.
- **Cache di `index.html`**: File Transformation non cambia l'ETag del file originale; un browser che aveva già in cache `index.html`
  prima dell'installazione riceve 304 e non vede lo script finché non ricarica forzando (Ctrl+F5). Documentato nelle FAQ.
- **File Transformation**: una registrazione al riavvio basta (`RegisterTransformation`); senza il plugin l'aggancio va in
  `Unavailable` dopo circa un minuto di tentativi, `index.html` resta intatto e la pagina impostazioni mostra le istruzioni manuali.
  Nota per i test: disattivare File Transformation dalla dashboard lo ha rimosso dalla cartella plugin (reinstallato con `install-deps.ps1`).

## Firme verificate (v10.11.11 e v12.4)

Identiche nelle due versioni salvo dove indicato.

- `BasePlugin<T>(IApplicationPaths, IXmlSerializer)`; `DataFolderPath`, `Configuration`, `SaveConfiguration()`.
- `IHasWebPages.GetPages()` → `PluginPageInfo { Name, DisplayName, EmbeddedResourcePath, EnableInMainMenu, MenuSection, MenuIcon }`.
- `IPluginServiceRegistrator.RegisterServices(IServiceCollection, IServerApplicationHost)`.
- `IUserDataManager`: evento `UserDataSaved` (`UserDataSaveEventArgs { UserId, Keys, SaveReason, UserData, Item }`),
  `SaveUserData(User, BaseItem, UserItemData, UserDataSaveReason, CancellationToken)`,
  `SaveUserData(User, BaseItem, UpdateUserItemDataDto, UserDataSaveReason)`, `GetUserData(User, BaseItem)`.
  Solo 12: `GetUserDataBatch`, `GetResumeUserData*`.
- `UserItemData { double? Rating; bool? Likes; bool Played; int PlayCount; bool IsFavorite; DateTime? LastPlayedDate; }`.
- `IUserManager.GetUsers()` (da 10.11.9; prima esisteva la proprietà `Users`), `GetUserById(Guid)`.
- `ILibraryManager.GetItemList(InternalItemsQuery)`; `InternalItemsQuery(User?)` con `IncludeItemTypes (BaseItemKind[])`, `IsPlayed`, `Recursive`, `ItemIds`, `ExcludeItemIds`, `OrderBy`, `DtoOptions`.
- `IDtoService.GetBaseItemDtos(IReadOnlyList<BaseItem>, DtoOptions, User? = null, BaseItem? = null)`;
  **12** aggiunge `bool skipVisibilityCheck = false` → binari distinti obbligatori.
- `IScheduledTask { Name, Key, Description, Category, ExecuteAsync(IProgress<double>, CancellationToken), GetDefaultTriggers() }`;
  `TaskTriggerInfo.Type` è l'enum `TaskTriggerInfoType` (`IntervalTrigger`, …) in entrambe.
- Solo 12 (`MediaBrowser.Controller.Library`): `ISimilarItemsProvider { Name, Type, CacheDuration }`,
  `ILocalSimilarItemsProvider { Supports(Type); GetSimilarItemsAsync(BaseItem, SimilarItemsQuery, CancellationToken) }`
  (+ generico `<TItemType>`), `IBatchLocalSimilarItemsProvider`, `ISimilarItemsManager` (anche `GetMovieRecommendationsAsync`),
  `SimilarItemsQuery { User, Limit, DtoOptions, ExcludeItemIds, ExcludeArtistIds }`, `MetadataPluginType.LocalSimilarityProvider`.
  Registrazione per scansione degli export (`ApplicationHost`: `AddParts(GetExports<ISimilarItemsProvider>())`).

## Dati utente e voti (verificato in M1)
- `UserItemData.Likes` **è calcolato** da `Rating` (`Rating >= 6.5`); il setter di `Likes` scrive `Rating = 10` o `1`.
  Quindi il plugin scrive solo `Rating` (2, 4, 7, 10) e `Likes` segue da solo; scrivere `Likes` dopo `Rating` lo sovrascriverebbe.
- `UserDataSaved` viene sollevato **in modo sincrono** dentro `SaveUserData` → per ignorare le proprie scritture basta un marcatore
  attivo durante la chiamata (`SelfWriteGuard`).
- Il cuore dei client (`POST/DELETE /UserFavoriteItems/{id}`) salva con `UserDataSaveReason.UpdateUserRating` (10.11 e 12).
- Utente dal token: `IAuthorizationContext.GetAuthorizationInfo(HttpContext)` → `AuthorizationInfo { User, IsApiKey }`;
  amministratore: `user.HasPermission(PermissionKind.IsAdministrator)` (`Jellyfin.Data`, enum in `Jellyfin.Database.Implementations.Enums`).
- Visibilità (librerie + parental control): `BaseItem.IsVisibleStandalone(User)`.
- Namespace: `User` in `Jellyfin.Database.Implementations.Entities`, `BaseItemKind` in `Jellyfin.Data.Enums` (uguali in 10.11.9 e 12.0).
- Le risposte JSON di Jellyfin **omettono le proprietà null** e scrivono gli id **senza trattini**: i DTO del plugin forzano `value`
  anche quando è null; lo script web dovrà confrontare gli id normalizzati.

## Caricamento dei plugin
- Il server carica un plugin se `versione server ≥ targetAbi` (nessun limite superiore) — `PluginManager.ReadManifest`.
- Il catalogo filtra le versioni con `targetAbi > versione server` e ordina per versione decrescente.
- `meta.json` nello zip: `category, changelog, description, guid, name, overview, owner, targetAbi, timestamp, version`, più `assemblies` (limita le DLL caricate). `status`, `autoUpdate`, `imagePath` li scrive il server.
- Manifest: array di plugin `{guid, name, description, overview, owner, category, imageUrl, versions[{version, changelog, targetAbi, sourceUrl, checksum (MD5 hex minuscolo), timestamp}]}`.

## Plugin di IAmParadox27 (GPL-3.0, chiamati solo via reflection)
Repository dei plugin: `https://www.iamparadox.dev/jellyfin/plugins/manifest.json` (File Transformation, Home Screen Sections, Plugin Pages; build per 10.11.11 e 12.0–12.4).

- **File Transformation** (GUID `5e87cc92-571a-4d8d-8d98-d2d4147f9f90`): assembly con `FullName` che contiene `.FileTransformation`,
  tipo `Jellyfin.Plugin.FileTransformation.PluginInterface`, `static void RegisterTransformation(JObject payload)`.
  Payload: `id` (Guid), `fileNamePattern` (regex), `callbackAssembly` (`Assembly.FullName` esatto), `callbackClass`, `callbackMethod`.
  Callback **statica**, un parametro con proprietà `contents` (stringa), ritorna il file trasformato (`string`). 3.x ha anche `RemoveTransformation(Guid)`.
  Il `JObject` si costruisce via reflection (`Parse` sul tipo del parametro) → nessuna dipendenza Newtonsoft nel nostro progetto.
- **Home Screen Sections** (GUID `b8298e01-2697-407a-b44d-aa8dc795e850`): tipo `Jellyfin.Plugin.HomeScreenSections.PluginInterface`,
  `static void RegisterSection(JObject)`, invariato tra 2.5.11 e 3.0.4. Payload: `id, displayText, limit, route, additionalData,
  resultsAssembly, resultsClass, resultsMethod` (oppure `resultsEndpoint`). Metodo **d'istanza** creato con `ActivatorUtilities` (DI),
  parametro `{ UserId, AdditionalData }`, ritorno `QueryResult<BaseItemDto>`.
- **Plugin Pages** (GUID `5b6550fa-a014-4f4c-8a2c-59a43680ac6d`): 3.x `PluginInterface.RegisterPage(JObject)` con
  `{ id, url, displayText, icon, isEnabledAssembly, isEnabledClass, isEnabledMethod }`; 2.4.x senza `PluginInterface` (solo config.json).

## Server di prova
- La pagina impostazioni (pagina classica `data-role="page"`) si apre sia in 10.11.11 sia nel layout Modern di 12.4.
- In 12 l'header `Authorization: MediaBrowser Client=..., Token=...` funziona (le rotte `/emby/*` sono state rimosse).
- Jellyfin risponde 503 per alcuni secondi dopo l'avvio: gli script ritentano.
- Su 12.4 aggiungere una libreria con `refreshLibrary=true` non sempre avvia la scansione: `setup-server.ps1` chiama `POST /Library/Refresh` e aspetta i media.
- `docker/test-api.ps1` verifica i criteri di accettazione dell'API (26 controlli per server, incluso il riavvio).
