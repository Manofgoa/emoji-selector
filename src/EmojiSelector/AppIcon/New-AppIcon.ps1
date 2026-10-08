<#
.SYNOPSIS
    Generates app.ico, the app icon, from slightly_smiling_face_color.svg next to this script.

.DESCRIPTION
    The SVG is Fluent Emoji's "Slightly smiling face", Color style (MIT, see LICENSE next to it). It is rendered at
    every size of the icon by Microsoft Edge headless, shipped with Windows, on a transparent background; the PNGs are
    then packed into app.ico, one PNG entry per size. Nothing is downloaded: the script reads the committed SVG.

    The script is not part of the build: app.ico is committed. Run it again only when the SVG or the sizes change.

.EXAMPLE
    pwsh src/EmojiSelector/AppIcon/New-AppIcon.ps1
#>
[CmdletBinding()]
param(
    # The Edge executable; found in its usual places when not given.
    [string] $Edge
)

$ErrorActionPreference = 'Stop'

# The sizes Windows asks an icon for, from 100 % to 250 % display scale, and the 256 of the File Explorer's large views.
$sizes = 16, 20, 24, 32, 40, 48, 64, 256

$svg = Join-Path $PSScriptRoot 'slightly_smiling_face_color.svg'
$ico = Join-Path $PSScriptRoot 'app.ico'

if (-not $Edge) {
    $Edge = @(
        "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
        "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $Edge -or -not (Test-Path $Edge)) {
    throw 'Microsoft Edge not found: pass its path with -Edge.'
}

# A folder of its own: the page, the renders and Edge's profile — a profile of its own, or the render would go to an Edge
# already running.
$work = Join-Path ([IO.Path]::GetTempPath()) "new-app-icon-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $work | Out-Null
try {
    Copy-Item $svg (Join-Path $work 'icon.svg')
    $pngs = foreach ($size in $sizes) {
        $page = Join-Path $work "icon-$size.html"
        Set-Content -Path $page -Encoding utf8 -Value @"
<!doctype html>
<html><head><style>html, body { margin: 0; background: transparent; overflow: hidden; }
img { display: block; width: ${size}px; height: ${size}px; }</style></head>
<body><img src="icon.svg"></body></html>
"@
        $png = Join-Path $work "icon-$size.png"
        # Not a pipeline: Edge must have written the PNG when the next line runs.
        $process = Start-Process -FilePath $Edge -Wait -PassThru -WindowStyle Hidden -ArgumentList @(
            '--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run', '--force-device-scale-factor=1',
            "--user-data-dir=`"$(Join-Path $work 'profile')`"", '--default-background-color=00000000',
            "--window-size=$size,$size", "--screenshot=`"$png`"", "`"$([Uri]::new($page).AbsoluteUri)`"")
        if ($process.ExitCode -ne 0 -or -not (Test-Path $png)) {
            throw "Edge could not render the icon at $size px (exit code $($process.ExitCode))."
        }
        # The comma keeps the byte array one item: unrolled, every byte would be one.
        , [IO.File]::ReadAllBytes($png)
    }

    # ICO: an ICONDIR (reserved, type 1 = icon, count), one 16-byte ICONDIRENTRY per image, then the images. A width or
    # height of 256 is written 0.
    $stream = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($stream)
    $writer.Write([uint16] 0)
    $writer.Write([uint16] 1)
    $writer.Write([uint16] $sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $side = [byte] ($sizes[$i] % 256)
        $writer.Write($side)                # width
        $writer.Write($side)                # height
        $writer.Write([byte] 0)             # colour count: none, 32 bits per pixel
        $writer.Write([byte] 0)             # reserved
        $writer.Write([uint16] 1)           # colour planes
        $writer.Write([uint16] 32)          # bits per pixel
        $writer.Write([uint32] $pngs[$i].Length)
        $writer.Write([uint32] $offset)
        $offset += $pngs[$i].Length
    }
    foreach ($png in $pngs) {
        $writer.Write($png)
    }
    $writer.Flush()
    [IO.File]::WriteAllBytes($ico, $stream.ToArray())
    Write-Host "Written $ico ($($sizes -join ', ') px)."
}
finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
