# Test-Wasm


### WASM-AOT Build

```shell
cd src/Test-Wasm/
dotnet publish -c Release
```

Host static WASM-AOT build locally 
```shell
dotnet serve -p 5010 -S -d bin/Release/net10.0-browser/publish/wwwroot/
```

Open WASM app in browser:  
https://localhost:5010/