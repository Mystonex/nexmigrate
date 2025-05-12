cd C:\Users\nex\_dev\nexmigrate

dotnet publish -c Release `
  -r win-x64 `
  --self-contained false `
  -p:PublishSingleFile=true `
  -p:PublishTrimmed=true `
  -p:EnableCompressionInSingleFile=true `
  -o ./publish-slim
