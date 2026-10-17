# Repository Guidelines

## Project Structure & Module Organization

`AIVES.slnx` contains three .NET 10 projects under `src/`:

- `AIVES.PresentationLayer`: MVC/API controllers, Razor `Views/`, request `ViewModels/`, middleware, and static assets in `wwwroot/`.
- `AIVES.BusinessLogicLayer`: service contracts in `Interfaces/`, implementations in `Services/`, shared `ServiceResult<T>`, and dependency registration in `ServiceCollectionExtensions.cs`.
- `AIVES.DataAccessLayer`: EF Core `AivesDbContext`, entity `Models/`, repositories, unit of work, and material-file storage.

Keep request handling in controllers, business rules in services, and persistence in the data layer. Architecture diagrams live in `docs/`. No automated test project currently exists.

## Build, Test, and Development Commands

Run from the repository root with the .NET 10 SDK installed:

- `dotnet restore AIVES.slnx`: restore NuGet dependencies.
- `dotnet build AIVES.slnx`: compile all three projects.
- `dotnet run --project src/AIVES.PresentationLayer --launch-profile http`: start the development server at `http://localhost:5234`.
- `dotnet watch --project src/AIVES.PresentationLayer run`: run with automatic reload during development.

Development exposes the OpenAPI document at `/openapi/v1.json`. Use `src/AIVES.PresentationLayer/AIVES.PresentationLayer.http` for manual API requests.

## Coding Style & Naming Conventions

Follow existing C# conventions: four-space indentation, braces on separate lines, file-scoped namespaces, and nullable reference types. Use PascalCase for types, methods, and properties; camelCase for parameters and locals; `_camelCase` for private fields. Prefix interfaces with `I` and asynchronous methods with `Async`. Match namespaces to project folders and use descriptive suffixes such as `Controller`, `Service`, and `ViewModel`. No repository-specific formatter or lint configuration is present; follow neighboring code.

## Testing Guidelines

No testing framework or coverage threshold is configured. Build before submitting changes and manually verify affected MVC pages and API endpoints, including validation failures. If adding automated tests, place them under `tests/`, use descriptive names such as `CreateAsync_DuplicateCourseCode_ReturnsFailure`, and run `dotnet test AIVES.slnx` after adding the test project to the solution.

## Commit & Pull Request Guidelines

Recent commits use `feat:` and `fix:` prefixes. Follow that pattern with a concise description. PRs should explain the behavior change, link relevant issues, record validation commands and results, and include screenshots for UI changes. Call out database or configuration changes explicitly.

## Security & Configuration Tips

Current dependency registration uses MySQL through `ConnectionStrings:DefaultConnection`. Override credentials locally with environment variables such as `ConnectionStrings__DefaultConnection`; never commit secrets. Configure `MaterialStorage:BasePath` for local uploads and keep uploaded materials and audio out of commits.
