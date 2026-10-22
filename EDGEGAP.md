# Hosting the Incremental sample on Edgegap

This fork of [PurrNet-Incremental-Sample](https://github.com/PurrNet/PurrNet-Incremental-Sample) runs the game on [Edgegap](https://edgegap.com) dedicated servers. Every player is a client of a headless server that Edgegap runs close to them. No one hosts.

**Start** has two modes:

- **Direct connection (default).** Clients connect to a server address and port. Use it to test locally first, then against a deployment you start from the Edgegap dashboard. No matchmaker needed.
- **Edgegap Matchmaker (optional, off by default).** Players click **Find Match**. The [Edgegap Matchmaker](https://docs.edgegap.com/learn/matchmaking) groups them, Edgegap starts a server near them, and every client connects to it automatically.

In both modes the server stops its own Edgegap deployment once the match is empty, so idle servers don't cost money. The game client never holds an Edgegap API token.

**Tested versions:** Unity `6000.3.11f1`, PurrNet `v1.22.0-beta.11` (commit `a311c06`), Edgegap Unity SDK `3.5.5`, with the PurrNet `UDPTransport`; e2e verified on Edgegap on 2026-10-15. `Packages/manifest.json` pins both packages to these tags. In both modes, two players connected and played on the same Edgegap server. In matchmaker mode, both were matched onto the same server about 35 s after clicking Find Match. Each time, the server stopped its own deployment after the players left.

## What this fork adds

The upstream game is unchanged, apart from one fix (see [Upstream changes](#upstream-changes)). Everything Edgegap-specific lives in `Assets/Edgegap/` and adds itself at runtime, so `GameScene` is not modified and syncing new upstream releases stays easy.

| File | Role |
|---|---|
| `Assets/Edgegap/Resources/EdgegapSettings.asset` | All settings: mode, ports, matchmaker URL and token. |
| `Assets/Edgegap/EdgegapBootstrap.cs` | Runs before the scene loads. In builds, it turns off PurrNet's auto start and adds the components below. In the Editor, play mode keeps PurrNet's normal host + clone flow unless **Apply In Editor** is ticked. |
| `Assets/Edgegap/EdgegapServerLifecycle.cs` | Server builds. Starts the PurrNet server on `0.0.0.0:5000`. On Edgegap, stops the deployment when nobody joins within 120 s or 30 s after the last player leaves. Logs `N player(s) connected.` |
| `Assets/Edgegap/EdgegapConnectPanel.cs` | Client builds. The connect panel: address and port (direct), or Find Match (matchmaker). Reloads the scene when the connection drops. |
| `Assets/Edgegap/EdgegapMatchmakingClient.cs`, `EdgegapMatchmakingDTOs.cs` | Matchmaker mode only. Creates a ticket, waits for `HOST_ASSIGNED`, then connects to the server's public IP and external port. |
| `Assets/Edgegap/Editor/EdgegapServerBuild.cs` | **Tools → Edgegap → Build Linux Dedicated Server**, plus command-line server and client builds. |
| `Dockerfile`, `.dockerignore` | Container image for the server build. |
| `matchmaker-config.json` | Matchmaker configuration to upload in the dashboard. |
| `Packages/manifest.json` | Adds `com.edgegap.unity-sdk` pinned to `3.5.5`, and pins PurrNet to `v1.22.0-beta.11` instead of its `dev` branch. See [Package versions](#package-versions). |

## Requirements

- Unity 6 (`6000.3.x`) with the **Linux Build Support (Mono)** and **Linux Dedicated Server Build Support** modules, plus the module for your client platform.
- [Docker](https://docs.docker.com/get-docker/).
- An [Edgegap account](https://app.edgegap.com) (the free tier is enough).

The sample uses UDP, so clients are desktop builds (Windows, macOS, Linux). WebGL would need PurrNet's `WebTransport` and a WebSocket port with TLS Upgrade on the app version.

## 1. Play locally (direct connection)

1. Open the project in Unity 6.
2. Build the server: **Tools → Edgegap → Build Linux Dedicated Server**. The output goes to `Builds/EdgegapServer/`.
3. Run it in Docker:
   ```bash
   docker build --platform linux/amd64 -t purrnet-incremental:local .
   docker run --rm -p 5000:5000/udp purrnet-incremental:local
   ```
   The log shows `Edgegap Server | Starting server on 0.0.0.0:5000.`
4. Build the client (**File → Build Profiles**, any desktop platform) and start it twice. Each window shows the connect panel with `127.0.0.1` / `5000`. Click **Connect** in both. The server logs `Edgegap Server | 2 player(s) connected.`

Play mode in the Editor still works as upstream: it starts a host, and a ParrelSync clone joins it.

## 2. Play on an Edgegap server (direct connection)

1. Push the image to your Edgegap registry. Your registry project, username and token are on the dashboard's **Container Registry** page.
   ```bash
   docker login registry.edgegap.com
   docker build --platform linux/amd64 -t registry.edgegap.com/<project>/purrnet-incremental:v1 .
   docker push registry.edgegap.com/<project>/purrnet-incremental:v1
   ```
   Use a new tag for every build, never `latest`.
2. In the [dashboard](https://app.edgegap.com), create an application, then a version for that image:
   - **Port:** name `gameport`, port `5000`, protocol **UDP**.
   - **Registry username and token:** from the Container Registry page. Without them, deployments fail to pull the image. Push the image before saving the version; the dashboard checks that the tag exists.
3. Deploy the version (**Deploy** on the version page). When it's ready, open the deployment and note its host (`<id>.pr.edgegap.net`) and the external port of `gameport`.
4. Start two clients, enter that host and port in the connect panel, and click **Connect**.

Once both players leave, the server stops its own deployment after 30 s.

## 3. Turn on the Edgegap Matchmaker (optional)

The matchmaker is off by default. Turn it on once direct connection works:

1. In `matchmaker-config.json`, set `application.name` and `version` to your application and version.
2. Dashboard → **Matchmaker** → **Create Matchmaker** → upload the file → **Create and Start**.
3. Select `Assets/Edgegap/Resources/EdgegapSettings.asset`. Tick **Use Edgegap Matchmaker**, then paste the matchmaker's **API URL** into **Matchmaker Url** and its **Auth Token** into **Matchmaker Auth Token**. The Auth Token is meant for game clients: it only allows matchmaking calls and gives no access to your Edgegap account.
4. Rebuild the client, start it twice and click **Find Match** in both. The status goes through searching → match found → joining → connected, and both players land on the same server.

The `casual` profile matches 2 to 4 players.

**Playing alone:** the profile lowers `min_team_size` to 1 after 30 s, but don't count on that. With the same profile shape (2 to 4 players, `"30"` expansion to 1, 5-minute `ticket_expiration_period`) on matchmaker 3.3.2, a lone ticket in Edgegap's FishNet sample was only given a server around its 5-minute expiry, not at 30 s. This sample's lone-player case was not tested. If no server comes before the ticket expires, the panel shows "No match found in time, try again." To play alone, use direct connection, or test the matchmaker with two clients.

**Known limitation:** the server does not check that a connecting player holds a matchmaker ticket for this match, so anyone who learns the server address can join. Deployments stop themselves when no one joins or everyone leaves, which limits cost. Before production, validate each player against the match's tickets (`MM_TICKET_IDS` / the matchmaker API) or an auth token.

## Command-line options

Command-line options override `EdgegapSettings` in builds. They're useful for scripted tests and for Edgegap's `UNITY_COMMANDLINE_ARGS` variable.

| Option | Effect |
|---|---|
| `-edgegapAddress <host> -edgegapPort <port>` | Direct connection address and port |
| `-edgegapConnect` | Connect directly on startup |
| `-edgegapMatchmaker true\|false` | Turn matchmaker mode on or off |
| `-edgegapMatchmakerUrl <url> -edgegapAuthToken <token>` | Matchmaker URL and Auth Token |
| `-edgegapFindMatch` | Find a match on startup |
| `-edgegapListenPort <port>` | Server listen port (must match the app version's internal port) |

For example, two clients that join a deployment straight away:

```bash
Game.exe -screen-fullscreen 0 -edgegapAddress 1a2b3c4d5e6f.pr.edgegap.net -edgegapPort 31234 -edgegapConnect
```

## Shipping a new server build

Build the server → push a new tag → create a new app version (or edit the tag on a test version) → if you use the matchmaker: **Stop → Edit** `application.version` **→ Start**.

## Upstream changes

The fork changes one upstream line. `ResourceSpawner.InitSeed` is now `[ObserversRpc(runLocally: true, bufferLast: true)]`. Without `runLocally`, a dedicated server never receives its own seed, so it never spawns trees. Hosts weren't affected, because their local client receives the RPC. PurrNet skips the local client when `runLocally` is set, so host mode still runs it once.

## Syncing with upstream

```bash
git remote add upstream https://github.com/PurrNet/PurrNet-Incremental-Sample.git
git fetch upstream
git merge upstream/main
```

Edgegap files live in their own folder, so conflicts are limited to `Packages/manifest.json`, `Packages/packages-lock.json` and the one line above. After merging, rebuild the server and run the two-client test again.

## Package versions

`Packages/manifest.json` pins the two git packages to the versions the sample was tested with, so every clone builds the same code:

- [edgegap-unity-sdk](https://github.com/edgegap/edgegap-unity-sdk) `#3.5.5` (release tag).
- [PurrNet](https://github.com/PurrNet/PurrNet) `#v1.22.0-beta.11`, the tag on commit `a311c06`. The sample previously followed PurrNet's `dev` branch.

To move to a newer release, change the tag after `#` in `manifest.json` (for example `#3.5.6` for the SDK), let Unity resolve it, then rebuild the server and run the two-client test again.

## Notes

- The game uses PurrNet's permissive network rules (owner-authoritative movement and harvesting), as upstream does. That keeps the sample responsive but trusts clients. A competitive game should use stricter rules.
- Free tier: one deployment at a time, and the matchmaker restarts every 3 hours (its URL and token stay the same).
- Next steps for a real game: check the client's ticket ID on the server (`MM_TICKET_IDS`, see the known limitation above), handle players joining a match in progress (backfill), and move to a production matchmaker tier before launch.
