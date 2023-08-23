#!/bin/bash

if [ -z "$1" ]; then
    echo "Description: Kills the task which is LISTENING to the given port using TCP protocol"
    echo "Usage: $0 PORT_NUMBER"
    exit 1
fi

port="$1"
found=0
output=""

output=$(netstat -tulnp | grep "LISTEN" | grep ":$port")

echo "Searching for processes listening on port $port..."
if [ -n "$output" ]; then
    echo "$output"
    pid=$(echo "$output" | awk '{print $7}' | awk -F '/' '{print $1}')
    found=1
fi

if [ "$found" -eq 1 ]; then
    echo "Terminating process with PID $pid that is LISTENING on port $port using TCP protocol..."
    kill -9 "$pid"
else
    echo "No process found LISTENING on port $port using TCP protocol."
fi
