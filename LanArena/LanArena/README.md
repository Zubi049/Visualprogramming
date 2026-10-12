# LAN Arena  (C# / .NET 8 WinForms)

Desktop app jo Wi-Fi (WLAN) list dikhati hai aur same network par 2 players ko
**Chess** aur **Tic Tac Toe** multiplayer khelne deti hai. Har player sirf apni turn par move kar sakta hai.

## Chalane ka tareeqa
1. Install **.NET 8 SDK** (https://dotnet.microsoft.com/download) ya **Visual Studio 2022** (".NET desktop development").
2. Zip extract karein -> `Run.bat` par double click  (ya Visual Studio mein `LanArena.csproj` open karke F5).
3. Dusre PC ke liye ek single exe chahiye ho to `Publish.bat` chalayein -> `publish\LanArena.exe` copy kar dein.

## Kaise khelein
* **Player 1:** game choose karein (Tic Tac Toe / Chess) -> **Host a game**.
* **Player 2:** same WLAN par app kholein -> room list mein host ka naam aa jayega -> **Join selected room**
  (ya host ka IP likh kar **Join by IP**).
* Pehla move random player ko milta hai. Game ke baad **Rematch** dabayein (colours/marks swap ho jate hain).
* Right side par chat bhi hai.

## Features
* Available WLANs list (SSID, security, signal strength, connected network) - `netsh wlan` se.
* "Connect to selected" - saved Wi-Fi profile par connect karta hai.
* Auto room discovery (UDP broadcast, port 47777) + game connection (TCP, port 47778).
* Chess: full rules - legal moves, check, checkmate, stalemate, castling, en passant, promotion dialog.
* Tic Tac Toe: hover effect + winning line highlight.
* Server-side validation: opponent ka har chess move dobara check hota hai.

## Agar kuch kaam na kare
* **Windows Firewall** pehli baar allow poochhega -> *Private networks* allow karein (dono PCs par).
* Public Wi-Fi / hotspot par "client isolation" ho to devices ek dusre ko nahi dekhte - router setting check karein.
* WLAN list khali ho to Windows 11 mein **Settings > Privacy > Location** on karein (netsh ko location permission chahiye).
* Host ka IP direct likh kar bhi join kar sakte hain.

## Files
* `MainForm.cs`   - lobby (WLAN list, host, join)
* `GameForm.cs`   - game window + chat + rematch
* `ChessGame.cs`  - chess rules engine
* `ChessBoardControl.cs`, `TicTacToeControl.cs` - custom drawn boards
* `NetPeer.cs`, `Discovery.cs` - TCP / UDP networking
* `WlanScanner.cs` - Wi-Fi scanning
