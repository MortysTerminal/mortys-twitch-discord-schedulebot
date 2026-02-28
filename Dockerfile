# ── Phase 1: Build ────────────────────────────────────────────────────────────
# Wir nutzen das offizielle .NET 8 SDK Image um das Projekt zu kompilieren
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Zuerst nur die .csproj kopieren und Abhängigkeiten laden
# (Docker cached diesen Layer – spart Zeit bei späteren Builds)
COPY ["mortys-twitch-discord-schedulebot/mortys-twitch-discord-schedulebot.csproj", "mortys-twitch-discord-schedulebot/"]
RUN dotnet restore "mortys-twitch-discord-schedulebot/mortys-twitch-discord-schedulebot.csproj"

# Restlichen Code kopieren und kompilieren
COPY . .
WORKDIR "/src/mortys-twitch-discord-schedulebot"
RUN dotnet publish -c Release -o /app/publish

# ── Phase 2: Runtime ──────────────────────────────────────────────────────────
# Nur das schlanke Runtime-Image verwenden – kein SDK nötig zum Ausführen
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime

WORKDIR /app

# Zeitzonendaten installieren (wichtig für Europe/Berlin Konvertierung)
RUN apt-get update && apt-get install -y tzdata && rm -rf /var/lib/apt/lists/*

# Kompilierte Dateien aus Build-Phase kopieren
COPY --from=build /app/publish .

# Bot starten
ENTRYPOINT ["dotnet", "mortys-twitch-discord-schedulebot.dll"]