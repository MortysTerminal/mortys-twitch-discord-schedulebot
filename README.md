# Twitch → Discord Schedule Bot

A self-hostable .NET 8 worker service that automatically mirrors Twitch stream schedules into Discord scheduled events. Every operator can run the same Docker image with their own channels — no rebuilding required.

> **Hobby project.** Written with care, but offered as-is without warranty.

---

## How it works

```
Twitch Schedule API  ──▶  Bot (every N minutes)  ──▶  Discord Scheduled Events
```

On each sync cycle the bot:

1. Fetches the upcoming schedule for every configured Twitch channel
2. **Creates** new Discord events for entries that don't exist yet
3. **Updates** existing events when the title, time, or category changes
4. **Deletes** Discord events that no longer appear in the Twitch schedule

Events are matched by embedding the Twitch segment ID inside the Discord event description, so the bot never confuses its own events with manually created ones.

---

## Prerequisites

### 1 — Discord Bot

1. Go to <https://discord.com/developers/applications> → **New Application**
2. In the left sidebar choose **Bot** → **Add Bot**
3. Under **Token** click **Reset Token** and copy it — this is your `DISCORD_BOT_TOKEN`
4. Enable the **Server Members Intent** and **Message Content Intent** (under Privileged Gateway Intents) — the bot uses the REST client only, so these are optional, but toggling them avoids future friction
5. Go to **OAuth2 → URL Generator**, tick `bot`, then tick these bot permissions:
   - **Manage Events**
6. Copy the generated URL, open it in a browser, and invite the bot to your server
7. Enable **Developer Mode** in Discord (User Settings → Advanced) to be able to right-click your server and **Copy Server ID** — this is your `DISCORD_GUILD_ID`

### 2 — Twitch Developer App

1. Go to <https://dev.twitch.tv/console> → **Register Your Application**
2. Set **OAuth Redirect URL** to `http://localhost` (only used for the app credential, not OAuth flow)
3. Set **Category** to *Chat Bot* or *Application Integration*
4. After creation, click **Manage** → **New Secret**
5. Copy the **Client ID** (`TWITCH_CLIENT_ID`) and the **Client Secret** (`TWITCH_CLIENT_SECRET`)

> The bot uses the Client Credentials flow — it never asks for a user login.

---

## Configuration reference

All configuration is supplied via **environment variables**. The image ships with only safe defaults (`appsettings.json`) — no secrets, no channels.

### Required

| Environment variable | Description |
|---|---|
| `Discord__BotToken` | Discord bot token |
| `Discord__GuildId` | Numeric ID of your Discord server |
| `Twitch__ClientId` | Twitch developer app client ID |
| `Twitch__ClientSecret` | Twitch developer app client secret |
| `Twitch__Channels__0__Username` | Twitch login name of the first channel (lowercase) |
| `Twitch__Channels__0__DisplayName` | Display name shown in Discord events |

### Additional channels

Use contiguous, zero-based indices. Add as many as you need:

```
Twitch__Channels__1__Username=secondchannel
Twitch__Channels__1__DisplayName=Second Channel
Twitch__Channels__2__Username=thirdchannel
Twitch__Channels__2__DisplayName=Third Channel
```

> **Important:** The index sequence must be contiguous (0, 1, 2 …). A gap stops .NET from reading subsequent entries.

### Optional

| Environment variable | Default | Description |
|---|---|---|
| `Discord__EventLanguage` | `en` | Language used inside Discord events: `en` (English) or `de` (German) |
| `Sync__IntervalMinutes` | `30` | How often (in minutes) the bot syncs with Twitch |
| `Sync__LookaheadDays` | `14` | How many days ahead to include in Discord events |
| `DOTNET_ENVIRONMENT` | *(not set)* | Set to `Production` if you want environment-specific config loading |

---

## Deployment via Portainer (recommended)

The GitHub Actions workflow automatically builds and pushes a fresh image to GHCR on every push to `master`:

```
ghcr.io/mortysterminal/mortys-twitch-discord-schedulebot:latest
```

### Option A — Git-based stack (recommended)

This is the cleanest approach: Portainer pulls `docker-compose.yml` directly from the repository and you supply all secrets as stack environment variables. The image updates automatically when you trigger a re-pull.

1. In Portainer go to **Stacks → Add stack**
2. Choose **Repository**
3. Set the repository URL to your fork (or this repo if public)
4. Leave the compose file path as `docker-compose.yml`
5. Scroll down to **Environment variables** and add every variable from the table above:

   ```
   DISCORD_BOT_TOKEN       = <your token>
   DISCORD_GUILD_ID        = <your server id>
   TWITCH_CLIENT_ID        = <your client id>
   TWITCH_CLIENT_SECRET    = <your client secret>
   TWITCH_CHANNEL_0_USERNAME    = shroud
   TWITCH_CHANNEL_0_DISPLAYNAME = Shroud
   ```

   The `docker-compose.yml` references these with `${...}` placeholders — Portainer substitutes them at deploy time.

6. Click **Deploy the stack**

### Option B — Manual / local Docker

```bash
docker run -d \
  --name schedulebot \
  --restart unless-stopped \
  -e Discord__BotToken="YOUR_TOKEN" \
  -e Discord__GuildId="YOUR_GUILD_ID" \
  -e Twitch__ClientId="YOUR_CLIENT_ID" \
  -e Twitch__ClientSecret="YOUR_CLIENT_SECRET" \
  -e Twitch__Channels__0__Username="somechannel" \
  -e Twitch__Channels__0__DisplayName="Some Channel" \
  ghcr.io/mortysterminal/mortys-twitch-discord-schedulebot:latest
```

---

## Testing guide (Portainer)

### Step 1 — Verify the bot starts and binds channels correctly

After deploying in Portainer, open **Containers → schedulebot → Logs** (or click the log icon on the stack view).

A successful startup looks like this:

```
HH:mm:ss [INF] Mortys Twitch-Discord Schedulebot startet...
HH:mm:ss [INF] TwitchService initialized with 1 channel(s): Shroud
HH:mm:ss [INF] DiscordService initialisiert.
HH:mm:ss [INF] SyncService gestartet – Synchronisation alle 30 Minuten.
HH:mm:ss [INF] Discord Login erfolgreich.
HH:mm:ss [INF] Starte Synchronisation...
```

The `TwitchService initialized with N channel(s): ...` line is your confirmation that the environment variables were read correctly.

### Step 2 — Test the fail-fast behavior (no channels configured)

Deploy the stack once **without** the `TWITCH_CHANNEL_0_USERNAME` / `TWITCH_CHANNEL_0_DISPLAYNAME` variables. The container should exit immediately with:

```
[ERR] No Twitch channels are configured. Set at least one channel pair via environment variables:
      Twitch__Channels__0__Username and Twitch__Channels__0__DisplayName
```

This confirms the guard works and the image is not silently doing nothing. Re-add the variables and redeploy to continue.

### Step 3 — Verify Twitch API credentials

Watch the logs for the first sync cycle. You should see:

```
HH:mm:ss [INF] Twitch Authentifizierung erfolgreich.
HH:mm:ss [INF] Hole Zeitplan für Kanal: somechannel
HH:mm:ss [INF] Kanal somechannel: N Einträge gefunden.
```

If you see an authentication error here, double-check `TWITCH_CLIENT_ID` and `TWITCH_CLIENT_SECRET`.

### Step 4 — Verify Discord events are created

Go to your Discord server → **Events** (the calendar icon in the left sidebar or the server header). After the first sync you should see scheduled events for every upcoming stream on the configured channels.

If events are missing:
- Make sure the bot has the **Manage Events** permission in the server
- Check the logs for `Fehler beim Erstellen des Discord Events` lines
- Confirm the Twitch channel has actual upcoming scheduled segments (not all streamers use the schedule feature)

### Step 5 — Test an update cycle

Change the title of a scheduled segment on Twitch (via the Creator Dashboard → Schedule). Wait for the next sync (or lower `Sync__IntervalMinutes` temporarily to `1`), then check the Discord event — the title should update automatically.

### Step 6 — Test deletion

Cancel a scheduled Twitch segment. After the next sync the corresponding Discord event should disappear.

### Step 7 — Test with multiple channels

Add a second channel pair:

```
TWITCH_CHANNEL_1_USERNAME    = anotherchannel
TWITCH_CHANNEL_1_DISPLAYNAME = Another Channel
```

Update the stack in Portainer and redeploy. The startup log should now read `TwitchService initialized with 2 channel(s): Some Channel, Another Channel`.

---

## Local development

Copy `appsettings.example.json` to `appsettings.Development.json` (gitignored) and fill in real values:

```json
{
  "Discord": { "BotToken": "...", "GuildId": "..." },
  "Twitch": {
    "ClientId": "...",
    "ClientSecret": "...",
    "Channels": [
      { "Username": "somechannel", "DisplayName": "Some Channel" }
    ]
  }
}
```

Then run:

```bash
DOTNET_ENVIRONMENT=Development dotnet run
```

The host builder loads `appsettings.Development.json` automatically on top of the base defaults.

---

## Project structure

```
├── Program.cs                   # Host setup, configuration layering, Serilog
├── Services/
│   ├── TwitchService.cs         # Twitch API client, schedule fetching
│   ├── DiscordService.cs        # Discord REST client, event CRUD
│   └── SyncService.cs           # Background worker, sync loop
├── Models/
│   ├── TwitchChannelConfig.cs   # Channel config shape (Username + DisplayName)
│   └── StreamScheduleEntry.cs  # Internal schedule entry model
├── appsettings.json             # Safe defaults (no secrets, empty channels)
├── appsettings.example.json     # Full schema with placeholder values
├── Dockerfile                   # Multi-stage build (sdk:8.0 → runtime:8.0)
├── docker-compose.yml           # Portainer-ready stack definition
└── .github/workflows/
    └── docker-publish.yml       # Build + push to GHCR on master push
```

---

## Built with

| Component | Library |
|---|---|
| Runtime | .NET 8 Worker Service |
| Discord API | [Discord.Net](https://github.com/discord-net/Discord.Net) |
| Twitch API | [TwitchLib](https://github.com/TwitchLib/TwitchLib.Api) |
| Logging | [Serilog](https://serilog.net/) |
| Hosting | Docker via [Portainer](https://www.portainer.io/) |
