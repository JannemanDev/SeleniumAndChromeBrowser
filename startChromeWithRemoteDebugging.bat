@echo off
setlocal enabledelayedexpansion

rem Get the port number from the command-line argument (if provided)
set "port=%1"
if not defined port (
	set "port=1559"
	echo No port as commandline argument was given^^! Using default port !port!
)

rem Check if the environment variable is defined
if defined TEMP (
    rem Check if the directory exists
    if not exist "%TEMP%" (
        echo TEMP Directory does not exist: %TEMP%
		exit /b 1
    )
) else (
    echo TEMP Environment variable is not defined: %TEMP%
	exit /b 1
)

if not "!TEMP:~-1!"=="\" (
	set "userDataDir=%TEMP%\chrome-debug-profile-%port%"
) else (
	set "userDataDir=%TEMP%chrome-debug-profile-%port%"
)

echo Chrome will be started on port %port% using profile located at %userDataDir%

@REM Always use an user data dir (profile) so it works when multiple Chrome instances are used (with or without specific debugging port)
start "" "C:\Program Files\Google\Chrome\Application\chrome.exe" --remote-debugging-port=%port% --user-data-dir="%userDataDir%"
exit /b 0