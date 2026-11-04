<p align="center">
  <a href="README.md"><img alt="한국어" src="https://img.shields.io/badge/%ED%95%9C%EA%B5%AD%EC%96%B4-%ED%95%9C%EA%B5%AD%EC%96%B4%EB%A1%9C%20%EB%B3%B4%EA%B8%B0-6e7681?style=for-the-badge"></a>
  <a href="README.en.md"><img alt="English (this page)" src="https://img.shields.io/badge/English-This%20page-1f6feb?style=for-the-badge"></a>
</p>

# Hephaestus (SPT 4.1.6 port)

A server mod adding the trader "Hephaestus", who sells your saved **weapon presets (weapon builds)** exactly as saved.
Buy complete guns in euros at a steep discount (90% by default) from the combined flea market value of their parts.

---

### ⚠️ IMPORTANT NOTICE / DISCLAIMER

- **Original Author:** alexkarpen (Alexandros)
- **Original Repository:** Hephaestus
- **Original Link:** [https://github.com/alexkarpen/Hephaestus](https://github.com/alexkarpen/Hephaestus)
- **License:** MIT (listed in the original `package.json`)

1. **Reflection & Take-Downs:** I deeply reflect on the ECOT incident. As an AI-assisted "vibe coder," I will immediately delete files if the original authors ask.
2. **No Re-Distribution:** These ported builds are unverified, temporary fixes. Please do NOT re-upload or share them anywhere else.
3. **Do Not Pester Original Authors:** Never report bugs or pester original modders regarding issues from my unofficial ports.
4. **Full Credit & Respect:** I will always credit original creators on GitHub and prioritize their decisions above all else.
5. **Support Original Creators:** Instead of using my ports, please visit the original authors' Forge pages to leave kind words or tips.

---

## Changelog

The full changelog (5 entries) is in the [`changelog/`](changelog/) folder, 10 per file (higher number = newer, latest: [`CHANGELOG-001.md`](changelog/CHANGELOG-001.md)). Per-release summaries: [Releases](../../releases).

## Installation

1. Extract the release zip (`release/Hephaestus-1.2.0.zip`) into your SPT installation (`E:\SPT 4.1`) to get this layout.
   ```
   E:\SPT 4.1\SPT_Runtime\user\mods\Hephaestus\     ← 서버 모드 (필수)
       Hephaestus.dll
       config\config.json
       db\base.json
       res\img.jpg
   E:\SPT 4.1\BepInEx\plugins\HephaestusClient.dll   ← F12 설정 화면 (선택)
   ```
2. On server startup, `[Hephaestus] 상인 추가 완료 (화폐 EUR, 할인 90%)` (trader added, currency EUR, discount 90%; `trader added ...` on English Windows) indicates success.
3. Save a preset in the game's weapon-modification/build screen, then open Hephaestus to buy it.
4. If you do not need the F12 BepInEx Configuration Manager screen, omit `HephaestusClient.dll`. The server trader still works; edit `config.json` in a text editor.

> The old 3.7 mod folder (`user/mods/Hephaestus`, containing `src/mod.js`) does not work on 4.1. This repository's `user/` folder is retained **only as an upstream reference**; do not install it.

## Configuration (`config/config.json`)

| Key | Default | Meaning |
|---|---|---|
| `currency` | `569668774bdc2da2298b4568` (euros) | Price currency. Roubles: `5449016a4bdc2d6f028b456f`; dollars: `5696686a4bdc2da3298b456a` |
| `discount` | `90` | Discount 0~100 (0 = market value, 100 = effectively free, minimum price 1) |
| `cost` | `123` | Fallback price if no preset market prices can be found (in the selected currency, discount still applies) |
| `debugLogging` | `false` | When `true`, logs one server line per assortment build with presets offered/skipped |

Keys starting with `HELP_` are explanatory and ignored. Quoted values such as `"90"` also work.
F12 changes only the four values in this file, preserving `HELP_` descriptions.

## F12 BepInEx Configuration Manager screen (`HephaestusClient.dll`)

| Category | Setting | Meaning |
|---|---|---|
| 0. 언어 (Language) | 언어 (Language) | `자동 · Auto` (default) / `한국어 · Korean` / `English` |
| 1. 상인 가격 (Trader prices) | 할인율 (%) (Discount) | 0~100, `discount` above |
| 1. 상인 가격 (Trader prices) | 가격 화폐 (Price currency) | Euros / roubles / dollars, `currency` above |
| 1. 상인 가격 (Trader prices) | 대체 가격 (Fallback price) | 1~10,000,000, `cost` above |
| 1. 상인 가격 (Trader prices) | 서버 디버그 로그 (Server debug log) | `debugLogging` above |
| 2. 상태 (Status) | 서버 연결 상태 (Server connection status) | Last save result (read-only) |

- Sends changes to the server after 0.8 seconds and saves `config.json` (only once while adjusting a slider). **Reopen the trader screen** to see new prices.
- Reads current server values at game startup into F12 (server `config.json` is authoritative). Failed connections retry up to 6 times at 10-second intervals; 서버 연결 상태 (Server connection status) explains why.
- Settings are shared per server. With multiple players, such as Fika, anyone's changes affect everyone.

## Automatic language selection

| Component | How language is chosen |
|---|---|
| Trader name/description/location | Game language. The game requests that language from the server: Korean for `kr`, English otherwise |
| F12 screen | In 자동 (Auto), uses the game's actual active language (Korean → Korean, otherwise English). Before the game sets its language, uses Windows display language. Rechecks every second; **close and reopen F12** after changes |
| Server logs | Windows display language on the server PC |

Reads the game's internal localization-manager value directly, falling back to Windows language without errors if an update makes it inaccessible.

## How it works (same as / different from upstream)

- **Same**: One offer per saved weapon preset; stock 2000 (effectively unlimited); available at loyalty level 1; price = sum of parts' market values × (1 - discount); hourly restock; trader description/portrait.
- **Different**
  - Rebuilds the trader's assortment from that player's presets **when opening the trader screen and when buying** (upstream did so on login and every item move).
    If a just-saved preset is missing, leave and reopen the trader screen.
  - Prices sum baseline flea market values without random variation. Upstream used flea-offer-style ±variation, changing prices whenever the screen reopened.
  - Does not sell equipment/magazine presets, matching upstream (weapon presets only).
  - Trader ID changed (`a18160fc82b4f2be55205c89`), because 4.1 accepts only 24-character hexadecimal IDs.

## Verification status

- `dotnet build -c Release` succeeded (0 warnings, 0 errors) against SPT 4.1.6 server packages.
- Full `Hephaestus.sln` build (server/client/tests): 0 warnings, 0 errors.
- 94 self-tests passed (`tests/Hephaestus.Tests`): reading `base.json` with SPT 4.1.6 JSON rules; settings loading/saving (preserves help keys, corrects invalid values); creating offers from 40 actual weapons left by upstream
  (unchanged profile presets, no ID collisions, part links, stock, prices); profile isolation; patch binding (target functions/parameter names);
  language rules (manual/auto, game/Windows), localized trader text, and F12 request → server → response round trips (including Korean text).
- **Checked on a real SPT 4.1 server** (server in a container, scripted requests instead of the game; 32 tests passed in `tests/e2e/e2e.py`):
  mod loading/trader registration; client-facing trader info (`isAvailableInPVE`, portrait path); portrait response; localized text (`kr` 헤파이스토스 (Hephaestus), English for `en`/`ru`);
  preset save → assortment (all parts, stock 2000, integer prices stable on reopening); unchanged profile presets; isolation from other profiles;
  F12 settings routes (load, switch to roubles, save `config.json`, preserve help keys); purchases (gun reaches stash, exact displayed price deducted, all parts, no duplicate IDs, second purchase also works);
  Korean server logs when running in a Korean environment.
- **Not yet verified** (expected from code): Actual game screens — trader list/Korean name, F12 display/automatic language switching/save-button behavior (the F12 plugin cannot run without the game).
- The container's `HephaestusClient.dll` was compiled against stub DLLs instead of Unity (confirmed the output references only BepInEx/netstandard). Rebuilding on the PC as below is recommended before distribution.

## Build

Open `Hephaestus.sln` in Visual Studio / Rider, or:

```
dotnet build Hephaestus.sln -c Release
# 게임이 E:\SPT 4.1이 아니면: dotnet build Hephaestus.sln -c Release -p:SptRoot="D:\다른\경로"
# 테스트
dotnet run --project tests/Hephaestus.Tests -c Release -- Hephaestus
```

- Server mod: `Hephaestus/bin/Release/net10.0/`
- F12 plugin: `HephaestusClient/bin/Release/HephaestusClient.dll` (references the game's `BepInEx\core\BepInEx.dll` and `EscapeFromTarkov_Data\Managed\UnityEngine.dll`)

## License

The original `package.json` specifies MIT, so this port follows MIT too. Original rights belong to the original author (alexkarpen).

