
# TuToDataTunnel 
[![.NET](https://github.com/viordash/TuToDataTunnel/actions/workflows/dotnet.yml/badge.svg?branch=main)](https://github.com/viordash/TuToDataTunnel/actions/workflows/dotnet.yml)
## Reverse websocket/http data tunnel on .NET10 + SignalR.

![Demo preview](./article/tutoproxy.png)



### Applications

#### TutoProxy.Server

[TutoProxy.Server](https://github.com/viordash/TuToDataTunnel/tree/main/Projects/TutoProxy/TutoProxy.Server) - inbound server that accepts connections from tunneling clients (TutoProxy.Client) via SignalR and listens for incoming TCP/UDP traffic from external clients.

**Command line arguments:**

| Argument | Required | Description |
|----------|----------|-------------|
| `<host>` | Yes | Server binding address with port. Example: `http://0.0.0.0:8088` or `http://200.100.10.1:8088` |
| `--tcp <ports>` | No* | TCP ports to listen on. Supports individual ports and ranges. Example: `--tcp=80,443,8000-8100` |
| `--udp <ports>` | No* | UDP ports to listen on. Supports individual ports and ranges. Example: `--udp=5000-5010,65500` |
| `--clients <list>` | No | Comma-separated list of allowed client IDs. If omitted, all clients are allowed |
| `--daemon` | No | Run in daemon mode without terminal GUI, reduces CPU overhead |

*At least one of `--tcp` or `--udp` must be specified.

**Example - start server with 50 TCP/UDP ports for 3 specific clients:**

```bash
TutoProxy.Server http://200.100.10.1:8088 \
    --tcp=3389,8071-8073,10000-10010,20000-20010 \
    --udp=5000-5010,7000-7010 \
    --clients=Client0Linux,ClientSecLinux,Client3Win
```

---

#### TutoProxy.Client

[TutoProxy.Client](https://github.com/viordash/TuToDataTunnel/tree/main/Projects/TutoProxy/TutoProxy.Client) - tunneling client that connects to TutoProxy.Server via SignalR and forwards traffic to the target host.

**Command line arguments:**

| Argument | Required | Description |
|----------|----------|-------------|
| `<server>` | Yes | TutoProxy.Server address. Example: `http://200.100.10.1:8088` |
| `<sendto>` | Yes | Target host IP where traffic will be forwarded. Example: `127.0.0.1` or `192.168.1.100` |
| `--id <id>` | Yes | Unique client identifier. Must match allowed clients on server if restriction is enabled |
| `--tcp <ports>` | No* | TCP ports to handle. Must be subset of server's TCP ports. Example: `--tcp=80,443` |
| `--udp <ports>` | No* | UDP ports to handle. Must be subset of server's UDP ports. Example: `--udp=5000-5005` |
| `--protocol <mode>` | No | Transport protocol: `Auto` (default), `Http`, `WebSocket`. WebSocket mode skips negotiation for faster connection |
| `--parallel <count>` | No | Number of parallel SignalR connections (1-8, default: 1). Multiple connections increase throughput by distributing TCP sessions across channels |
| `--daemon` | No | Run in daemon mode without terminal GUI, reduces CPU overhead |

*At least one of `--tcp` or `--udp` must be specified.

**Example - start client forwarding 5 TCP and 3 UDP ports:**

```bash
TutoProxy.Client http://200.100.10.1:8088 127.0.0.1 \
    --tcp=8071,10000,20004-20006 \
    --udp=7000-7002 \
    --id=Client0Linux
```

**Example - start client with 4 parallel SignalR connections for higher throughput:**

```bash
TutoProxy.Client http://200.100.10.1:8088 127.0.0.1 \
    --tcp=8071-8080 \
    --id=Client0Linux \
    --protocol=WebSocket \
    --parallel=4
```

**Important:** Ports of different TutoProxy.Client instances must not overlap. Each client serves a unique set of ports.

---

### Traffic Flow Architecture

#### Parallel SignalR Connections

TutoProxy.Client supports multiple parallel SignalR connections to increase throughput. When `--parallel=N` is specified:
- Client creates N `SignalRConnection` instances to the server
- Server groups these connections into a `ClientGroup`
- New TCP sessions are distributed across connections using round-robin
- Each TCP session maintains affinity to its assigned connection (session stickiness)
- UDP traffic is distributed across connections using round-robin

#### Request Flow (External Client -> Target Host)

**TutoProxy.Server side:**
1. External Client connects to `TcpServer.Listen()` / `UdpServer`
2. `TcpServer` calls `DataTransferService.ConnectTcp()` to establish session
3. `DataTransferService` selects connection via `HubClientsService.GetConnectionIdForTcp()` (round-robin)
4. `DataTransferService` registers session affinity via `HubClientsService.RegisterTcpSession()`
5. `DataTransferService` invokes `ConnectTcp` on selected `SignalRConnection`
6. On data receive, `TcpClient.ReceivingStream()` calls `DataTransferService.SendTcpRequest()`
7. `DataTransferService` uses `HubClientsService.GetConnectionIdForTcpSession()` for session affinity
8. `DataTransferService` invokes `TcpRequest` on the same connection

**TutoProxy.Client side:**
1. `ClientReceiver.ConnectTcp()` receives connection request
2. `ClientsService.AddTcpClient()` creates `TcpClient` with specific `ITcpDataChannel`
3. `TcpClient.Connect()` establishes socket to Target Host
4. `ClientReceiver.TcpRequest()` receives data, passes to `TcpClient.SendRequest()`
5. `TcpClient` sends data to Target Host via socket

#### Response Flow (Target Host -> External Client)

**TutoProxy.Client side:**
1. Target Host sends response data
2. `TcpClient.ReceivingStream()` receives data from socket
3. `TcpClient` calls `ITcpDataChannel.SendTcpResponse()` (bound to specific `SignalRConnection`)
4. `SignalRConnection.SendTcpResponse()` invokes `hubProxy.TcpResponse()`

**TutoProxy.Server side:**
1. `SignalRHub.TcpResponse()` receives response with `Context.ConnectionId`
2. `SignalRHub` calls `DataTransferService.HandleTcpResponse()`
3. `DataTransferService` calls `HubClientsService.GetClient()` to find `HubClient`
4. `HubClient.SendTcpResponse()` finds `TcpServer` by port
5. `TcpServer.SendResponse()` finds `TcpClient` by origin port
6. `TcpClient.SendDataAsync()` sends response via socket to External Client

#### Component Responsibilities

**TutoProxy.Server:**
- `SignalRHub` - SignalR hub, handles `TcpResponse`, `UdpResponse`, `DisconnectTcp`, `DisconnectUdp`
- `HubClientsService` - manages `ClientGroup` instances, handles parallel connections, round-robin distribution, session affinity
- `ClientGroup` - groups SignalR connections from same client, contains `HubClient` and connection list
- `DataTransferService` - routes data between SignalRHub and HubClients, manages session registration
- `HubClient` - represents connected TutoProxy.Client, contains `TcpServer`/`UdpServer` instances
- `TcpServer` / `UdpServer` - listen on ports, manage `TcpClient`/`UdpClient` sessions
- `TcpClient` / `UdpClient` - handle individual external client socket sessions

**TutoProxy.Client:**
- `SignalRClient` - manages multiple `SignalRConnection` instances for parallel connections
- `SignalRConnection` - implements `ITcpDataChannel`, wraps `HubConnection`, handles TCP session affinity
- `ClientReceiver` - implements `IClientReceiver`, handles incoming `TcpRequest`, `UdpRequest`, `ConnectTcp`
- `TcpClient` / `UdpClient` - connect to target host, send requests, receive responses via bound `ITcpDataChannel`
- `ClientsService` - manages active `TcpClient`/`UdpClient` instances

---

### Performance Testing

The project includes a performance testing script that measures tunnel throughput using `iperf3` and Docker.

#### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        localhost                                │
│                                                                 │
│  ┌─────────────┐      ┌──────────────────┐      ┌────────────┐  │
│  │ iperf3      │      │ TutoProxy.Server │      │ TutoProxy. │  │
│  │ -c localhost│─────▶│ :5201            │─────▶│ Client     │  │
│  │ -p 5201     │ TCP  │ (SignalR :5088)  │SignalR            │  │
│  └─────────────┘      └──────────────────┘      └─────┬──────┘  │
│                                                       │         │
└───────────────────────────────────────────────────────┼─────────┘
                                                        │ TCP
                                                        ▼
┌───────────────────────────────────────────────────────────────┐
│                     Docker Network                            │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │  iperf3-server (172.17.0.X:5201)                        │  │
│  └─────────────────────────────────────────────────────────┘  │
└───────────────────────────────────────────────────────────────┘
```

#### Prerequisites

- Docker
- iperf3 (`sudo apt install iperf3`)
- .NET SDK

#### Usage

```bash
# Full TCP test (Auto + Http + WebSocket protocols)
./Projects/TutoProxy/scripts/perf-test.sh full
./Projects/TutoProxy/scripts/perf-test.sh full-reverse

# WebSocket protocol test (fastest)
./Projects/TutoProxy/scripts/perf-test.sh websocket -d 10
./Projects/TutoProxy/scripts/perf-test.sh websocket-reverse -d 10

# Parallel connections test (4 iperf streams over 4 SignalR connections)
./Projects/TutoProxy/scripts/perf-test.sh websocket -d 10 -p 4 -s 4

# Full UDP test (Auto + Http + WebSocket protocols)
./Projects/TutoProxy/scripts/perf-test.sh udp-full -d 10 -b 1000M
./Projects/TutoProxy/scripts/perf-test.sh udp-full-reverse -d 10 -b 1000M
```

#### VSCode Tasks

Performance tests can also be run from VSCode: `Ctrl+Shift+P` → "Tasks: Run Task" → select a perf test
