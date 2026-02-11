# KoThumb Mini デュアルリリースビルドスクリプト
# 2つのビルドを作成: フレームワーク依存版（軽量）と自己完結型版（単一EXE）+ Vector用パッケージ

param(
    [string]$Version = "1.0.0"
)

Write-Host "--- KoThumb Mini v$Version Dual Release Build ---" -ForegroundColor Cyan
Write-Host ""

# 変数定義
$ProjectFile = "KoThumbMini.csproj"
$DistDir = "dist"
$TempFrameworkDir = "$DistDir\temp_framework"
$TempStandaloneDir = "$DistDir\temp_standalone"
$TempVectorDir = "$DistDir\temp_vector"
$FrameworkZipFile = "$DistDir\KoThumbMini-v$Version-framework-dependent.zip"
$StandaloneZipFile = "$DistDir\KoThumbMini-v$Version-standalone.zip"
$VectorZipFile = "$DistDir\KoThumbMini-v$Version-vector.zip"

# ビルド開始時刻を記録
$BuildStartTime = Get-Date

# Create dist directory if it doesn't exist
if (!(Test-Path $DistDir)) {
    New-Item -ItemType Directory -Path $DistDir | Out-Null
}

# Cleanup
foreach ($path in @($TempFrameworkDir, $TempStandaloneDir, $TempVectorDir, $FrameworkZipFile, $StandaloneZipFile, $VectorZipFile)) {
    if (Test-Path $path) {
        try { Remove-Item -Path $path -Recurse -Force -ErrorAction Stop } catch {
            Write-Host "Warning: Could not remove $path. It might be locked." -ForegroundColor Magenta
        }
    }
}

# ========================================
# 1. フレームワーク依存ビルド（軽量版）
# ========================================
Write-Host "Building Framework-Dependent (Lightweight)..." -ForegroundColor Yellow
$frameworkBuildSuccess = $false
try {
    dotnet publish $ProjectFile -c Release --self-contained false /p:PublishSingleFile=false /p:DebugType=none /p:DebugSymbols=false --output $TempFrameworkDir
    if ($LASTEXITCODE -eq 0) {
        if (Test-Path "README.md") { Copy-Item "README.md" -Destination $TempFrameworkDir }
        # Add a small delay to ensure file handles are released
        Start-Sleep -Seconds 1
        Compress-Archive -Path "$TempFrameworkDir\*" -DestinationPath $FrameworkZipFile
        Write-Host "  [OK] Framework-dependent build completed" -ForegroundColor Green
        $frameworkBuildSuccess = $true
    }
}
catch { Write-Host "  [ERROR] Framework-dependent build failed: $($_.Exception.Message)" -ForegroundColor Red }

# ========================================
# 2. 自己完結型ビルド（単一EXE版）
# ========================================
Write-Host ""
Write-Host "Building Self-Contained (Single EXE)..." -ForegroundColor Yellow
$standaloneBuildSuccess = $false
try {
    dotnet publish $ProjectFile -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true /p:DebugType=none /p:DebugSymbols=false --output $TempStandaloneDir
    if ($LASTEXITCODE -eq 0) {
        if (Test-Path "README.md") { Copy-Item "README.md" -Destination $TempStandaloneDir }
        # Add a small delay to ensure file handles are released
        Start-Sleep -Seconds 1
        Compress-Archive -Path "$TempStandaloneDir\*" -DestinationPath $StandaloneZipFile
        Write-Host "  [OK] Self-contained build completed" -ForegroundColor Green
        $standaloneBuildSuccess = $true
    }
}
catch { Write-Host "  [ERROR] Self-contained build failed: $($_.Exception.Message)" -ForegroundColor Red }

# ========================================
# 3. Vector用パッケージ（自己完結型 + Vector用README）
# ========================================
Write-Host ""
Write-Host "Building Vector Package..." -ForegroundColor Yellow
try {
    if ($standaloneBuildSuccess) {
        New-Item -ItemType Directory -Path $TempVectorDir | Out-Null
        Copy-Item -Path "$TempStandaloneDir\*" -Destination $TempVectorDir -Recurse -Force
        
        # README_VECTOR.md を README.md として配置
        if (Test-Path (Join-Path $TempVectorDir "README.md")) { Remove-Item (Join-Path $TempVectorDir "README.md") -Force }
        if (Test-Path "README_VECTOR.md") { Copy-Item "README_VECTOR.md" (Join-Path $TempVectorDir "README.md") -Force }
        
        Compress-Archive -Path "$TempVectorDir\*" -DestinationPath $VectorZipFile
        Write-Host "  [OK] Vector package completed" -ForegroundColor Green
    }
}
catch { Write-Host "  [ERROR] Vector package failed: $($_.Exception.Message)" -ForegroundColor Red }

# Cleanup
foreach ($path in @($TempFrameworkDir, $TempStandaloneDir, $TempVectorDir)) {
    if (Test-Path $path) { Remove-Item -Path $path -Recurse -Force }
}

Write-Host ""
Write-Host "--- Build Finished! ---" -ForegroundColor Green
Write-Host "Packages are located in: $DistDir\" -ForegroundColor White
