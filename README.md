# SteamBooster

Steam account booster with automatic card-drop farming.

## What It Does

- Logs in to one or more Steam accounts from `Accounts.txt`.
- Auto-detects games with card drops left and plays them one at a time.
- Stops automatically when all card drops are claimed.
- Optional fallback: play manually configured app IDs.

## Setup

1. Build and run once:
   - `dotnet run --project Steam-HourBoost/HexedBooster.csproj`
2. The app creates `Accounts.txt` if missing.
3. Fill your credentials and settings, then run again.

## Accounts.txt Example

```json
[
  {
    "username": "your_steam_username",
    "password": "your_steam_password",
    "deviceName": "SteamBooster",
    "autoFarmCardDrops": true,
    "farmCheckIntervalSeconds": 60,
    "games": [570]
  }
]
```

## Notes

- Card-drop detection is based on your Steam badge pages (`?l=english`).
- If your profile blocks badge visibility, drop detection may return no games.
- `games` values are Steam app IDs.
