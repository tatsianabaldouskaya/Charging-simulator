# Charging Simulator

Charging Simulator is a local web application for simulating a two-connector EV charger against an **OCPP 1.6-J central system**. It opens the OCPP WebSocket as the charge point, sends the normal charger-originated messages, and gives an operator a small dashboard to drive a charging session.

It is useful for manually checking a central-system integration without a physical charger. It does not create or manage chargers in a provider's management API.

## What you need

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Confirm it with `dotnet --version`.
- An OCPP 1.6-J central system that accepts WebSocket connections. For local experimentation, this can be a test server or a sandbox; to exercise a real platform, use a non-production charger identity.
- The central system's WebSocket base URL, for example `ws://localhost:9000/ocpp` or `wss://example.test/ocpp`.
- A unique OCPP charge-point ID that the central system recognises. The simulator adds this ID to the end of the WebSocket URL.
- A company ID accepted by the central system for `BootNotification`.

The central system must negotiate the `ocpp1.6` WebSocket subprotocol. TLS certificates must be trusted by the computer running the simulator when using `wss://`.

## Run locally

From the repository root, restore dependencies and start the host:

```sh
dotnet restore ChargingSimulator.sln
dotnet run --project src/ChargingSimulator.Host
```

The console prints the local listening URL, usually an address such as `http://localhost:5000` or `http://localhost:5001`. Open that URL in a browser. You can choose a predictable port instead:

```sh
dotnet run --project src/ChargingSimulator.Host --urls http://localhost:5050
```

Then open <http://localhost:5050>. A successful local startup can also be checked at <http://localhost:5050/health>; it returns `{"status":"healthy"}`.

To stop the application, press `Ctrl+C` in the terminal. The simulator stores active sessions and history only in memory, so restarting it clears that data.

## Use the dashboard

1. Enter the **OCPP WebSocket URL**, **OCPP ID**, and **Company ID**.
   - The URL must start with `ws://` or `wss://` and is the base endpoint, not the full charge-point URL.
   - For a base URL of `wss://cs.example.test/ocpp` and OCPP ID `demo-001`, the simulator connects to `wss://cs.example.test/ocpp/demo-001`.
   - Use an OCPP ID that has already been provisioned or allowed by your central system. The dashboard does not provision it for you.
2. Select **Connect simulator**. The simulator opens the WebSocket and sends `BootNotification`, then an `Available` `StatusNotification` for each connector.
3. Choose **Left** or **Right** connector. The two connectors have independent state and can be used separately.
4. For a basic charging flow, enter an ID tag and meter value, then use the controls in this order:

   ```text
   Prepare → Authorize → Start charging → Send meter value → Stop charging
   ```

   The ID tag must be accepted by the central system. Meter values are in Wh and must not be lower than the starting meter value.
5. Use **Suspend EV**, **Suspend EVSE**, and **Resume** when you want to test the corresponding status changes during an active charge.
6. Select **Refresh session** to see the current charger and connector state. Select **Disconnect** when finished to close the OCPP connection and release the local session.

Invalid state changes are rejected. For example, starting a transaction before preparing the connector, reporting meter values without an active transaction, or stopping with a lower meter value will fail.

## Important behaviour and limits

- The simulator is a charge-point client, not an OCPP central system.
- It uses OCPP connector ID `1` for Left and `2` for Right.
- `StartTransaction` uses the transaction ID returned by the central system for subsequent meter values and `StopTransaction`.
- A local session lease lasts five minutes by default. Configuration allows a lease between 1 and 120 minutes; see [Configuration](#configuration).
- The visible status panel shows the local session state, connector state, active transaction, and meter readings. It is not a complete OCPP frame log.
- Do not use production charge-point identities or secrets for local testing. No API-key field is currently exposed by the dashboard, and the host does not call a ChargeLab or other charger-management API.

## Configuration

The host reads the `Simulator` section from `appsettings.json` and standard .NET configuration sources. Development configuration supplies example charger IDs; the manual dashboard creates a local session from the OCPP ID you enter.

| Setting | Default | Valid range / purpose |
| --- | ---: | --- |
| `Simulator:HistoryLimit` | `500` | Maximum in-memory history entries per session; 1–10,000. |
| `Simulator:LeaseMinutes` | `5` | Default local session lifetime in minutes; 1–120. |

For a one-off local run, use environment variables (double underscores map to configuration nesting):

```sh
Simulator__HistoryLimit=1000 \
Simulator__LeaseMinutes=10 \
dotnet run --project src/ChargingSimulator.Host --urls http://localhost:5050
```

Avoid committing environment-specific endpoints, keys, or production identities to `appsettings*.json`.

## Validate the checkout

Run the automated checks before making changes:

```sh
dotnet test ChargingSimulator.sln
dotnet format ChargingSimulator.sln --verify-no-changes
```

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| `ChargeLabWss must be a ws:// or wss:// URL` | Supply a complete WebSocket base URL, including `ws://` or `wss://`. |
| Connection fails or reports that `ocpp1.6` was not negotiated | Confirm the central system supports OCPP 1.6-J over WebSocket and negotiates the `ocpp1.6` subprotocol. |
| Boot or authorization is rejected | Confirm the OCPP ID, company ID, and ID tag are provisioned/accepted by the central system. |
| `invalid transition` | Follow the basic flow above and make sure meter values do not decrease. |
| Browser cannot reach the dashboard | Use the exact URL printed by the `dotnet run` console output, or explicitly pass `--urls`. |

For the planned session-control API contract and broader design notes, see [the session-control contract](specs/001-charger-session-management/contracts/session-control-api.md) and the feature [quickstart](specs/001-charger-session-management/quickstart.md).
