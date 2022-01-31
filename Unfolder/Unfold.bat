@echo off

set /P path="Path: "

%~dp0/bin/Release/Unfolder.exe -u -s "%path%"

pause