@echo off
echo Building a single LanArena.exe (works without installing .NET) ...
dotnet publish LanArena.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
echo.
echo Done! Your exe is in the "publish" folder.
pause
