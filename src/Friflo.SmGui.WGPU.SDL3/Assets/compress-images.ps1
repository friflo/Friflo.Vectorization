$targetDir = $PSScriptRoot
if (-not $targetDir) { $targetDir = $PWD.Path }

$files = @(
    "wgpu-transparent-dark-128x128.bmp",
    "wgpu-transparent-light-128x128.bmp"
)

foreach ($fileName in $files) {
    $filePath = Join-Path $targetDir $fileName
    $outputPath = "$filePath.gz"
    
    if (Test-Path $filePath) {
        $in  = [System.IO.File]::OpenRead($filePath)
        $out = [System.IO.File]::Create($outputPath)
        $gz  = New-Object System.IO.Compression.GZipStream($out, [System.IO.Compression.CompressionLevel]::Optimal)
        
        try {
            $in.CopyTo($gz)
            Write-Host "Komprimiert: $fileName -> $fileName.gz" -ForegroundColor Green
        }
        finally {
            $gz.Dispose()
            $out.Dispose()
            $in.Dispose()
        }
    } else {
        Write-Warning "Nicht gefunden: $filePath"
    }
}