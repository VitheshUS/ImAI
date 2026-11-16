# ImAI backend — Phase 2

.NET 10 Minimal API using Clean Architecture and MediatR. No database, authentication, real Azure uploads, retrieval, or model calls are enabled. The Angular frontend remains on its existing local demo services; connecting it is a separate integration step.

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
| POST | `/api/chat` | JSON: `query`, optional `conversationId` (UUID) | 200: conversation ID, dummy answer, `isSimulated: true`, empty sources | `ChatEndpoints.cs` |
| POST | `/api/documents` | Multipart form with one `file` field | 200: document receipt with `status: received-not-stored`, `isSimulated: true` | `DocumentEndpoints.cs` |
| GET | `/health` | None | 200: health and phase-2-stub mode | `Program.cs` |

```sh
curl -X POST http://localhost:5100/api/chat -H "Content-Type: application/json" -d '{"query":"Explain RAG"}'
curl -X POST http://localhost:5100/api/documents -F "file=@notes.pdf"
```

On Windows PowerShell, use `curl.exe` for multipart requests. Chat examples are also in `src/ImAI.Api/ImAI.Api.http`.

Queries must contain 1–8000 characters. Files must be non-empty, at most 50 MiB, and have a PDF, DOCX, Markdown, TXT, CSV, or JSON extension. File extension checks are preliminary validation, not content inspection. Kestrel caps the entire request at 51 MiB to allow multipart overhead. Invalid input returns Problem Details (400); oversized requests can return 413. Unsupported content types can return 415. Unexpected exceptions return a generic 500 with a trace ID and are logged server-side.

## Architecture and DI

- `ImAI.Domain`: dependency-free document receipt model. No runtime services to register.
- `ImAI.Application`: commands, MediatR handlers, input validation, storage and chat interfaces. `AddApplication()` registers MediatR by assembly scanning.
- `ImAI.Infrastructure`: stub document storage and dummy chat responder. `AddInfrastructure()` registers its own implementations. Microsoft.Extensions.AI and Azure.Storage.Blobs are installed here for the subsequent integration phase.
- `ImAI.Api`: HTTP binding, separate endpoint classes, OpenAPI, CORS, and global exception handling through ASP.NET Core's exception middleware and `IExceptionHandler`. `AddApi()` owns these registrations.

Dependencies point inward: API → Application + Infrastructure; Infrastructure → Application; Application → Domain. `Program.cs` composes the extension methods and pipeline only.

The storage stub consumes the file stream and discards bytes. No blob URL is fabricated and no document is retained. Replace `IDocumentStorage` with an Azure Blob adapter and update `AddInfrastructure()` when implementing uploads. Likewise, replace `IChatResponder` with the model/retrieval adapter. The packages are available, but no cloud credentials or AI provider are needed now.

## Configuration

CORS defaults to `http://localhost:4200`. Override `Cors:AllowedOrigins` in configuration (for example, environment variable `Cors__AllowedOrigins__0`). All endpoints are currently anonymous. Multipart antiforgery validation is disabled because no cookie-based identity is used; revisit it when adding authentication. Configure Azure credentials through user secrets or environment configuration in the storage integration phase.

## Tests

```sh
dotnet test ImAI.slnx
```

HTTP integration tests use `WebApplicationFactory` and verify dummy replies, conversation IDs, invalid queries, malformed JSON, file acceptance/rejection, missing files, and safe global exception responses.

Reference: [ASP.NET Core API error handling](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/handle-errors).
