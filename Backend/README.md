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
- Azure credentials: Development uses `AzureCliCredential` to match the signed-in Azure CLI identity. `AzureOpenAI:TenantId` pins the resource tenant, and optional `AzureOpenAI:AzureCliDirectory` adds a portable CLI directory to the application process PATH. Outside Development, the default credential mode is `Default`. Set `AzureOpenAI:Credential` to `AzureCli` or `Default` explicitly to override it.
- `ConnectionStrings:ImAI`: optional SQLite connection string. Default: `Backend/imai.db` in this checkout, or `imai.db` beside published binaries. `ConnectionStrings__ImAI` overrides both runtime configuration and EF tooling. The design-time factory otherwise uses the default path; it does not load API appsettings files.
- `ConversationContext:SystemInstructions`: required system instructions, configured in API `appsettings.json`. Shared by the conversation context reader and Azure chat service; override through environment-specific configuration or `ConversationContext__SystemInstructions`.
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
| Conversations | Application handler builds summary-aware context; a specialized store queries by session ownership and atomically saves both messages | MediatR, persistence adapter, optimistic concurrency |
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
3. For follow-ups, send `{ "query": "Explain more", "conversationId": "ID_FROM_PREVIOUS_RESPONSE" }` with the same session cookie. The server validates ownership, reads summary-aware context, calls AI, and saves the new exchange.
4. To start another chat, omit `conversationId` again. Both conversations remain associated with the same session and their histories stay separate.

Both message rows and the conversation timestamp are committed together only after the AI call succeeds. Failed AI calls do not leave partial exchanges. `UpdatedAt` acts as an EF concurrency token: if two prompts read the same history and compete to save, one receives 409 and must retry against the updated history. No database transaction is held open while waiting for AI. Session refresh retains conversation ownership because the session ID does not change.

## Conversation context reader

`IConversationContextReader` is an Application abstraction implemented by the scoped Infrastructure `ConversationContextReader`. Call `ReadAsync(sessionId, conversationId, currentMessage, cancellationToken)` to obtain a `ConversationContextDto` from `Application/Dtos`:

```json
{
  "systemInstructions": "You are ImAI, an intelligent portfolio assistant.",
  "conversationSummary": "The user is exploring RAG architecture and Azure AI Search.",
  "recentMessages": [
    { "role": "user", "content": "What is vector search?" },
    { "role": "assistant", "content": "Vector search retrieves information based on semantic similarity." }
  ],
  "retrievedKnowledge": [],
  "currentMessage": "Does Azure AI Search support hybrid search?"
}
```

The reader projects directly from the unique conversation summary and related messages into DTOs using an untracked EF query. It includes only messages with `Sequence > LastSummarizedSequence`, in ascending sequence order. If there is no summary, or the conversation is unknown or belongs to another session, it returns `null`. A summary with no later messages still returns a context with an empty `recentMessages` list. `currentMessage` is supplied by the caller and remains separate from stored history; `retrievedKnowledge` is an empty string list until retrieval is implemented.

Message sequences start at 1 independently for each conversation and increase by one per message (user, then assistant). A unique `(ConversationId, Sequence)` index enforces uniqueness. The sequence migration backfills existing rows in `CreatedAt`, then `Id` order before creating the index. Run the database update command above for existing databases.

The chat handler calls the reader before generation and passes `ConversationContextDto` to `IAzureChatService`. If no summary exists, it builds a context with configured instructions, empty summary/history/knowledge, and the current query, preserving the reader's no-summary rule. The Azure adapter sends the configured system instructions, optional JSON summary/knowledge reference data as a user message, recent messages with their actual user/assistant roles, then the current prompt exactly once. Reference data is not promoted to system instructions. The current exchange is saved only after generation succeeds, so it is not duplicated in recent history. The reader does not create or update summaries; automatic summarization and knowledge retrieval are still deferred.

## Temporary SQLite explorer

Open **https://localhost:7106/dev/database** while running the backend in Development. The explorer is a separate optional project in `tools/ImAI.SqliteExplorer`, mounted by extension methods in `Program.cs`. It reads the same effective SQLite connection string as `ImAiDbContext`; there is no copied database or separate connection configuration.

- The browser view lists tables/views and columns, previews table rows, and runs SQL queries with horizontally scrollable results. Use Ctrl/Cmd+Enter to run the editor query.
- `GET /dev/database/tables` returns table and column metadata; `POST /dev/database/query` accepts `{ "sql": "SELECT * FROM ChatMessage ORDER BY ConversationId, Sequence" }` and returns `columns`, `rows`, and `truncated`.
- Backend development code can inject the scoped `ImAI.SqliteExplorer.ISqliteExplorer` and call `GetTablesAsync(cancellationToken)` or `QueryAsync(sql, cancellationToken)`. These operations use the same read-only implementation as the browser endpoints.
- Queries accept one SELECT, WITH, or EXPLAIN statement and return at most 500 rows. The SQLite connection itself uses `Mode=ReadOnly` and `PRAGMA query_only=ON`, including when used directly from backend code. Write statements and multiple statements are rejected. No authentication/session cookie is needed for this local development tool; it requires a localhost request and same-origin browser access. Responses are not cached, and endpoints are omitted from OpenAPI.
- `SqliteExplorer:Enabled` in `appsettings.Development.json` controls the component. Set it to `false` and restart to disable it. It is never mapped outside Development, even if the flag is enabled.

To remove the component entirely, delete its `AddSqliteExplorer` block and `MapSqliteExplorer()` call (and associated usings) in `Program.cs`, remove the API project reference and solution entry, delete `tools/ImAI.SqliteExplorer`, remove its tests, and delete the `SqliteExplorer` configuration section. Application, Domain, and Infrastructure do not reference this tool and need no changes.

## Conversation history endpoints

- `GET /api/conversations`: lists the current session's saved conversations.
- `GET /api/conversations/{id}`: loads that session's conversation and ordered messages.
- `DELETE /api/conversations/{id}`: deletes the conversation, its messages, and its summary in a single transaction.

All require the session cookie. Unknown and foreign conversation IDs return 404. The frontend loads this list when the chat screen opens, loads messages when a saved conversation is selected, and retains each returned conversation ID independently for follow-up prompts.

## Troubleshooting live AI authentication

If chat fails while session creation succeeds, check the backend terminal exception. The former CLI-only configuration failed with `Azure CLI not installed`. The backend now uses `DefaultAzureCredential`; it still needs a valid Azure identity with access to the OpenAI resource. For Azure CLI, install it and run `az login`, then restart the backend from a new terminal so PATH and credentials are available. Missing Azure credentials produce a 503 Problem Details response with a safe message; detailed credential errors stay in server logs.

## Azure OpenAI 401 investigation (2026-11-24)

The repository DI pipeline reproduced 401 while a raw REST request and the SDK adapter using the CLI credential succeeded. `DefaultAzureCredential` selected `VisualStudioCredential` rather than the CLI identity. Its token tenant was `f8cdef31-a31e-4b4a-93e4-5f571e91255a`, while the working CLI token tenant was `e140db0a-9392-48f3-9a07-3ef59e125f23`. Both requested the Cognitive Services audience; identity selection, not an incorrect scope, caused the observed 401.

Runtime inspection found one `IChatClient` registration and no API user-secrets ID. Endpoint and deployment came from `appsettings.Development.json`, with no environment overrides for those keys in the inspected process. The portable CLI directory was absent from the inherited PATH. Development now explicitly selects `AzureCliCredential`, pins the resource tenant, and supports the configured portable directory without changing the machine PATH. The client explicitly uses API version `2024-10-21` and `AzureOpenAIAudience.AzurePublicCloud`, producing scope `https://cognitiveservices.azure.com/.default`. Non-secret endpoint/deployment/credential/tenant configuration is logged when the lazy client is created.

Verified using the unchanged Azure.AI.OpenAI 2.1.0, Azure.Identity 1.21.0, OpenAI 2.14.0, Microsoft.Extensions.AI.OpenAI 11.0.0 and System.ClientModel 1.15.0 dependency set:

| Isolation layer | Result |
| --- | --- |
| AzureCliCredential token acquisition | Succeeded; correct resource tenant and Cognitive Services audience |
| Raw HTTP with that token | 200 |
| AzureOpenAIClient binary-content request without adapter | 200 |
| AzureOpenAIClient strongly typed request without adapter | MissingMethodException for ChatCompletionOptions.get_SerializedAdditionalRawData before HTTP; separate version-compatibility issue |
| Microsoft.Extensions.AI adapter using CLI | Succeeded |
| Original DI with DefaultAzureCredential | Visual Studio identity selected; HTTP 401 |
| Fixed application DI/service pipeline | Succeeded |
| Actual session-authenticated ImAI HTTP endpoint | First/follow-up prompts 200; same conversation; four persisted messages; diagnostic conversation deleted afterward |

All emitted SDK/REST chat requests targeted `https://aoai-imai-dev.openai.azure.com/openai/deployments/gpt-4.1-mini/chat/completions?api-version=2024-10-21`. No package upgrades were made: the strongly typed overload compatibility problem does not affect the active adapter path and should be addressed separately before using that overload.

The manual tool `dotnet run --project tools/AzureDiagnostics` reproduces the individual layers using existing project references. It makes small live Azure requests (normal inference charges apply), prints configuration provenance, requested scopes, tenant/audience and an identity fingerprint, and never prints access tokens or Authorization header values. The DefaultAzureCredential comparison intentionally exercises the former identity-selection behavior; its failure is not an automated test failure. The tool is excluded from the solution and normal CI tests.
