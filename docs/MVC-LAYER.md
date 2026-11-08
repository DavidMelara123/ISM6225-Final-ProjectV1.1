# MVC & Experience layer (Joel)

Chat page, About/Architecture page, controllers, view models, conversation history,
suggested questions, responsive styling, validation, anti-forgery, friendly errors,
and display of tool evidence and model/fallback mode.

## How it connects to the rest of the app

```
ChatController ──> IChatbotService ──> OrchestratorChatbotService ──> ChatOrchestrator (Morgan) ──> MCP tools ──> SQLite
```

- `Services/Chat/ChatContracts.cs` — the interface the MVC layer depends on (`IChatbotService`)
  and the types it returns: answer, mode, model name, tool calls, discovered tools.
- `Services/Chat/OrchestratorChatbotService.cs` — adapter over the existing `ChatOrchestrator`.
  It works out the mode from the `Ai:*` settings and lists the tools through MCP runtime discovery
  (cached for 5 minutes) for the status badge and the About page.
- The controllers contain no SQL; all data goes through the chatbot service and MCP tools.

## For Morgan: per-answer tool evidence

`ChatOrchestrator.AskAsync` returns only a string, so the "Tool evidence" panel under each answer
currently says "No tool calls were reported for this answer." To fill it in, the orchestrator needs
to return the tool calls it made. Either:

- have `ChatOrchestrator` implement `IChatbotService` directly (register it in `Program.cs` and
  delete the adapter), or
- return something richer from `AskAsync` and pass it through in the adapter.

Where the data comes from:
- **Fallback mode:** the tool name, the `arguments` dictionary and the result text are already in
  `AskDemoAsync` — wrap them in a `ToolInvocation`.
- **Model mode:** after `GetResponseAsync`, `response.Messages` contains `FunctionCallContent`
  (tool name + arguments) and `FunctionResultContent` (result) for every tool the model called.

The UI shows answers as plain text, so the system prompt should ask the model not to use Markdown.

## Settings (`appsettings.json` → `ChatUi`)

| Setting | Default | Meaning |
|---|---|---|
| `TimeoutSeconds` | 45 | How long to wait for an answer before showing a timeout message |
| `MaxStoredMessages` | 30 | Messages kept in the session history |
| `HistoryTurnsSentToService` | 8 | Recent turns passed to the chatbot service |
| `RequestsPerMinute` | 10 | Questions allowed per visitor per minute |
| `SuggestedQuestions` | 6 questions | Buttons shown beside the chat |

When the final tool names are settled, update the planned-tool list in `HomeController`
so the About page's "Discovered" column matches.

## Notes for deployment (David)

- Conversation history is held in in-memory session state: fine for a single App Service
  instance, cleared on restart. The SQLite data is separate.
- Cookies become Secure automatically under HTTPS.
- Friendly error pages are not applied to `/mcp`, so MCP clients still get raw protocol responses.
- The Content-Security-Policy only allows this site's own scripts and styles — no inline
  `<script>` or `style=""` in views.

## Test evidence (Joel)

| ID | Test | How | Expected |
|---|---|---|---|
| T-J01 | Empty input (A8) | Send an empty or spaces-only question | "Please type a question before sending." |
| T-J02 | Too short (A8) | Send `a` | "Questions must be between 3 and 500 characters." |
| T-J03 | Long input | Paste 600+ characters | Box stops at 500; server rejects anything longer (HTTP 400) |
| T-J04 | Anti-forgery | Dev tools → delete the hidden `__RequestVerificationToken` → Send | "Your form couldn't be sent" page (HTTP 400) |
| T-J05 | Conversation history | Ask 3 questions, refresh | Turns still shown; refresh doesn't resend; Clear empties it |
| T-J06 | Mode indicator | Check badge with and without an API key | "Fallback mode (no AI model)" / "AI model · gpt-5-mini" |
| T-J07 | Tool evidence | Expand "Tool evidence" under an answer | Tool, arguments, result, time (once the orchestrator reports calls) |
| T-J08 | Responsive layout | Dev tools → phone size | No sideways scrolling; menu collapses |
| T-J09 | Tool failure | Set `Mcp:Endpoint` to `http://127.0.0.1:1/mcp` in `appsettings.Development.json`, ask anything | Friendly "couldn't get an answer" message; details only in the log |
| T-J10 | Timeout | Set `Mcp:Endpoint` to `http://10.255.255.1/mcp` and `ChatUi:TimeoutSeconds` to 5 | "That took longer than 5 seconds…" |
| T-J11 | HTML injection | Ask `<script>alert(1)</script> campaign 1` | Shown as text, no popup |
| T-J12 | Rate limit | Send 11+ questions in a minute | "Slow down a little" page (HTTP 429) |
| T-J13 | Not found | Visit `/nope` | "Page not found" page |

Undo the `appsettings.Development.json` changes after T-J09/T-J10.
