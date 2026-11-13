# Test-Wasm


### PWA Deployment
To run the WASM app as PWA (Progressive Web App) add the `manifest.webmanifest` file at [wwwroot/index.html](wwwroot/index.html)

```html
<link rel="manifest" href="manifest.webmanifest"> <!-- uncomment to run as PWA -->
```

### WASM-AOT Build

```shell
cd src/Test-Wasm/
dotnet publish -c Release
```

Host static WASM-AOT build locally 
```shell
dotnet serve -p 5010 -S -d bin/Release/net10.0-browser/publish/wwwroot/
```

Open WASM-AOT app in browser:  
https://localhost:5010/