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

`docker-compose.yml` pre-defines slots 0–4. To add channels, just set more variables in Portainer — **no editing of `docker-compose.yml` required**. Use the exact .NET notation as the variable name:

```
Twitch__Channels__0__Username    = mortys_welt
Twitch__Channels__0__DisplayName = MORTYS WELT
Twitch__Channels__1__Username    = dentugaming
Twitch__Channels__1__DisplayName = DentuGaming
Twitch__Channels__2__Username    = thirdchannel
Twitch__Channels__2__DisplayName = Third Channel
```

Unset slots are automatically ignored — only the slots you fill in Portainer are passed to the container. Need more than 5 channels? Add the same pass-through pattern to `docker-compose.yml` for slots 5+.

> **Important:** Indices must be contiguous starting from 0. A gap (e.g. setting 0 and 2 but not 1) stops .NET from reading anything after the gap.

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
5. Scroll down to **Environment variables** and add your values. Secrets use short names (referenced as `${...}` in the compose file); channel variables use the exact .NET notation:

   ```
   DISCORD_BOT_TOKEN    = <your token>
   DISCORD_GUILD_ID     = <your server id>
   TWITCH_CLIENT_ID     = <your client id>
   TWITCH_CLIENT_SECRET = <your client secret>

   Twitch__Channels__0__Username    = mortys_welt
   Twitch__Channels__0__DisplayName = MORTYS WELT
   Twitch__Channels__1__Username    = dentugaming
   Twitch__Channels__1__DisplayName = DentuGaming
   ```

   Channel variables are passed directly into the container — just add or remove pairs to control how many channels the bot monitors.

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
  -e Twitch__Channels__0__Username="mortys_welt" \
  -e Twitch__Channels__0__DisplayName="MORTYS WELT" \
  ghcr.io/mortysterminal/mortys-twitch-discord-schedulebot:latest
```

---

## Testing guide (Portainer)

### Step 1 — Verify the bot starts and binds channels correctly

After deploying in Portainer, open **Containers → schedulebot → Logs** (or click the log icon on the stack view).

A successful startup looks like this:

```
HH:mm:ss [INF] Twitch-Discord Schedule Bot starting...
HH:mm:ss [INF] TwitchService initialized with 1 channel(s): MORTYS WELT
HH:mm:ss [INF] DiscordService initialized (event language: en).
HH:mm:ss [INF] SyncService started — syncing every 30 minute(s).
HH:mm:ss [INF] Application started. Press Ctrl+C to shut down.
HH:mm:ss [INF] Hosting environment: Production
HH:mm:ss [INF] Discord login successful.
HH:mm:ss [INF] ═══════════════════════════════════════
HH:mm:ss [INF] Starting sync...
```

The `TwitchService initialized with N channel(s): ...` line is your confirmation that the environment variables were read correctly.

### Step 2 — Test the fail-fast behavior (no channels configured)

Deploy the stack once **without** the `Twitch__Channels__0__Username` / `Twitch__Channels__0__DisplayName` variables. The container should exit immediately with:

```
[ERR] No Twitch channels are configured. Set at least one channel pair via environment variables:
      Twitch__Channels__0__Username and Twitch__Channels__0__DisplayName
```

This confirms the guard works and the image is not silently doing nothing. Re-add the variables and redeploy to continue.

### Step 3 — Verify Twitch API credentials

Watch the logs for the first sync cycle. You should see:

```
HH:mm:ss [INF] Twitch authentication successful.
HH:mm:ss [INF] Fetching schedule for channel: mortys_welt
HH:mm:ss [INF] Channel mortys_welt: 3 entries found.
```

If you see an authentication error here, double-check `TWITCH_CLIENT_ID` and `TWITCH_CLIENT_SECRET`.

### Step 4 — Verify Discord events are created

Go to your Discord server → **Events** (the calendar icon in the left sidebar or the server header). After the first sync you should see scheduled events for every upcoming stream on the configured channels.

If events are missing:
- Make sure the bot has the **Manage Events** permission in the server
- Check the logs for `Failed to create Discord event` lines
- Confirm the Twitch channel has actual upcoming scheduled segments (not all streamers use the schedule feature)

### Step 5 — Test an update cycle

Change the title of a scheduled segment on Twitch (via the Creator Dashboard → Schedule). Wait for the next sync (or lower `Sync__IntervalMinutes` temporarily to `1`), then check the Discord event — the title should update automatically.

### Step 6 — Test deletion

Cancel a scheduled Twitch segment. After the next sync the corresponding Discord event should disappear.

### Step 7 — Test with multiple channels

Add a second channel pair in Portainer stack environment variables:

```
Twitch__Channels__1__Username    = mortys_welt
Twitch__Channels__1__DisplayName = MORTYS WELT
```

Redeploy the stack. The startup log should now read `TwitchService initialized with 2 channel(s): MORTYS WELT, MORTYS WELT`.

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
      { "Username": "mortys_welt", "DisplayName": "MORTYS WELT" }
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
