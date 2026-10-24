# Repository Guidelines

## Current architecture

React frontend lives in `src/`. `AIVES.slnx` contains the single ASP.NET Core .NET 10 backend project at `backend/AIVES.Api`.

Request flow: Controllers → application services in `Features/` → `Data/AivesDbContext` / EF Core → existing MySQL. Keep authorization, validation and transactions in services; controllers bind HTTP requests and return responses. No DbContext in controllers or SignalR hubs. Do not restore the obsolete three-project backend under `src`.

## Build and test

- `dotnet build AIVES.slnx`
- `npm run build`
- `npm run lint`
- Isolated backend tests: `dotnet test tests/AIVES.Workflow.Tests` with a filter selecting the relevant suites; use `AdminWorkflowTests`, `LayeringTests`, `LecturerWorkflowTests`, `ScheduleTimingTests`, `RealtimeScheduleTests` for admin/lecturer changes.
- `tests/LecturerHarness` uses SQLite and blocks AI/external HTTP. Do not run MySQL acceptance scripts or provider probes without explicit authorization.
- `tests/AIVES.Tests` is legacy and is not part of the active solution.

## Configuration and safety

Backend: `dotnet run --project backend/AIVES.Api --launch-profile AIVES.Api`; frontend: `npm run dev`. See README for Git Bash environment variables and docs/GEMINI_SETUP.md for private provider configuration.

Keep appsettings secrets, local environment files, uploads and artifacts out of Git. Do not print keys or passwords. Never reset the real MySQL database, rerun SQL dumps containing DROP statements, or delete user files without a clear request.

## Conventions

Follow adjacent C# style, nullable reference types, PascalCase types/methods and cancellation tokens for asynchronous operations. Keep React pages concise and Vietnamese UI consistent. Use scoped CSS and show API failures rather than faking success in localStorage.

Architecture diagrams are in `docs/architecture`. Keep run instructions aligned with the monolithic backend. Commit messages use concise `feat:` / `fix:` descriptions. Include meaningful build/test evidence in reviews.
