# ── Phase 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# .csproj kopieren und Abhängigkeiten laden
COPY ["mortys-twitch-discord-schedulebot.csproj", "."]
RUN dotnet restore "mortys-twitch-discord-schedulebot.csproj"

# Restlichen Code kopieren und kompilieren
COPY . .
RUN dotnet publish "mortys-twitch-discord-schedulebot.csproj" -c Release -o /app/publish

# ── Phase 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime

WORKDIR /app

# Zeitzonendaten installieren (wichtig für Europe/Berlin Konvertierung)
RUN apt-get update && apt-get install -y tzdata && rm -rf /var/lib/apt/lists/*

# Kompilierte Dateien aus Build-Phase kopieren
COPY --from=build /app/publish .

# Bot starten
ENTRYPOINT ["dotnet", "mortys-twitch-discord-schedulebot.dll"]