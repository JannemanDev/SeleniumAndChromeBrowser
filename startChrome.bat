@echo off

set "userDataDir=%1"
if not defined userDataDir (
	echo No userDataDir as commandline argument was given^!
	echo Starting Chrome...
)
if defined userDataDir (
	echo Starting Chrome using profile located at %userDataDir%
)
start "" "C:\Program Files\Google\Chrome\Application\chrome.exe" --user-data-dir=%userDataDir%
