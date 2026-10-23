<#
.SYNOPSIS
    Downloads the Twemoji flag images the app embeds: the country and subdivision flags Segoe UI Emoji cannot draw.

.DESCRIPTION
    The flags are the entries of the flags group (9) of ../../Data/Emojibase/compact.en.json whose text holds a
    regional indicator (a country flag, 🇫🇷) or a tag character (a subdivision flag, England) — the rule of
    Drawing/FlagImages.NeedsImage. Every other flag (🏁, 🏳️‍🌈…) is drawn by Segoe UI Emoji. Each one's SVG is downloaded from jsDelivr at the pinned Twemoji
    version, named as Twemoji names it: its code points in lowercase hex, joined by '-', FE0F left out. A flag Twemoji
    does not have is reported and left out: the app draws it through Segoe.

    The SVGs next to this script are replaced, with Twemoji's LICENSE-GRAPHICS (CC-BY 4.0) and version.txt. The script
    is not part of the build: the files are committed. Run it again after updating the embedded Emojibase data or to
    move to another Twemoji version.

.EXAMPLE
    pwsh src/EmojiSelector/Drawing/Twemoji/Update-FlagImages.ps1 -Version 17.0.3
#>
[CmdletBinding()]
param(
    # The Twemoji version (a tag of github.com/jdecked/twemoji); the one in version.txt when not given.
    [string] $Version
)

$ErrorActionPreference = 'Stop'

$versionFile = Join-Path $PSScriptRoot 'version.txt'
if (-not $Version) {
    $Version = (Get-Content $versionFile -Raw).Trim()
}

$baseUrl = "https://cdn.jsdelivr.net/gh/jdecked/twemoji@$Version"
$listPath = Join-Path $PSScriptRoot '..\..\Data\Emojibase\compact.en.json'

function Test-NeedsImage([string] $text) {
    foreach ($rune in $text.EnumerateRunes()) {
        $value = $rune.Value
        if (($value -ge 0x1F1E6 -and $value -le 0x1F1FF) -or ($value -ge 0xE0020 -and $value -le 0xE007F)) {
            return $true
        }
    }
    return $false
}

function Get-FileName([string] $text) {
    ($text.EnumerateRunes() | Where-Object { $_.Value -ne 0xFE0F } | ForEach-Object { '{0:x}' -f $_.Value }) -join '-'
}

$flags = Get-Content $listPath -Raw -Encoding utf8 | ConvertFrom-Json |
    Where-Object { $_.group -eq 9 -and (Test-NeedsImage $_.unicode) } |
    ForEach-Object { Get-FileName $_.unicode } |
    Sort-Object -Unique

# Downloaded to a folder of its own first: a failure leaves the committed files as they were.
$staging = Join-Path ([IO.Path]::GetTempPath()) "twemoji-flags-$Version"
Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
New-Item $staging -ItemType Directory | Out-Null

$missing = @()
foreach ($name in $flags) {
    try {
        Invoke-WebRequest "$baseUrl/assets/svg/$name.svg" -OutFile (Join-Path $staging "$name.svg")
    }
    catch {
        $missing += $name
    }
}
Invoke-WebRequest "$baseUrl/LICENSE-GRAPHICS" -OutFile (Join-Path $staging 'LICENSE-GRAPHICS')

Remove-Item (Join-Path $PSScriptRoot '*.svg')
Move-Item (Join-Path $staging '*') $PSScriptRoot -Force
Set-Content $versionFile $Version -Encoding ascii
Remove-Item $staging -Recurse -Force

Write-Host "Twemoji $Version`: $($flags.Count - $missing.Count) flag images written."
if ($missing) {
    Write-Warning "Not in Twemoji $Version, drawn by Segoe UI Emoji: $($missing -join ', ')"
}
