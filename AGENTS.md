# Project instructions — Albion Data Project

Personal Albion Online market tool: a .NET 10 WPF app plus CLI (`Aplicacio/`), a fork of
`albiondata-client` linked as a submodule (`Client/`), a local SQLite database (`Dades/`)
and this MkDocs site (`docs/`).

## Language rules

- **Documentation, READMEs and commit messages: English.**
- **UI strings, log messages and code comments: Catalan** (personal tool, existing style).
- **Chat with the user: Catalan.**

## Session protocol

### At the start of a session

1. Read `docs/status.md`.
2. Report the open tasks to the user as a short bullet list (do not dump the whole file).
3. If `git status` shows uncommitted work from a previous session, mention it.
4. Do not start implementing before that summary.

### During the session

- When a task from `docs/status.md` is finished, mark it right away (move it to **Done**).
- When a new task appears, add it under the right heading.
- Any change in behaviour, paths, commands or dependencies must update the matching page
  (`architecture`, `installation`, `data-sources`, `database`, `client`, `usage`).
- `docs/changelog.md` is only written at the end of the session.

### At the end of a session

Triggered when the user says we are done (e.g. "ja està", "fins aquí", "tanquem").

1. `docs/status.md`: move finished tasks to **Done**, add what is left, bump
   *Last updated* with the date.
2. `docs/changelog.md`: add a dated entry — what changed, why and how it was verified.
3. Run:

   ```powershell
   .\scripts\session-end.cmd -Message "Session YYYY-MM-DD: <short summary>"
   ```

   (`session-end.cmd` wraps the PowerShell script so it also works when script execution is
   disabled on the machine.)

   It validates the MkDocs build, commits everything and pushes to `main`; GitHub Pages
   redeploys automatically.
4. Confirm the pushed commit to the user.

Never commit `Dades/`, `site/`, `bin/`, `obj/` or any secret.

## Build and verify

```powershell
cd Aplicacio
dotnet build
dotnet run --project src\AlbionMarket.Cli -- flips
```

- Do not hammer the AODP API (Europe has been limited to 1 request per 10-minute window).
- The database lives at `Dades\market.db`; never hardcode paths outside `AppPaths`.

## Repository facts

- Public repository: <https://github.com/albertacedo/albion-data-project>
- Fork submodule: <https://github.com/albertacedo/albiondata-client>
- Documentation: <https://albertacedo.github.io/albion-data-project/>
- Docs are built and deployed by `.github/workflows/docs.yml` on every push touching
  `docs/**` or `mkdocs.yml`.
