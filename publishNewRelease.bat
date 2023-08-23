@echo off

set TAG_NAME=v1.1
set RELEASE_TITLE=Release %TAG_NAME%
set RELEASE_NOTES=
set dotnetcore=net6.0

:: Create the release
gh release create %TAG_NAME% --title "%RELEASE_TITLE%" --notes "%RELEASE_NOTES%"

call uploadFilesForRelease.bat %TAG_NAME% .\Builds\%dotnetcore%\*.zip
