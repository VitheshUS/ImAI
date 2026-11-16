# ImAI frontend

Angular frontend for a RAG workspace with a dark cyber-inspired theme. The default route opens chat; the sidebar provides document management and conversation history.

## Run locally

```sh
npm install
npm start
```

Open `http://localhost:4200`. On Windows PowerShell, use `npm.cmd` if script execution policy blocks `npm`.

## Available flows

- Chat: new conversations, starter prompts, collection selection, sample responses, and a source inspector.
- Documents: search, collection filters, library statistics, file picker and drag-and-drop, simulated indexing, and confirmed deletion.
- Access: visitors start as guests and can browse or chat. Upload and deletion prompt for member preview. Any valid email format starts a local demo member session; no identity verification or password authentication is performed.
- A demo session is silently restored from this tab's session storage for eight hours. Sign-out clears it and restores guest permissions. No authentication tokens are generated or sent.

## Frontend scope

Chat responses, citations, document ingestion, and indexing are simulations. Selected file metadata is used, but file bytes are not uploaded or parsed. Documents and conversations are stored in memory and reset on refresh. Session state uses session storage and is separate from the in-memory library.

Real user validation, silent SSO, authorization, persistent file storage, document parsing, and RAG responses require backend integration. Frontend permission checks demonstrate behavior; server endpoints must enforce permissions when connected.

The existing `/settings` and `/explorer` prototype routes remain available for future integration, outside the primary navigation.

## Validation

```sh
npm run build
npm test -- --watch=false
```

Tests cover guest restrictions, restored and expired sessions, invalid email input, file validation, and member deletion.
