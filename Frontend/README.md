# ImAI frontend

Angular frontend for a RAG workspace with a dark cyber-inspired theme. The default route opens chat; the sidebar provides document management and conversation history.

## Run locally

```sh
npm install
npm start
```

Open `https://localhost:4200`. The Angular dev server uses HTTPS because session cookies are Secure. Accept or trust the local development certificate when prompted. On Windows PowerShell, use `npm.cmd` if script execution policy blocks `npm`.

## Available flows

- Chat: real backend calls, saved conversation history, independent conversations, AI replies, send-failure retries, and persisted conversation deletion.
- Documents: search, collection filters, library statistics, file picker and drag-and-drop, simulated indexing, and confirmed deletion.
- Access: visitors start as guests and can browse or chat. Upload and deletion prompt for member preview. Any valid email format starts a local demo member session; no identity verification or password authentication is performed.
- A demo session is silently restored from this tab's session storage for eight hours. Sign-out clears it and restores guest permissions. No authentication tokens are generated or sent.

## Frontend scope

Chat calls and conversation history use the backend. Document ingestion and indexing in the document UI remain simulations. Selected file metadata is used, but file bytes are not uploaded or parsed. The document demo is stored in memory and resets on refresh. Chat conversations/messages are stored in backend SQLite and reloaded for the session. The demo member-login state uses session storage and is separate from the backend HttpOnly session cookie.

Real user-account validation, silent SSO, persistent document storage, document parsing, and retrieval remain future integration tasks. Frontend permission checks demonstrate behavior; server endpoints must enforce permissions when connected.

The existing `/settings` and `/explorer` prototype routes remain available for future integration, outside the primary navigation.

## Validation

```sh
npm run build
npm test -- --watch=false
```

Tests cover guest restrictions, restored and expired sessions, invalid email input, file validation, and member deletion.

## Backend session initialization

ImAI calls `POST /api/sessions` during Angular application initialization using `provideAppInitializer`. Requests include browser credentials; the API sets or reuses the Secure HttpOnly session cookie. Only `sessionId` and `expiresAt` are stored in frontend memory. The token is never read by JavaScript or stored in local/session storage. Reloading calls the endpoint again so the backend can validate and reuse the existing cookie.

Start the backend with `dotnet run --project Backend/src/ImAI.Api --launch-profile https` from the repository root, then run `npm start` in `Frontend/`. The HTTPS development proxy forwards `/api/**` to `https://localhost:7106`. Its `secure: false` option permits the backend development certificate only in the local proxy. Production hosting should route `/api` to the backend over its normal trusted connection. Override the `API_BASE_URL` injection token for a separately hosted API and configure credentialed CORS accordingly.

The initializer shares concurrent initialization calls, times out after eight seconds, and allows the UI to load with a retry notice if the backend is unavailable. Later API requests also include credentials through the API-scoped interceptor. This anonymous backend session is separate from the existing demo member-login UI. Chat now uses the live AI endpoint; document UI interactions remain simulated.

## Live chat workflow

After startup session creation/reuse, the chat screen loads `GET /api/conversations`. The first send posts `{ query }` to `/api/ai/chat`; the backend atomically saves the conversation and both messages after generation succeeds. The response supplies the conversation ID, saved assistant message ID, and answer. Follow-ups post `{ query, conversationId }`; only the current query is sent to AI, without document context, saved history, or summaries.

New chat starts an independent draft and receives a different server ID on first send. Switching chats retains the correct ID. Selecting a saved conversation loads its ordered messages from the backend. Deletion requires confirmation and removes the saved conversation. Failed prompts remain marked with a retry action; no AI response or source citation is fabricated. A delayed reply is attached to the thread that sent it, even if another thread is selected.
