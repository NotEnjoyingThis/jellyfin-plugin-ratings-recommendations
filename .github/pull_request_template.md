## What and why

<!-- Short description of the change and the problem it solves. Link the issue if any. -->

## Checklist

- [ ] Both builds compile (`net9.0` for Jellyfin 10.11, `net10.0` for Jellyfin 12)
- [ ] `dotnet test` is green and new tests cover the change (always for engine and rating store)
- [ ] `dotnet format --verify-no-changes` is clean
- [ ] Tested on the Docker test servers (10.11.11 and 12.x) when the change touches the server or the web client
- [ ] `CHANGELOG.md` updated (section `Unreleased`)
- [ ] UI strings added in both English and Italian
- [ ] No outgoing network calls, no new dependencies without discussion
