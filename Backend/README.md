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
| POST | `/api/ai/chat` | JSON: `query`, optional `conversationId` (UUID) | 200: `conversationId`, `messageId`, `result`, `source` array | `AiEndpoints.cs` |
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

CORS defaults to `http://localhost:4200`. Override `Cors:AllowedOrigins` in configuration (for example, environment variable `Cors__AllowedOrigins__0`). Chat, upload, session validation, and refresh require a valid anonymous-session cookie. Creation, Swagger, and health remain public. Origin checks protect POST requests, including multipart uploads. Configure Azure credentials through user secrets or environment configuration in the storage integration phase.

## Tests

```sh
dotnet test ImAI.slnx
```

HTTP integration tests use `WebApplicationFactory` and verify structured AI replies using a fake service, long queries, invalid input, malformed JSON, file acceptance/rejection, missing files, the removed dummy chat route, Swagger routes, and safe global exception responses.

Reference: [ASP.NET Core API error handling](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/handle-errors).

## Swagger UI

In Development, open `/swagger` to explore and call the Minimal API endpoints. Swagger UI uses the existing `/openapi/v1.json` document. Launch profiles open Swagger automatically. For example, `dotnet run --project src/ImAI.Api` serves it at `http://localhost:5041/swagger`. Documentation routes are enabled only in Development.

## AI conversation contract

`POST /api/ai/chat` accepts `{ "query": "Your question", "conversationId": "5d15c0eb-1988-40da-b4df-b67a435f1c94" }`. Omit `conversationId` (or send `null`) for a new conversation. A supplied ID must be a non-empty UUID.

The JSON response contains `conversationId`, a new UUID `messageId` for this reply, `result` with the AI answer text, and `source` as an array. `source` is currently empty because document retrieval is not connected. Reuse the returned conversation ID in subsequent requests to correlate replies. IDs do not yet enable stored conversation history or multi-turn model context.

## Anonymous sessions

Session endpoints persist to SQLite using Infrastructure's `IAnonymousSessionService` registration. The default database is `Backend/imai.db` in this checkout. Override it with `ConnectionStrings:ImAI` in application configuration or `ConnectionStrings__ImAI` in the environment, for example `Data Source=C:\data\imai.db`. The EF design-time factory also accepts this environment variable. When no solution directory is available (a published build), the default database is next to the application binaries.

For a fresh database, apply the existing migration before using sessions:

```sh
dotnet ef database update --project src/ImAI.Infrastructure --startup-project src/ImAI.Infrastructure
```

| Method | Route | Request | Success |
| --- | --- | --- | --- |
| POST | `/api/sessions` | No body | `sessionId`, `expiresAt`; sets session cookie |
| POST | `/api/sessions/validate` | Cookie, no body | `sessionId`, `expiresAt` |
| POST | `/api/sessions/refresh` | Cookie, no body | `sessionId`, `expiresAt`; rotates session cookie |

The token is sent only in the `Set-Cookie` response header as `__Host-ImAI.Session`, with `HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/`, no Domain attribute, and a seven-day expiry. It is never included in the JSON response. Only its SHA-256 hash is stored. Validation does not extend expiry; refresh rotates the token and renews expiry for seven days. Old and expired cookies return 401 on protected routes. Invalid cookies are cleared.

Authentication runs on incoming requests, validates the cookie against SQLite, sets `HttpContext.User` with the session ID in `ClaimTypes.NameIdentifier`, and makes session metadata available through `HttpContext.GetCurrentSession()` (import `ImAI.Api.Sessions`). `IHttpContextAccessor` is registered for scoped consumers. This is anonymous session identity, not a verified user account. Conversation ownership checks are not implemented yet.

Use HTTPS for the API and browser frontend. Run `dotnet run --project src/ImAI.Api --launch-profile https` and open `https://localhost:7106/swagger`. Trust the local development certificate if needed with `dotnet dev-certs https --trust`. Secure cookies are not downgraded for HTTP. The frontend must use `credentials: 'include'` in fetch, or `withCredentials: true` in Angular HttpClient. Cookies automatically accompany matching paths on the API host; JavaScript cannot read the token. Keep frontend and API on the same site and scheme for SameSite=Lax; different localhost ports are supported over HTTPS.

Credentialed CORS permits the configured `Cors:AllowedOrigins` (defaults: `http://localhost:4200` and `https://localhost:4200`). The API rejects POST requests from untrusted browser origins. HTTP clients without browser origin headers can authenticate with the cookie normally.
