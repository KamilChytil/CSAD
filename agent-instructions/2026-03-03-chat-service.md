# 2026-03-03 — Chat Service (SignalR + Conversation Rooms)

## Souhrn změn

Implementována kompletní chatovací služba s real-time komunikací přes SignalR. Nejprve základní 1:1 chat, poté přepracováno na model konverzačních místností (Support a Family typy).

**38+ změněných / nových souborů, +1 300 řádků**

---

## 1. Chat mikroslužba (nová)

### Struktura projektu
```
src/Services/Chat/
├── FairBank.Chat.Domain/
│   ├── Aggregates/ChatMessage.cs       — entita zprávy (ConversationId, SenderId, SenderName, Content, SentAt)
│   ├── Aggregates/Conversation.cs      — konverzační místnost (Type, ClientOrChildId, BankerOrParentId, Label)
│   ├── Enums/ConversationType.cs       — Support | Family
│   └── Ports/IChatRepository.cs        — rozhraní repozitáře
│   └── Ports/IConversationRepository.cs
├── FairBank.Chat.Application/
│   ├── Hubs/ChatHub.cs                 — SignalR hub (group-based)
│   ├── Conversations/Queries/          — GetConversationsQuery, GetParentConversationsQuery
│   └── Messages/Commands/SendMessage/  — SendMessageCommand
│   └── Messages/Queries/GetConversation/
├── FairBank.Chat.Infrastructure/
│   ├── Persistence/ChatDbContext.cs    — EF Core kontext (schema: chat_service)
│   └── Persistence/Repositories/      — ChatRepository, ConversationRepository
└── FairBank.Chat.Api/
    └── Program.cs                      — REST endpointy + SignalR hub
```

---

## 2. Typy konverzací

| Typ | Účastníci | Přístup |
|-----|-----------|---------|
| **Support** | Klient ↔ libovolný bankéř | Klient vidí jednu místnost „Banker support"; bankéři vidí seznam všech klientů |
| **Family** | Dítě ↔ Rodič | Každé dítě má separátní místnost; rodič vidí všechny children |

### Pravidla přístupu
- **Employee / Admin** → všechny Support konverzace
- **Client** → vlastní Support konverzace (vytvoří se automaticky při prvním přístupu) + případně Family (pokud je dítětem)
- **Parent** → Support konverzace + seznam Family místností (jedna na každé dítě)
- **Child** → Support konverzace + jedna Family místnost s rodičem

---

## 3. API endpointy (chat-api)

| Metoda | Cesta | Popis |
|--------|-------|-------|
| `GET` | `/health` | Health check |
| `GET` | `/api/v1/chat/conversations?userId=&role=&label=&parentId=` | Seznam konverzací pro uživatele |
| `GET` | `/api/v1/chat/conversations/{id}/messages` | Historie zpráv konverzace |
| `POST` | `/api/v1/chat/conversations/family?parentId=&childId=&childLabel=` | Vytvoří nebo vrátí Family místnost |

### SignalR hub (`/chat-hub`)

```csharp
// Klient se připojí do místnosti
HubProxy.InvokeAsync("JoinConversation", conversationId.ToString())

// Odeslání zprávy do místnosti
HubProxy.InvokeAsync("SendMessage", conversationId, senderId, senderName, content)

// Příjem zpráv
HubConnection.On("ReceiveMessage", (msg) => { ... })
// msg: { Id, ConversationId, SenderId, SenderName, Content, SentAt }
```

Skupiny jsou pojmenovány `conv-{conversationId}` — více bankéřů / rodičů může být v jedné místnosti najednou.

---

## 4. API Gateway (YARP)

Přidány dvě YARP route skupiny v `appsettings.json`:
- `/api/v1/chat/{**catch-all}` → `chat-api:8080`
- `/chat-hub/{**catch-all}` → `chat-api:8080` (WebSocket upgrade)

---

## 5. Nginx (`nginx.conf` + `nginx.conf.template`)

Přidány location bloky pro SignalR WebSocket:

```nginx
location /chat-hub {
    # OPTIONS preflight pro CORS
    proxy_pass         http://api-gateway:8080;
    proxy_http_version 1.1;
    proxy_set_header   Upgrade $http_upgrade;
    proxy_set_header   Connection $http_connection;
    proxy_cache_bypass $http_upgrade;
}

location /chat/health {
    proxy_pass http://api-gateway:8080;
}
```

---

## 6. Frontend (Blazor WASM)

### Nové / upravené soubory
- `FairBank.Web.Shared/Models/Chat/ConversationDto.cs` — DTO konverzace
- `FairBank.Web.Shared/Models/Chat/ChatMessageDto.cs` — upraven (ConversationId, SenderName místo ReceiverId)
- `FairBank.Web.Shared/Services/Chat/ChatService.cs` — přepsáno
  - `GetConversationsAsync(userId, role, label, parentId?)` — seznam konverzací
  - `GetConversationMessagesAsync(conversationId)` — historie
  - `InitializeAsync(token)` — připojení k SignalR hubu
  - `JoinConversationAsync(id)` / `LeaveConversationAsync(id)` / `SendMessageAsync(...)`
- `FairBank.Web/Pages/ChatList.razor` — nová stránka `/zpravy` (seznam konverzací)
- `FairBank.Web/Pages/Chat.razor` — upraven, route `/zpravy/{ConversationId:guid}`
  - Zobrazuje jméno odesílatele u přijatých zpráv
  - Tlačítko zpět na seznam
  - Deduplicace echo zpráv
- `SideNav.razor` + `BottomNav.razor` — link na `/zpravy` (bez hardkódovaných ID)

---

## 7. Docker

Přidána služba `chat-api` do `docker-compose.yml`:
```yaml
chat-api:
  build: { context: ., dockerfile: src/Services/Chat/FairBank.Chat.Api/Dockerfile }
  container_name: fairbank-chat-api
  environment:
    ConnectionStrings__DefaultConnection: "Host=postgres-primary;...;Search Path=chat_service"
  depends_on:
    postgres-primary: { condition: service_healthy }
```

---

## 8. Databáze

Schema `chat_service` v PostgreSQL, tabulky vytvořeny manuálně (EF `EnsureCreatedAsync` neaktualizuje existující schema):

```sql
CREATE TABLE chat_service.conversations (
    "Id"               uuid PRIMARY KEY,
    "Type"             varchar(20) NOT NULL,      -- 'Support' | 'Family'
    "ClientOrChildId"  uuid NOT NULL,
    "BankerOrParentId" uuid,                      -- NULL = libovolný bankéř
    "Label"            varchar(200) NOT NULL,
    "CreatedAt"        timestamptz NOT NULL
);

CREATE TABLE chat_service.messages (
    "Id"             uuid PRIMARY KEY,
    "ConversationId" uuid REFERENCES conversations("Id") ON DELETE CASCADE,
    "SenderId"       uuid NOT NULL,
    "SenderName"     varchar(200) NOT NULL,
    "Content"        varchar(2000) NOT NULL,
    "SentAt"         timestamptz NOT NULL
);
```

DB uživatel: `fairbank_app` musí mít `GRANT ALL ON SCHEMA chat_service`.

---

## Diff (zkrácený)

```diff
 docker-compose.yml                                          |  18 +
 src/FairBank.ApiGateway/appsettings.json                   |  19 +
 src/FairBank.Web.Shared/Models/Chat/ChatMessageDto.cs       |   6 +-
 src/FairBank.Web.Shared/Models/Chat/ConversationDto.cs      |   9 + (new)
 src/FairBank.Web.Shared/Services/Chat/ChatService.cs        |  89 +-
 src/FairBank.Web/Layout/BottomNav.razor                     |  14 +-
 src/FairBank.Web/Layout/SideNav.razor                       |  14 +-
 src/FairBank.Web/Pages/Chat.razor                           | 163 +-
 src/FairBank.Web/Pages/ChatList.razor                       | 138 + (new)
 src/FairBank.Web/nginx.conf                                 |  28 +
 src/FairBank.Web/nginx.conf.template                        |  85 + (new entry)
 src/Services/Chat/FairBank.Chat.Api/Dockerfile              |  24 + (new)
 src/Services/Chat/FairBank.Chat.Api/Program.cs              |  91 + (new)
 src/Services/Chat/FairBank.Chat.Application/... (10 files)  | 250 + (new)
 src/Services/Chat/FairBank.Chat.Domain/... (5 files)        | 120 + (new)
 src/Services/Chat/FairBank.Chat.Infrastructure/... (4 files)| 150 + (new)
```
