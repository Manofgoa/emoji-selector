<#
.SYNOPSIS
    Generates subgroups.json next to this script: the emojis' sub-categories, derived from Emojibase's en/data.json and
    the messages.json of English and French, for the version in version.txt.

.DESCRIPTION
    The app's Check for emoji updates… derives the same file (Data/EmojiSubgroups.Derive) for a newer version. Run this
    script again after replacing the embedded copy with another Emojibase version (CONTRIBUTING.md § Emoji data).
#>
$ErrorActionPreference = 'Stop'
$version = (Get-Content (Join-Path $PSScriptRoot 'version.txt') -Raw).Trim()
$base = "https://cdn.jsdelivr.net/npm/emojibase-data@$version"

$data = Invoke-RestMethod "$base/en/data.json"
# By order. Two subgroups sharing an order keep the last one listed: Emojibase 17 lists food-marine and food-sweet both
# at 46, and its data.json puts the sweets there.
$byOrder = @{}
foreach ($subgroup in (Invoke-RestMethod "$base/en/messages.json").subgroups) { $byOrder[[int]$subgroup.order] = $subgroup }
$french = @{}
foreach ($subgroup in (Invoke-RestMethod "$base/fr/messages.json").subgroups) { $french[[int]$subgroup.order] = $subgroup.message }

# A subgroup's number is its index in the list: the orders must run from 0 without a gap.
$count = ($byOrder.Keys | Measure-Object -Maximum).Maximum + 1
$english = @(0..($count - 1) | ForEach-Object {
    if (-not $byOrder.ContainsKey($_)) { throw "The subgroups of messages.json are not numbered from 0 without a gap." }
    $byOrder[$_]
})

$emojis = [ordered]@{}
foreach ($entry in $data | Where-Object { $null -ne $_.subgroup } | Sort-Object hexcode -CaseSensitive) {
    if (-not $emojis.Contains($entry.hexcode)) { $emojis[$entry.hexcode] = [int]$entry.subgroup }
}

$file = [ordered]@{
    subgroups = @($english | ForEach-Object {
        [ordered]@{ key = $_.key; name = $_.message; frenchName = $french[[int]$_.order] ?? '' }
    })
    emojis = $emojis
}

$json = $file | ConvertTo-Json -Depth 4
[System.IO.File]::WriteAllText((Join-Path $PSScriptRoot 'subgroups.json'), $json + "`n", [System.Text.UTF8Encoding]::new($false))
"subgroups.json: $($english.Count) subgroups, $($emojis.Count) emojis (Emojibase $version)"
