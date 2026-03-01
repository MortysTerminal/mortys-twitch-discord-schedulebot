# 📅 Mortys Twitch-Discord Schedulebot

Ein kleines Hobby-Projekt von mir – ein Bot der automatisch Twitch-Streamzeitpläne ausliest und daraus Discord Events erstellt, damit die Community immer weiß wann und was gestreamt wird.

> **Hinweis:** Dies ist ein privates Hobbyprojekt. Ich bin kein professioneller Softwareentwickler und lerne noch. Der Code ist nach bestem Wissen und Gewissen geschrieben, erhebt aber keinen Anspruch auf Vollständigkeit oder Fehlerfreiheit.

---

## Was macht der Bot?

- Liest die Stream-Zeitpläne konfigurierter Twitch-Kanäle automatisch aus
- Erstellt daraus Events im Discord Community Server
- Aktualisiert bestehende Events wenn sich Titel, Uhrzeit oder Inhalt ändern
- Entfernt Discord Events die nicht mehr im Twitch-Zeitplan stehen
- Synchronisiert sich alle X Minuten automatisch (konfigurierbar)
- Zeigt nur Events der nächsten X Tage an (konfigurierbar)

---

## Technologien

- **Sprache:** C# / .NET 8
- **Discord:** Discord.Net
- **Twitch:** TwitchLib
- **Hosting:** Docker (via Portainer)

---

## Konfiguration

Der Bot wird über Umgebungsvariablen konfiguriert. Eine Vorlage der Konfigurationsdatei liegt unter `appsettings.example.json`.

Folgende Werte müssen als Umgebungsvariablen gesetzt werden:

| Variable | Beschreibung |
|---|---|
| `Discord__BotToken` | Discord Bot Token |
| `Discord__GuildId` | ID des Discord Servers |
| `Twitch__ClientId` | Twitch API Client ID |
| `Twitch__ClientSecret` | Twitch API Client Secret |
| `DOTNET_ENVIRONMENT` | `Production` |

Twitch-Kanäle werden in der `appsettings.Production.json` konfiguriert.

---

## Lizenz

Dieses Projekt ist ein privates Hobbyprojekt und nicht für den öffentlichen Einsatz vorgesehen. Die Nutzung erfolgt auf eigene Gefahr.
