@echo off
setlocal enabledelayedexpansion

if "%~1" == "" (
    echo Description: Kills the task which is LISTENING to the given port using TCP protocol
    echo Usage: %~nx0 PORT_NUMBER
    exit /b 1
)

set "port=%~1"
set "found=0"
set "output="

for /f "delims=" %%a in ('netstat -aon -p TCP ^| findstr "LISTENING" ^| findstr ":%port%"') do (
    set "output=!output!%%a"
)

echo Searching for processes listening on port %port%...
if "%output%" neq "" (
    echo %output%
    for /f "tokens=5" %%a in ('echo %output%') do (
        set "pid=%%a"
        set "found=1"
        goto :Terminate
    )
)

:Terminate
if %found% equ 1 (
    echo Terminating process with PID %pid% that is LISTENING on port %port% using TCP protocol...
    taskkill /F /PID %pid%
) else (
    echo No process found LISTENING on port %port% using TCP protocol.
)

endlocal
