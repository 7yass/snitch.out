<#Requires -Version 5.1>
<#
.SYNOPSIS
  One-shot installer for snitch.out. Checks dependencies, pulls the repo,
  compiles it, installs it, and launches it.

.DESCRIPTION
  1. Verifies Windows 10+
  2. Installs missing dependencies via winget (git, .NET 6 SDK, .NET 6 Desktop Runtime)
  3. Clones (or updates) https://github.com/7yass/snitch.out
  4. Publishes snitch.out in Release and copies it to the install directory
  5. Creates a Start Menu shortcut and launches the app (first run = its own setup)

  Just run install.bat. No admin rights required (user-level install).

.PARAMETER Repo
  Git repository URL to build from.

.PARAMETER Branch
  Branch to build.

.PARAMETER InstallDir
  Where snitch.out gets installed. Defaults to %LOCALAPPDATA%\snitch.out.

.PARAMETER SourceDir
  Where the repo is cloned. Defaults to <InstallDir>\src.

.PARAMETER NoLaunch
  Install but do not launch snitch.out afterwards.

.PARAMETER NoShortcuts
  Skip Start Menu shortcut creation.

.PARAMETER Yes
  Skip the confirmation prompt.
#>
[CmdletBinding()]
param(
  [string]$Repo = "https://github.com/7yass/snitch.out.git",
  [string]$Branch = "main",
  [string]$InstallDir = (Join-Path $env:LOCALAPPDATA "snitch.out"),
  [string]$SourceDir = "",
  [switch]$NoLaunch,
  [switch]$NoShortcuts,
  [switch]$Yes
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Ok([string]$msg) { Write-Host "    [ok] $msg" -ForegroundColor Green }
function Write-Need([string]$msg) { Write-Host "    [..] $msg" -ForegroundColor Yellow }

function Refresh-Path {
  $machine = [System.Environment]::GetEnvironmentVariable("Path", "Machine")
  $user = [System.Environment]::GetEnvironmentVariable("Path", "User")
  $env:Path = "$machine;$user"
}

function Test-Command([string]$name) {
  return [bool](Get-Command $name -ErrorAction SilentlyContinue)
}

function Install-WithWinget([string]$id, [string]$label) {
  Write-Need "Installing $label ($id), this may take a few minutes..."
  & winget install --id $id -e --silent `
    --accept-source-agreements --accept-package-agreements `
    --disable-interactivity | Out-Null
  if ($LASTEXITCODE -ne 0) {
    throw "$label failed to install via winget (exit $LASTEXITCODE). Install it manually and re-run."
  }
  Refresh-Path
  Write-Ok "$label installed"
}

if ([string]::IsNullOrEmpty($SourceDir)) {
  $SourceDir = Join-Path $InstallDir "src"
}

Write-Host "snitch.out installer" -ForegroundColor Magenta
Write-Host "  repo:   $Repo ($Branch)"
Write-Host "  source: $SourceDir"
Write-Host "  target: $InstallDir"

if (-not $Yes) {
  $answer = Read-Host "`nContinue? [Y/n]"
  if ($answer -ne "" -and $answer -notmatch '^[Yy]') { Write-Host "Aborted."; exit 0 }
}

# 1. Windows version -------------------------------------------------------
Write-Step "Checking Windows version"
$os = [System.Environment]::OSVersion.Version
if ($os.Major -lt 10) {
  throw "snitch.out needs Windows 10 or newer (found $os)."
}
Write-Ok "Windows $os"

# 2. winget (needed to fetch missing dependencies) --------------------------
Write-Step "Checking for winget"
if (-not (Test-Command "winget")) {
  throw "winget not found. Install 'App Installer' from the Microsoft Store, then re-run."
}
Write-Ok "winget present"

# 3. git --------------------------------------------------------------------
Write-Step "Checking for git"
if (-not (Test-Command "git")) {
  Install-WithWinget "Git.Git" "Git"
}
if (-not (Test-Command "git")) { throw "git still not on PATH after install. Restart the terminal and re-run." }
Write-Ok "git $(& git --version)"

# 4. .NET 6 SDK (to compile) -------------------------------------------------
Write-Step "Checking for .NET 6 SDK"
$sdks = & dotnet --list-sdks 2>$null
$hasSdk6 = ($sdks | Select-String "^6\.") -ne $null
if (-not $hasSdk6) {
  Install-WithWinget "Microsoft.DotNet.SDK.6" ".NET 6 SDK"
  $sdks = & dotnet --list-sdks 2>$null
  $hasSdk6 = ($sdks | Select-String "^6\.") -ne $null
  if (-not $hasSdk6) { throw ".NET 6 SDK still missing after install. Restart the terminal and re-run." }
}
Write-Ok "dotnet SDK: $(($sdks | Select-Object -First 1).Trim())"

# 5. Clone or update sources --------------------------------------------------
Write-Step "Fetching sources"
if ((Test-Path (Join-Path $SourceDir ".git"))) {
  Write-Need "Existing checkout found, pulling latest..."
  & git -C $SourceDir fetch origin 2>&1 | Out-Null
  & git -C $SourceDir checkout $Branch 2>&1 | Out-Null
  & git -C $SourceDir pull --ff-only 2>&1 | Out-Null
  & git -C $SourceDir submodule update --init --recursive 2>&1 | Out-Null
}
else {
  New-Item -ItemType Directory -Force -Path $SourceDir | Out-Null
  & git clone --recurse-submodules --branch $Branch $Repo $SourceDir
  if ($LASTEXITCODE -ne 0) { throw "git clone failed (exit $LASTEXITCODE)." }
}
Write-Ok "sources ready at $SourceDir"

$solution = Join-Path $SourceDir "Snitch.sln"
if (-not (Test-Path $solution)) { throw "Snitch.sln not found in $SourceDir. Wrong branch or repo?" }

# 6. Compile ------------------------------------------------------------------
Write-Step "Compiling snitch.out (Release, this takes a few minutes)"
$publishDir = Join-Path $InstallDir "app"
& dotnet publish (Join-Path $SourceDir "Bloxstrap\Bloxstrap.csproj") `
  -c Release -o $publishDir --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE). See output above." }

$exe = Join-Path $publishDir "snitch.out.exe"
if (-not (Test-Path $exe)) { throw "Build succeeded but $exe is missing." }
Write-Ok "built $exe"

# 7. .NET 6 Desktop Runtime (to RUN the app) ------------------------------------
Write-Step "Checking for .NET 6 Desktop Runtime"
$runtimes = & dotnet --list-runtimes 2>$null
$hasDesktop6 = ($runtimes | Select-String "Microsoft\.WindowsDesktop\.App 6\.") -ne $null
if (-not $hasDesktop6) {
  Install-WithWinget "Microsoft.DotNet.DesktopRuntime.6" ".NET 6 Desktop Runtime"
}
else {
  Write-Ok ".NET 6 Desktop Runtime present"
}

# 8. Shortcuts ------------------------------------------------------------------
if (-not $NoShortcuts) {
  Write-Step "Creating Start Menu shortcut"
  $startMenu = [System.Environment]::GetFolderPath("StartMenu")
  $linkDir = Join-Path $startMenu "Programs\snitch.out"
  New-Item -ItemType Directory -Force -Path $linkDir | Out-Null
  $shell = New-Object -ComObject WScript.Shell
  $link = $shell.CreateShortcut((Join-Path $linkDir "snitch.out.lnk"))
  $link.TargetPath = $exe
  $link.WorkingDirectory = $publishDir
  $link.Description = "snitch.out - Roblox bootstrapper"
  $link.Save() | Out-Null
  Write-Ok "Start Menu > snitch.out"
}

# 9. Launch (first run performs the app's own setup) ------------------------------
Write-Host "`nsnitch.out is installed at $InstallDir" -ForegroundColor Green
if (-not $NoLaunch) {
  Write-Step "Launching snitch.out"
  Start-Process $exe
}

Write-Host "`nDone. Re-run install.bat any time to update + rebuild." -ForegroundColor Green
