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
