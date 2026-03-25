#!/usr/bin/env bash
set -euo pipefail

APP_DIR="$(cd "$(dirname "$0")/MandelbrotGpu" && pwd)"
PID_FILE="/tmp/mandelbrot.pid"
LOG_FILE="/tmp/mandelbrot.log"
PORT=5226

start() {
    if [ -f "$PID_FILE" ] && kill -0 "$(cat "$PID_FILE")" 2>/dev/null; then
        echo "Already running (PID $(cat "$PID_FILE"))"
        return 1
    fi
    echo "Starting MandelbrotGpu..."
    cd "$APP_DIR"
    nohup dotnet run > "$LOG_FILE" 2>&1 &
    echo $! > "$PID_FILE"
    # Wait for the server to be ready
    for i in $(seq 1 30); do
        if curl -s -o /dev/null -w '' "http://localhost:$PORT/" 2>/dev/null; then
            echo "Started (PID $(cat "$PID_FILE")) on http://0.0.0.0:$PORT"
            return 0
        fi
        sleep 1
    done
    echo "Failed to start — check $LOG_FILE"
    return 1
}

stop() {
    if [ -f "$PID_FILE" ]; then
        local pid
        pid=$(cat "$PID_FILE")
        if kill -0 "$pid" 2>/dev/null; then
            echo "Stopping (PID $pid)..."
            kill "$pid"
            wait "$pid" 2>/dev/null || true
        fi
        rm -f "$PID_FILE"
    fi
    # Also kill anything on the port as a fallback
    fuser -k "$PORT/tcp" 2>/dev/null || true
    echo "Stopped"
}

restart() {
    stop
    sleep 1
    start
}

case "${1:-}" in
    start)   start ;;
    stop)    stop ;;
    restart) restart ;;
    *)       echo "Usage: $0 {start|stop|restart}" ; exit 1 ;;
esac
