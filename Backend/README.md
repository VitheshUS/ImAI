# ImAI backend

.NET 10 Minimal API with Clean Architecture, MediatR, Azure OpenAI chat, and SQLite-backed anonymous sessions. Document ingestion remains an explicit stub; file bytes are discarded. Conversations and messages are persisted in SQLite and scoped to the anonymous session. Retrieval is not connected yet.

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

Queries must contain non-whitespace text. Omitting or sending null `conversationId` starts a new conversation under the authenticated session. Reuse the returned ID on subsequent prompts to continue that conversation. A supplied ID must be non-empty and belong to the current session; unknown and foreign IDs return 404. Every successful exchange saves a user message and an assistant message. The returned `messageId` is the saved assistant message ID. Earlier messages are available for display, but only the current user query is sent to AI in this phase. `source` remains empty until retrieval is integrated.

Uploads accept PDF, DOCX, Markdown, TXT, CSV, and JSON extensions. Files must be non-empty and at most 50 MiB. The 51 MiB server request limit includes multipart overhead and derives from the same file-size constant. Extension checks do not inspect document contents.

## Configuration

- `AzureOpenAI:Endpoint`: HTTPS Azure OpenAI resource endpoint.
- `AzureOpenAI:ChatDeployment`: deployment name. The shared SDK client is created lazily, so cloud configuration is needed only when chat is used.
- Azure credentials: the current implementation retains `AzureCliCredential`; sign in with Azure CLI for live local calls. Managed identity configuration is a future deployment task.
- `ConnectionStrings:ImAI`: optional SQLite connection string. Default: `Backend/imai.db` in this checkout, or `imai.db` beside published binaries. `ConnectionStrings__ImAI` overrides both runtime configuration and EF tooling. The design-time factory otherwise uses the default path; it does not load API appsettings files.
- `Cors:AllowedOrigins`: exact HTTP(S) origins. Defaults: `http://localhost:4200` and `https://localhost:4200`. CORS and POST origin validation share one normalized configuration. Wildcards, credentials, and non-root paths are rejected.

Use the same site and scheme for frontend and API with SameSite=Lax cookies; different HTTPS localhost ports are supported. The frontend initializes its session and connects chat, conversation history, and conversation deletion to this API. Document management retains its frontend demo implementation.

## Session behavior

Tokens use 32 cryptographically random bytes encoded as hexadecimal; only their SHA-256 hash is stored. The token is delivered only through `Set-Cookie`: `__Host-ImAI.Session`, `HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/`, no Domain, seven-day UTC expiry. Service and HTTP responses contain only session metadata. `ISessionTokenWriter` keeps HTTP cookie handling outside Infrastructure.

Authentication validates cookies against SQLite on incoming requests, sets `HttpContext.User` (`ClaimTypes.NameIdentifier`), and exposes `HttpContext.GetCurrentSession()` from `ImAI.Api.Extensions`. Missing, invalid, or expired sessions get 401 on protected routes. Validation does not extend expiry. Refresh atomically replaces the hash and renews expiry for seven days; only one concurrent refresh succeeds.

Rejected requests do not clear cookies: a stale concurrent request must not delete the replacement cookie from a successful refresh. Browser expiry removes expired cookies, and creating a session replaces invalid ones. Session responses and authentication failures are marked `no-store`. Unsafe browser requests from untrusted origins are rejected, including multipart submissions.

## Architecture and review decisions

| Area | Implementation and reason | Pattern |
| --- | --- | --- |
| API | Endpoint classes bind HTTP input; chat and upload delegate to application handlers | Minimal API, thin transport layer |
| Application | One real `SendChatQuery` handler replaces the obsolete dummy request/response path; `ReceiveDocument` handles ingestion validation | Mediator / request-handler pattern through MediatR |
| Infrastructure | Azure, SQLite, session hashing, and storage implement application interfaces | Dependency inversion, adapters |
| Sessions | One metadata response replaces the redundant always-valid DTO; token writing is a separate port | Interface segregation, adapter pattern |
| Conversations | Application handler sends the current query; a specialized store queries by session ownership and atomically saves both messages | MediatR, persistence adapter, optimistic concurrency |
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

## API folder organization

- `Middleware/`: request-origin middleware and global exception handler.
- `Authentication/`: anonymous-session authentication handler.
- `Cookies/`: secure session cookie helper.
- `Configuration/`: browser-origin configuration and matching policy.
- `Extensions/`: HttpContext current-session extension.
- `Endpoints/`: Minimal API route definitions.

Each folder has a matching namespace. API DI registration remains in the API project's root `DependencyInjection.cs`.

## Conversation workflow

1. Call `POST /api/sessions` once and retain the HttpOnly cookie. Calling it again with the same valid cookie reuses the session and does not rotate the token or extend its expiry. A missing/expired cookie requires a new session. Tabs in the same browser profile share the cookie.
2. Send `POST /api/ai/chat` with `{ "query": "Hello" }` to start a conversation. The response includes its `conversationId`.
3. For follow-ups, send `{ "query": "Explain more", "conversationId": "ID_FROM_PREVIOUS_RESPONSE" }` with the same session cookie. The server validates ownership and saves the new exchange. Stored history is available for display but is not sent to AI.
4. To start another chat, omit `conversationId` again. Both conversations remain associated with the same session and their histories stay separate.

Both message rows and the conversation timestamp are committed together only after the AI call succeeds. Failed AI calls do not leave partial exchanges. `UpdatedAt` acts as an EF concurrency token: if two prompts read the same history and compete to save, one receives 409 and must retry against the updated history. No database transaction is held open while waiting for AI. The current implementation sends only the current query to AI, plus the fixed system instruction. Document context, saved history, and summaries are not added. Session refresh retains conversation ownership because the session ID does not change.

## Conversation history endpoints

- `GET /api/conversations`: lists the current session's saved conversations.
- `GET /api/conversations/{id}`: loads that session's conversation and ordered messages.
- `DELETE /api/conversations/{id}`: deletes the conversation, its messages, and its summary in a single transaction.

All require the session cookie. Unknown and foreign conversation IDs return 404. The frontend loads this list when the chat screen opens, loads messages when a saved conversation is selected, and retains each returned conversation ID independently for follow-up prompts.
