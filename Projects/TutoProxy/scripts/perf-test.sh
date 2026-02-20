#!/bin/bash

# Performance test for TutoProxy using iperf3
#
# Architecture:
#   iperf3-client → TutoProxy.Server:5201 → SignalR → TutoProxy.Client → iperf3-server:5201
#
# We use Docker for iperf3-server to avoid port conflicts on localhost

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

# Configuration
TUNNEL_PORT=5201
SERVER_HTTP_PORT=5088
DOCKER_NETWORK="tutoproxy-test"
IPERF_SERVER_CONTAINER="iperf3-server"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

cleanup() {
    log_info "Cleaning up..."

    # Kill TutoProxy processes
    if [ -n "$SERVER_PID" ] && kill -0 "$SERVER_PID" 2>/dev/null; then
        kill "$SERVER_PID" 2>/dev/null || true
        wait "$SERVER_PID" 2>/dev/null || true
    fi

    if [ -n "$CLIENT_PID" ] && kill -0 "$CLIENT_PID" 2>/dev/null; then
        kill "$CLIENT_PID" 2>/dev/null || true
        wait "$CLIENT_PID" 2>/dev/null || true
    fi

    # Stop Docker container
    docker stop "$IPERF_SERVER_CONTAINER" 2>/dev/null || true
    docker rm "$IPERF_SERVER_CONTAINER" 2>/dev/null || true

    # Remove Docker network
    docker network rm "$DOCKER_NETWORK" 2>/dev/null || true

    log_info "Cleanup complete"
}

trap cleanup EXIT

check_dependencies() {
    log_info "Checking dependencies..."

    if ! command -v docker &> /dev/null; then
        log_error "Docker is not installed"
        exit 1
    fi

    if ! command -v iperf3 &> /dev/null; then
        log_error "iperf3 is not installed. Install with: sudo apt install iperf3"
        exit 1
    fi

    if ! command -v dotnet &> /dev/null; then
        log_error "dotnet is not installed"
        exit 1
    fi

    log_info "All dependencies OK"
}

build_projects() {
    log_info "Building TutoProxy projects..."

    dotnet build "$PROJECT_DIR/TutoProxy.Server/TutoProxy.Server.csproj" -c Release --nologo -v q
    dotnet build "$PROJECT_DIR/TutoProxy.Client/TutoProxy.Client.csproj" -c Release --nologo -v q

    log_info "Build complete"
}

setup_docker() {
    log_info "Setting up Docker network and iperf3 server..."

    # Create network if not exists
    docker network create "$DOCKER_NETWORK" 2>/dev/null || true

    # Start iperf3 server in Docker (no port mapping - we access via Docker IP)
    docker run -d \
        --name "$IPERF_SERVER_CONTAINER" \
        --network "$DOCKER_NETWORK" \
        networkstatic/iperf3 \
        -s

    # Get container IP - this is where TutoProxy.Client will forward traffic
    IPERF_SERVER_IP=$(docker inspect -f '{{range.NetworkSettings.Networks}}{{.IPAddress}}{{end}}' "$IPERF_SERVER_CONTAINER")

    if [ -z "$IPERF_SERVER_IP" ]; then
        log_error "Failed to get Docker container IP"
        exit 1
    fi

    log_info "iperf3 server started at $IPERF_SERVER_IP:5201 (Docker network)"

    # Wait for server to be ready
    sleep 2
}

start_tutoproxy_server() {
    local compression="${1:-None}"
    log_info "Starting TutoProxy.Server on port $SERVER_HTTP_PORT (tunneling TCP:$TUNNEL_PORT, compression: $compression)..."

    dotnet run --project "$PROJECT_DIR/TutoProxy.Server/TutoProxy.Server.csproj" \
        -c Release --no-build -- \
        "http://127.0.0.1:$SERVER_HTTP_PORT" \
        --tcp="$TUNNEL_PORT" \
        --compression "$compression" \
        --daemon &

    SERVER_PID=$!

    # Wait for server to start
    sleep 3

    if ! kill -0 "$SERVER_PID" 2>/dev/null; then
        log_error "TutoProxy.Server failed to start"
        exit 1
    fi

    log_info "TutoProxy.Server started (PID: $SERVER_PID)"
}

start_tutoproxy_client() {
    local protocol="${1:-Auto}"
    local compression="${2:-None}"
    log_info "Starting TutoProxy.Client (protocol: $protocol, compression: $compression, forwarding to Docker iperf3 at $IPERF_SERVER_IP)..."

    # Client connects to our Server and forwards to iperf3 in Docker
    # Using the Docker container's IP address
    dotnet run --project "$PROJECT_DIR/TutoProxy.Client/TutoProxy.Client.csproj" \
        -c Release --no-build -- \
        "http://127.0.0.1:$SERVER_HTTP_PORT" \
        "$IPERF_SERVER_IP" \
        --tcp="$TUNNEL_PORT" \
        --id="PerfTestClient" \
        --protocol="$protocol" \
        --compression "$compression" \
        --daemon &

    CLIENT_PID=$!

    # Wait for client to connect
    sleep 3

    if ! kill -0 "$CLIENT_PID" 2>/dev/null; then
        log_error "TutoProxy.Client failed to start"
        exit 1
    fi

    log_info "TutoProxy.Client started (PID: $CLIENT_PID)"
}

stop_tutoproxy() {
    if [ -n "$SERVER_PID" ] && kill -0 "$SERVER_PID" 2>/dev/null; then
        kill "$SERVER_PID" 2>/dev/null || true
        wait "$SERVER_PID" 2>/dev/null || true
        SERVER_PID=""
    fi

    if [ -n "$CLIENT_PID" ] && kill -0 "$CLIENT_PID" 2>/dev/null; then
        kill "$CLIENT_PID" 2>/dev/null || true
        wait "$CLIENT_PID" 2>/dev/null || true
        CLIENT_PID=""
    fi
}

run_iperf_test() {
    local duration=${1:-10}
    local parallel=${2:-1}

    log_info "Running iperf3 test (duration: ${duration}s, parallel: ${parallel})..."
    echo ""

    # Connect to TutoProxy.Server which tunnels to iperf3 server
    iperf3 -c 127.0.0.1 -p "$TUNNEL_PORT" -t "$duration" -P "$parallel"

    echo ""
}

run_protocol_test() {
    local protocol="$1"
    local duration="$2"
    local parallel="$3"
    local compression="${4:-None}"

    echo ""
    echo "========================================="
    echo "  TUNNEL TEST ($protocol protocol, compression: $compression)"
    echo "========================================="

    start_tutoproxy_server "$compression"
    start_tutoproxy_client "$protocol" "$compression"

    run_iperf_test "$duration" "$parallel"

    stop_tutoproxy
}

print_usage() {
    echo "Usage: $0 [command] [options]"
    echo ""
    echo "Commands:"
    echo "  full       Run full test (Auto + Http + WebSocket)"
    echo "  auto       Run tunnel test with Auto protocol"
    echo "  http       Run tunnel test with Http protocol (LongPolling)"
    echo "  websocket  Run tunnel test with WebSocket protocol (fastest)"
    echo "  compare    Run compression comparison (Auto: None vs Lz4_1024)"
    echo ""
    echo "Options:"
    echo "  -d, --duration    Test duration in seconds (default: 10)"
    echo "  -p, --parallel    Number of parallel streams (default: 1)"
    echo ""
    echo "Examples:"
    echo "  $0 full"
    echo "  $0 websocket -d 30 -p 4"
    echo "  $0 auto -d 5"
    echo "  $0 compare -d 10"
}

main() {
    local command="${1:-full}"
    shift || true

    local duration=10
    local parallel=1

    while [[ $# -gt 0 ]]; do
        case $1 in
            -d|--duration)
                duration="$2"
                shift 2
                ;;
            -p|--parallel)
                parallel="$2"
                shift 2
                ;;
            -h|--help)
                print_usage
                exit 0
                ;;
            *)
                log_error "Unknown option: $1"
                print_usage
                exit 1
                ;;
        esac
    done

    check_dependencies
    build_projects
    setup_docker

    case $command in
        auto)
            run_protocol_test "Auto" "$duration" "$parallel"
            ;;
        http)
            run_protocol_test "Http" "$duration" "$parallel"
            ;;
        websocket)
            run_protocol_test "WebSocket" "$duration" "$parallel"
            ;;
        full)
            run_protocol_test "Auto" "$duration" "$parallel"
            run_protocol_test "Http" "$duration" "$parallel"
            run_protocol_test "WebSocket" "$duration" "$parallel"
            ;;
        compare)
            run_protocol_test "Auto" "$duration" "$parallel" "None"
            run_protocol_test "Auto" "$duration" "$parallel" "Lz4_1024"
            ;;
        *)
            log_error "Unknown command: $command"
            print_usage
            exit 1
            ;;
    esac

    log_info "Test complete!"
}

main "$@"
