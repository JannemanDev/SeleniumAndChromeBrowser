@echo off
setlocal enabledelayedexpansion

rem Get the port number from the command-line argument (if provided)
set "port=%1"
if not defined port (
	set "port=1559"
	echo No port as commandline argument was given^^! Searching on default port !port!
)

set "chromeRunning=false"
tasklist | find /i "chrome.exe" > nul

if %errorlevel% equ 0 (
    set "chromeRunning=true"
)

rem Run the netstat command and filter with findstr
for /f "tokens=*" %%a in ('netstat -ano ^| grep %port% ^| grep LISTENING') do (
    set "output=%%a"
)
rem Check if output is set
if defined output (
    echo Chrome instance^(s^) LISTENING on port %port% found:
    echo %output%
) else (
    if "%chromeRunning%"=="true" (
		echo Chrome is running but no Chrome instance^(s^) found LISTENING on port %port%!
	) else (	
		echo Chrome is not running at all, no Chrome instance^(s^) were found LISTENING on port %port%!
	)
)

endlocal
