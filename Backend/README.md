# ImAI backend

.NET 10 Minimal API with Clean Architecture, MediatR, Azure OpenAI chat, and SQLite-backed anonymous sessions. Document ingestion remains an explicit stub; file bytes are discarded. Retrieval, stored conversation history, and conversation ownership enforcement are not connected yet.

## Run

From `Backend/`:

```sh
dotnet restore ImAI.slnx
dotnet ef database update --project src/ImAI.Infrastructure --startup-project src/ImAI.Infrastructure
dotnet run --project src/ImAI.Api --launch-profile https
```

Open `https://localhost:7106/swagger` in Development. Trust the local certificate with `dotnet dev-certs https --trust` if needed. HTTPS is the default launch profile because session cookies are Secure. The `http` profile remains available for non-cookie checks. OpenAPI JSON is at `/openapi/v1.json`; documentation is not served in Production.

## Endpoints

| Method | Route | Input | Result | Access |
| --- | --- | --- | --- | --- |
| POST | `/api/sessions` | No body | `sessionId`, `expiresAt`; sets cookie | Public |
| POST | `/api/sessions/validate` | Cookie, no body | `sessionId`, `expiresAt` | Session |
| POST | `/api/sessions/refresh` | Cookie, no body | `sessionId`, `expiresAt`; rotates cookie | Session |
| POST | `/api/ai/chat` | JSON: `query`, optional UUID `conversationId` | `conversationId`, `messageId`, `result`, `source` array | Session |
| POST | `/api/documents` | Multipart `file` | Simulated document receipt, `received-not-stored` | Session |
| GET | `/health` | None | `status: ok` (process liveness) | Public |

Create a session before calling protected endpoints. Browser clients attach the cookie automatically; use `withCredentials: true` in Angular HttpClient or `credentials: 'include'` in fetch. Swagger's same-origin requests also use the cookie. HTTP examples are in `src/ImAI.Api/ImAI.Api.http`.

Queries must contain non-whitespace text. Omitting or sending null `conversationId` generates a new UUID; a supplied ID must not be empty. Every reply gets a new message UUID. IDs currently correlate requests only, and do not provide stored multi-turn context. `source` remains empty until retrieval is integrated.

Uploads accept PDF, DOCX, Markdown, TXT, CSV, and JSON extensions. Files must be non-empty and at most 50 MiB. The 51 MiB server request limit includes multipart overhead and derives from the same file-size constant. Extension checks do not inspect document contents.

## Configuration

- `AzureOpenAI:Endpoint`: HTTPS Azure OpenAI resource endpoint.
- `AzureOpenAI:ChatDeployment`: deployment name. The shared SDK client is created lazily, so cloud configuration is needed only when chat is used.
- Azure credentials: the current implementation retains `AzureCliCredential`; sign in with Azure CLI for live local calls. Managed identity configuration is a future deployment task.
- `ConnectionStrings:ImAI`: optional SQLite connection string. Default: `Backend/imai.db` in this checkout, or `imai.db` beside published binaries. `ConnectionStrings__ImAI` overrides both runtime configuration and EF tooling. The design-time factory otherwise uses the default path; it does not load API appsettings files.
- `Cors:AllowedOrigins`: exact HTTP(S) origins. Defaults: `http://localhost:4200` and `https://localhost:4200`. CORS and POST origin validation share one normalized configuration. Wildcards, credentials, and non-root paths are rejected.

Use the same site and scheme for frontend and API with SameSite=Lax cookies; different HTTPS localhost ports are supported. The frontend currently retains its own demo services and is not connected to this API.

## Session behavior

Tokens use 32 cryptographically random bytes encoded as hexadecimal; only their SHA-256 hash is stored. The token is delivered only through `Set-Cookie`: `__Host-ImAI.Session`, `HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/`, no Domain, seven-day UTC expiry. Service and HTTP responses contain only session metadata. `ISessionTokenWriter` keeps HTTP cookie handling outside Infrastructure.

Authentication validates cookies against SQLite on incoming requests, sets `HttpContext.User` (`ClaimTypes.NameIdentifier`), and exposes `HttpContext.GetCurrentSession()` from `ImAI.Api.Sessions`. Missing, invalid, or expired sessions get 401 on protected routes. Validation does not extend expiry. Refresh atomically replaces the hash and renews expiry for seven days; only one concurrent refresh succeeds.

Rejected requests do not clear cookies: a stale concurrent request must not delete the replacement cookie from a successful refresh. Browser expiry removes expired cookies, and creating a session replaces invalid ones. Session responses and authentication failures are marked `no-store`. Unsafe browser requests from untrusted origins are rejected, including multipart submissions.

## Architecture and review decisions

| Area | Implementation and reason | Pattern |
| --- | --- | --- |
| API | Endpoint classes bind HTTP input; chat and upload delegate to application handlers | Minimal API, thin transport layer |
| Application | One real `SendChatQuery` handler replaces the obsolete dummy request/response path; `ReceiveDocument` handles ingestion validation | Mediator / request-handler pattern through MediatR |
| Infrastructure | Azure, SQLite, session hashing, and storage implement application interfaces | Dependency inversion, adapters |
| Sessions | One metadata response replaces the redundant always-valid DTO; token writing is a separate port | Interface segregation, adapter pattern |
| Refresh | Conditional database update includes the original hash and expiry | Optimistic concurrency / compare-and-swap |
| Azure client | DI owns one thread-safe `IChatClient`; scoped service consumes it | Dependency injection and managed lifetime |
| Persistence | EF Core maps sessions, conversations, messages, and relationships; token hash is indexed | DbContext unit of work, EF migrations |
| HTTP policies | Shared normalized origin configuration; centralized Problem Details trace IDs; cancellations avoid false 500 errors | Middleware pipeline, single responsibility |

Removed `DummyChatResponder`, `IChatResponder`, the obsolete `ChatReply` contract, unused `Azure.Search.Documents`, duplicate nullable/implicit-using project settings, and the unused cookie-clear method. Compiler settings are centralized in `Directory.Build.props`. Fixed test-host disposal and updated regression coverage.

Retained the document storage stub because upload is an active endpoint and real blob integration is deferred. Retained Azure Blob and Microsoft.Extensions.AI packages as previously requested integration dependencies. Retained conversation/message models, schema migrations, the EF design-time factory, authentication, origin checks, and the cookie writer: they support current requirements or the explicitly created database schema. A generic repository layer was not added; it would duplicate EF Core without a current need.

## Verification

```sh
dotnet test ImAI.slnx --configuration Release
dotnet format ImAI.slnx style --verify-no-changes --severity warn
```

Tests cover HTTP contracts, input validation, long queries, cookie security, hash-only storage, expiry, rotation races, missing/invalid sessions, origin validation, global errors, cancellations, client registration, SQLite migrations, and relational foreign keys. Tests use fake chat services and isolated SQLite databases; they do not call Azure.
