# Romanized Titles for Jellyfin

Shows Indian films and series under their original title in Roman script: **Teen Kanya**, not *Three Daughters*;
**Apur Sansar**, not *The World of Apu*. Films from other countries keep their English titles.

## How it decides

- An item qualifies when TMDB's original title is in an Indian script (Devanagari through Malayalam: Hindi, Marathi,
  Bengali, Punjabi, Gujarati, Odia, Tamil, Telugu, Kannada, Malayalam) and it has an IMDb id.
- The new title is IMDb's original title, which IMDb keeps in Roman script for Indian films. It comes from IMDb's free
  [title.basics](https://developer.imdb.com/non-commercial-datasets/) list, downloaded weekly and trimmed to id and title.
- The name is then locked, so metadata refreshes keep it. Items whose name is already locked are left alone: to bring
  back the English title, unlock it under *Edit metadata* and add the IMDb id to the plugin's exclusion list.

Urdu titles (Arabic script) aren't covered, and an item whose TMDB original title is in English is skipped.

## When it runs

- 30 seconds after Jellyfin adds or updates films or series (one pass for a whole library scan).
- Weekly, and on demand, from the scheduled task **Apply romanized titles**, which also refreshes the IMDb list.

## Install

In Jellyfin, *Dashboard → Plugins → Repositories*, add

```
https://raw.githubusercontent.com/siriuspal/jellyfin-romanized-titles/main/manifest.json
```

then install **Romanized Titles** from the catalogue and restart Jellyfin.

## Build and release

```
dotnet test
git tag -a v1.0.1 -m "What changed" && git push origin v1.0.1
```

The tag builds the plugin, attaches it to a GitHub release and adds it to `manifest.json`. Built against Jellyfin 12.4;
the *Jellyfin watch* workflow opens an issue when a newer Jellyfin is out.
