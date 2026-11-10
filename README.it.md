# Ratings & Recommendations per Jellyfin

[![Ultima release](https://img.shields.io/github/v/release/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations)](https://github.com/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations/releases)
[![CI](https://github.com/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations/actions/workflows/ci.yml/badge.svg)](https://github.com/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations/actions/workflows/ci.yml)
![Jellyfin](https://img.shields.io/badge/Jellyfin-10.11.9%2B%20%7C%2012.0%2B-00a4dc)
[![Licenza: GPL-3.0](https://img.shields.io/badge/licenza-GPL--3.0-blue)](LICENSE)

*[Read in English](README.md)*

> **Stato: in sviluppo.** La prima versione pubblica sarà la v1.0.0. Fino ad allora il catalogo non contiene versioni installabili.

Sostituisce il cuore dei Preferiti con un voto a 4 livelli — **Detesto (-3)**, **Guardabile (-1)**, **Bello (+1)**,
**Fantastico (+3)** — e propone consigli costruiti su ciò che ti è piaciuto *e* su ciò che non ti è piaciuto.
Tutto viene calcolato sul tuo server.

**Richiede Jellyfin 10.11.9 o successivo, oppure Jellyfin 12.0 o successivo.**

## Funzioni

- Selettore a 4 pollici nel client web al posto del cuore (pagine dettaglio e card).
- Voti rispecchiati sui campi nativi di Jellyfin `Likes`, `Rating` e Preferiti: non si perdono disinstallando il plugin.
- Consigli personali basati sui contenuti (generi, registi, attori, sceneggiatori, tag, studi, decenni, saghe),
  con spiegazioni come "Perché hai amato *X*".
- Righe in home "Consigliati per te" e "Valuta questi" (con Home Screen Sections, oppure aggiunte dallo script del plugin),
  una pagina "Per te" e una riga "Simili, scelti per te" nella pagina dettaglio.
- Jellyfin 12: provider nativo dei "Simili", così i consigli arrivano anche alle app TV e mobile.
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
