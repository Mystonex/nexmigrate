$proj = "C:\Users\nex\_dev\nexmigrate"
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$proj'; dotnet run"
