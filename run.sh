#!/usr/bin/env bash
set -e
PORT=${PORT:-5000}
echo "Starting StatLab 2.0 on http://localhost:$PORT"
dotnet run --urls "http://localhost:$PORT"
