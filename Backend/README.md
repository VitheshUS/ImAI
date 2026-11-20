# ImAI backend — Phase 2

.NET 10 Minimal API using Clean Architecture and MediatR. No database, API user authentication, real document storage, or retrieval is enabled. AI queries call Azure OpenAI. The Azure AI endpoint uses `IAzureChatService` configured through `AzureOpenAI:Endpoint` and `AzureOpenAI:ChatDeployment`, with `DefaultAzureCredential` authentication. The Angular frontend remains on its existing local demo services; connecting it is a separate integration step.

## Run

Requires the .NET 10 SDK.

```sh
dotnet restore ImAI.slnx
dotnet run --project src/ImAI.Api --no-launch-profile --urls http://localhost:5100
```

Set `ASPNETCORE_ENVIRONMENT=Development` to expose the OpenAPI JSON at `/openapi/v1.json`. The included launch profile also starts Development at `http://localhost:5041` when using `dotnet run --project src/ImAI.Api` without the flags above.

## Endpoint table

Minimal API definitions live in `src/ImAI.Api/Endpoints`, separately from `Program.cs` and application handlers.

| Method | Route | Input | Success | Definition |
| --- | --- | --- | --- | --- |
| POST | `/api/ai/chat` | JSON: `query` | 200: raw AI answer as `text/plain` | `AiEndpoints.cs` |
| POST | `/api/documents` | Multipart form with one `file` field | 200: document receipt with `status: received-not-stored`, `isSimulated: true` | `DocumentEndpoints.cs` |
| GET | `/health` | None | 200: health and phase-2-stub mode | `Program.cs` |

```sh
curl -X POST http://localhost:5100/api/ai/chat -H "Content-Type: application/json" -d '{"query":"Explain RAG"}'
curl -X POST http://localhost:5100/api/documents -F "file=@notes.pdf"
```

On Windows PowerShell, use `curl.exe` for multipart requests. Chat examples are also in `src/ImAI.Api/ImAI.Api.http`.

Queries must contain non-whitespace text and are sent in the JSON body, subject to the server request size limit. Files must be non-empty, at most 50 MiB, and have a PDF, DOCX, Markdown, TXT, CSV, or JSON extension. File extension checks are preliminary validation, not content inspection. Kestrel caps the entire request at 51 MiB to allow multipart overhead. Invalid input returns Problem Details (400); oversized requests can return 413. Unsupported content types can return 415. Unexpected exceptions return a generic 500 with a trace ID and are logged server-side.

## Architecture and DI

- `ImAI.Domain`: dependency-free document receipt model. No runtime services to register.
- `ImAI.Application`: commands, MediatR handlers, input validation, storage and chat interfaces. `AddApplication()` registers MediatR by assembly scanning.
- `ImAI.Infrastructure`: Azure chat service and stub document storage. `AddInfrastructure(configuration)` registers its own implementations, including `IAzureChatService`. The old dummy responder is retained internally but has no HTTP endpoint.
- `ImAI.Api`: HTTP binding, separate endpoint classes, OpenAPI, CORS, and global exception handling through ASP.NET Core's exception middleware and `IExceptionHandler`. `AddApi()` owns these registrations.

Dependencies point inward: API → Application + Infrastructure; Infrastructure → Application; Application → Domain. `Program.cs` composes the extension methods and pipeline only.

The storage stub consumes the file stream and discards bytes. No blob URL is fabricated and no document is retained. Replace `IDocumentStorage` with an Azure Blob adapter and update `AddInfrastructure()` when implementing uploads. The AI endpoint resolves `IAzureChatService` and requires valid Azure OpenAI configuration and credentials. Document upload does not require cloud access yet.

## Configuration

CORS defaults to `http://localhost:4200`. Override `Cors:AllowedOrigins` in configuration (for example, environment variable `Cors__AllowedOrigins__0`). All endpoints are currently anonymous. Multipart antiforgery validation is disabled because no cookie-based identity is used; revisit it when adding authentication. Configure Azure credentials through user secrets or environment configuration in the storage integration phase.

## Tests

```sh
dotnet test ImAI.slnx
```

HTTP integration tests use `WebApplicationFactory` and verify raw AI response forwarding using a fake service, long queries, invalid input, malformed JSON, file acceptance/rejection, missing files, the removed dummy chat route, Swagger routes, and safe global exception responses.

Reference: [ASP.NET Core API error handling](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/handle-errors).

## Swagger UI

In Development, open `/swagger` to explore and call the Minimal API endpoints. Swagger UI uses the existing `/openapi/v1.json` document. Launch profiles open Swagger automatically. For example, `dotnet run --project src/ImAI.Api` serves it at `http://localhost:5041/swagger`. Documentation routes are enabled only in Development.
