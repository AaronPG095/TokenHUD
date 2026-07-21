# Token HUD

Token HUD makes the otherwise hidden token state of local Codex chats continuously visible and understandable.

## Language

**Context Load**:
The tokens currently occupying a chat's available model context, expressed as a count and percentage of the context window.
_Avoid_: Token usage, context usage

**Context Budget**:
The resolved maximum desirable Context Load for one chat. It inherits from the installation default, may be overridden by provider and then chat, and is capped below the model's reported technical context limit. Aaron's installation default is 150,000 tokens.
_Avoid_: Context window, token limit, account quota

**Budget Zone**:
A Context Load classification relative to the Context Budget: healthy, caution, critical, or over budget.
_Avoid_: Usage tier, rate-limit status

**Context Velocity**:
The recent rate at which a chat's Context Load is changing, used to estimate how soon it may enter a higher Budget Zone.
_Avoid_: Token speed, burn rate

**Token Spike**:
A single observed increase in Context Load that is unusually large relative to the chat's recent changes.
_Avoid_: Expensive prompt, token leak

**Account Quota**:
The remaining plan allowance reported for a time-bounded usage window, such as the five-hour or weekly window.
_Avoid_: Token balance, session consumption

**Top-level Chat**:
A user-started Codex chat that may own one or more spawned subagent chats.
_Avoid_: Parent session, root task

**Chat Name**:
The current user-visible name assigned to a Codex chat. It may change independently of the initial request and is the only name the HUD should present.
_Avoid_: Thread title, first prompt, first user message

**Chat Tree**:
A Top-level Chat together with all of its spawned descendant chats. Context Load is always reported per chat rather than combined across the tree.
_Avoid_: Combined context, agent session

**Running Subagent**:
A spawned chat whose Codex edge is open and whose metadata has changed within the last 30 minutes. An older open edge is treated as inactive until it changes again, preventing abandoned edge records from creating stale warnings.
_Avoid_: Open edge, child thread

**Tracked Chat**:
The top-level chat whose metrics occupy the HUD's primary view, normally the most recently active one.
_Avoid_: Active chat, current session

**Pinned Chat**:
A top-level chat explicitly chosen to remain the Tracked Chat while other chats continue working.
_Avoid_: Locked session, favorite chat

**Recent Chat List**:
The six most recently active Top-level Chats, ordered by the latest activity anywhere in each Chat Tree. It is the HUD's primary compact surface rather than a detail view of one Tracked Chat.
_Avoid_: Chat carousel, session dashboard

**Observation**:
Reading and presenting a chat's state without changing that chat or starting Codex work.
_Avoid_: Passive action, monitoring command

**Chat Action**:
An explicit, user-initiated operation that changes a provider chat through a supported capability, such as starting context compaction.
_Avoid_: HUD command, automatic fix

**Context Compaction**:
A Chat Action that summarizes older working history to reduce Context Load while preserving continuity in the existing chat.
_Avoid_: Clear context, delete history, start fresh

**Context Reset**:
A Chat Action that removes accumulated working history from the model-visible context while retaining the same chat identity and keeping its earlier transcript readable to the user. Persistent instructions may load again after the reset, so Context Load does not necessarily become zero.
_Avoid_: Context Compaction, new chat, delete chat

**Compact View**:
The small, continuously visible HUD surface showing the Recent Chat List, essential per-chat metrics, and Account Quota.
_Avoid_: Mini dashboard, widget

**Expanded View**:
The detailed HUD surface for comparing chats, inspecting a Chat Tree, searching history, and changing preferences.
_Avoid_: Full dashboard, settings window

**Privacy Mode**:
A display state that masks chat titles and project paths while preserving token metrics and operational status.
_Avoid_: Incognito mode, private monitoring

**Local-first**:
A permanent product constraint under which core functionality runs on the user's device without an account, monitored content and telemetry stay on-device by default, and any future cloud feature is optional and data-minimized.
_Avoid_: Offline-only, local storage mode
