set TAG_NAME=%1
set FILES_TO_UPLOAD=%2

:: Upload files to the release
for %%f in (%FILES_TO_UPLOAD%) do (
  gh release upload %TAG_NAME% "%%f"
)