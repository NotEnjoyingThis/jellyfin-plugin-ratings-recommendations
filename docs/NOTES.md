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
| 6 | `LibraryOptions.SimilarItemProviders` | In 12.4 l'elenco è `TypeOptions.SimilarItemProviders` / `SimilarItemProviderOrder` (`string[]`, per tipo di media); i provider **locali sono sempre attivi** | Verificato nel codice `v12.4` (`MediaBrowser.Model/Configuration/TypeOptions.cs`, `SimilarItemsManager.cs`). "Attivabile per libreria" andrà fatto con un elenco librerie nelle impostazioni del plugin: da confermare prima di M5 | M5 |
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
| 19 | "Valuta questi": i più noti per "CommunityRating e numero di voti" | Ordine: visti non votati, poi `CommunityRating`; giro a turno per genere principale (un titolo per genere prima di ripetere) | Jellyfin non ha il numero di voti; il giro a turno realizza "coprire generi diversi" | M3 |
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

## Stato attuale (11/11/2026)

Milestone completate e committate: M0, M1, M2, M3, M3b, M4 (`2cd8810`). Prossima: M5 (provider nativo di Jellyfin 12), poi M6.

Esito delle prove manuali dell'utente su M4:
- **10.11 (Legacy)**: righe in home, "Per te" nel menu laterale, "Simili, scelti per te": tutto funziona.
- **12 (Modern, layout predefinito)**: le righe e "Simili, scelti per te" funzionano, ma **nel layout Modern l'utente non ha il menu
  laterale**, quindi la voce "Per te" non è raggiungibile. Nelle mie prove il drawer MUI esisteva nel DOM (probabilmente solo in certe
  larghezze o aperto dall'icona ☰): serve un punto di accesso sempre visibile nel Modern (barra in alto e/o menu utente) — **da correggere**.
- **Home Screen Sections**: attivato dall'amministratore senza problemi su entrambi, ma come `rrfamily` **la voce "Modular Home" non
  compare** — **da indagare** (quale opzione la rende disponibile, come si raggiunge nel Modern) e da documentare nel README.

Stato dei server di prova: File Transformation, Plugin Pages e Home Screen Sections installati (HSS reinstallato e attivato dall'utente).

## Home, menu e righe (M4)
- **File Transformation raggruppa le trasformazioni per testo del pattern** e cerca prima la chiave esatta (`WebFileTransformationService.RunTransformation`):
  HSS e Plugin Pages usano `index.html`, quindi il nostro pattern deve essere **esattamente `index.html`** (con `index\.html$` la nostra
  trasformazione non veniva più eseguita quando quei plugin erano installati). Lo stato mostra anche `lastInjectedUtc`.
- **Home Screen Sections 3.0.4.0** (stessa API su 10.11.11 e 12.4): `PluginInterface.RegisterSection(JObject)`; la classe dei risultati è creata
  con DI (`ActivatorUtilities`), metodo d'istanza con parametro `{ UserId, AdditionalData }` e ritorno `QueryResult<BaseItemDto>`.
  Le sezioni compaiono in `GET /ModularHomeViews/Sections` e nelle impostazioni utente "Modular Home"; `GET /HomeScreen/Section/{id}` restituisce
  i nostri risultati (verificato su entrambi i server). Per vederle in home servono: HSS **attivato dall'amministratore** (`Enabled` nella sua
  configurazione, di default `false`), "Modular Home" attivato dall'utente e le nostre sezioni spuntate.
- Sui server di prova la home modulare di HSS restava vuota/in caricamento anche con le sole sezioni predefinite (anche svuotando la cache del
  browser): non è stato possibile vedere la resa grafica delle nostre sezioni dentro HSS → verifica manuale. Con HSS installato e utente con
  "Modular Home" attivo, la home standard non compare più (è HSS a disegnarla).
- Disattivare un plugin dalla dashboard lo può rimuovere (visto con File Transformation in M2); disinstallare HSS libera la home standard.
- Home standard: `.homeSectionsContainer` con `.verticalSection.section0…` (10.11 e 12). Le righe dello script vanno in cima.
- Menu laterale: Legacy `.mainDrawer a.navMenuOption[href="#/home"]` (icone `material-icons <nome>`); Modern `.MuiDrawer-root a[href="#/home"]`
  (`a.MuiListItemButton-root` con `.MuiListItemIcon-root` e `.MuiListItemText-primary`): la voce "Per te" è una copia senza gestori React.
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
