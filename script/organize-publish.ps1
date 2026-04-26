#Requires -Version 7.0
param(
    [Parameter(Mandatory = $true, HelpMessage = "Path to the publish output directory")]
    [string]$PublishDir
)

$ErrorActionPreference = "Stop"
$PublishDir = Resolve-Path $PublishDir

# Files that must stay in root for self-contained app to run
$rootFiles = @(
    "Taix.exe",
    "Taix.dll",
    "Taix.deps.json",
    "Taix.runtimeconfig.json"
)

$rootPatterns = @(
    "hostfxr.dll",
    "hostpolicy.dll",
    "coreclr.dll",
    "clrjit.dll",
    "clrgc.dll",
    "clretwrc.dll",
    "mscordaccore*",
    "mscordbi.dll",
    "mscorrc.dll",
    "System.IO.Compression.Native.dll",
    "Microsoft.DiaSymReader.Native.*.dll",
    "msquic.dll"
)

function ShouldKeepInRoot($fileName) {
    if ($rootFiles -contains $fileName) { return $true }
    foreach ($pattern in $rootPatterns) {
        if ($fileName -like $pattern) { return $true }
    }
    return $false
}

# Create bin/ subdirectory
$binDir = Join-Path $PublishDir "bin"
New-Item -ItemType Directory -Force -Path $binDir | Out-Null
Write-Host "Created bin directory: $binDir"

# Move managed DLLs to bin/, keep runtime host files in root
Get-ChildItem -Path $PublishDir -Filter "*.dll" -File | ForEach-Object {
    if (-not (ShouldKeepInRoot $_.Name)) {
        $dest = Join-Path $binDir $_.Name
        Move-Item -Path $_.FullName -Destination $dest -Force
        Write-Host "Moved DLL: $($_.Name) -> bin/"
    } else {
        Write-Host "Kept in root: $($_.Name)"
    }
}

# Move runtimes/ directory to bin/
$runDir = Join-Path $PublishDir "runtimes"
if (Test-Path $runDir) {
    $destRunDir = Join-Path $binDir "runtimes"
    Move-Item -Path $runDir -Destination $destRunDir -Force
    Write-Host "Moved runtimes/ -> bin/runtimes/"
}

# Update paths in Taix.deps.json
$depsFile = Join-Path $PublishDir "Taix.deps.json"
if (-not (Test-Path $depsFile)) {
    Write-Warning "deps.json not found at $depsFile, skipping path update."
    exit 0
}

$deps = Get-Content $depsFile -Raw | ConvertFrom-Json -AsHashtable -Depth 100

# Collect list of moved files (relative to PublishDir)
$movedFiles = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
Get-ChildItem -Path $binDir -Recurse -File | ForEach-Object {
    $relativePath = $_.FullName.Substring($PublishDir.Length + 1).Replace('\', '/')
    [void]$movedFiles.Add($relativePath)
}

# Helper: compute updated path based on actual file location
function Get-UpdatedPath($originalPath) {
    # Try 1: prefix with bin/ (for files that kept their directory structure, e.g. runtimes/)
    $candidateWithPrefix = "bin/$originalPath"
    if ($movedFiles.Contains($candidateWithPrefix)) {
        return $candidateWithPrefix
    }

    # Try 2: bin/filename (for files flattened into bin/ root)
    $fileName = [System.IO.Path]::GetFileName($originalPath)
    $candidateFlat = "bin/$fileName"
    if ($movedFiles.Contains($candidateFlat)) {
        return $candidateFlat
    }

    # Not moved, keep original path
    return $originalPath
}

# Update runtime and native paths in targets
foreach ($targetKey in $deps['targets'].Keys) {
    $target = $deps['targets'][$targetKey]
    foreach ($libKey in @($target.Keys)) {
        $lib = $target[$libKey]

        # runtime
        if ($lib['runtime']) {
            $newRuntime = [ordered]@{}
            foreach ($pathKey in @($lib['runtime'].Keys)) {
                $newPath = Get-UpdatedPath $pathKey
                $newRuntime[$newPath] = $lib['runtime'][$pathKey]
            }
            $lib['runtime'] = $newRuntime
        }

        # native
        if ($lib['native']) {
            $newNative = [ordered]@{}
            foreach ($pathKey in @($lib['native'].Keys)) {
                $newPath = Get-UpdatedPath $pathKey
                $newNative[$newPath] = $lib['native'][$pathKey]
            }
            $lib['native'] = $newNative
        }
    }
}

$deps | ConvertTo-Json -Depth 100 | Set-Content $depsFile -Encoding UTF8NoBOM
Write-Host "Updated Taix.deps.json paths."

# Remove debug/diagnostic files not needed in production
$toRemove = @(
    (Join-Path $PublishDir "createdump.exe"),
    (Join-Path $binDir "createdump.exe")
)
foreach ($file in $toRemove) {
    if (Test-Path $file) {
        Remove-Item $file -Force
        Write-Host "Removed: $file"
    }
}

# Remove any leftover PDB or XML doc files
Get-ChildItem -Path $PublishDir, $binDir -Recurse -Include "*.pdb", "*.xml" -File | ForEach-Object {
    Remove-Item $_.FullName -Force
    Write-Host "Removed: $($_.FullName)"
}

Write-Host "Publish directory organized successfully."
