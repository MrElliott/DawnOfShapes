<#
.SYNOPSIS
  Per-unit efficiency from a balance.ps1 output folder.
.DESCRIPTION
  skirmishScore: average win score (draw = 0.5) against every other unit at equal Form cost.
  skirmishMargin: average of (own surviving HP fraction - opponent's), -1..1; shows how close fights were.
  dmgPerForm: damage dealt in full matches per Form spent building that unit type
  (Orb Cluster includes the Mini Rollers it releases).
.EXAMPLE
  ./tools/unit-report.ps1 results/baseline
#>
param([Parameter(Mandatory)] [string]$OutDir)

$catalogue = (Get-Content (Join-Path $OutDir 'catalogue.json') -Raw | ConvertFrom-Json).units
$skPath = Join-Path $OutDir 'skirmish.csv'
$sk = if (Test-Path $skPath) { @(Import-Csv $skPath | Where-Object format -eq 'cost') } else { @() }

$built = @{}; $damage = @{}
Get-ChildItem (Join-Path $OutDir 'runs') -Filter 'full_*.json' | ForEach-Object {
    $r = Get-Content $_.FullName -Raw | ConvertFrom-Json
    foreach ($p in $r.players) {
        foreach ($e in $p.unitsBuilt.PSObject.Properties) { $built[$e.Name] = [double]$built[$e.Name] + [double]$e.Value }
        foreach ($e in $p.damageDealt.PSObject.Properties) {
            $key = if ($e.Name -eq 'MiniRoller') { 'OrbCluster' } else { $e.Name }
            $damage[$key] = [double]$damage[$key] + [double]$e.Value
        }
    }
}

$catalogue | Where-Object producible | ForEach-Object {
    $u = $_.key
    $games = @($sk | Where-Object { $_.unitA -eq $u -or $_.unitB -eq $u })
    $score = if ($games.Count) {
        (@($games | Where-Object winner -eq $u).Count + 0.5 * @($games | Where-Object winner -eq 'draw').Count) / $games.Count
    } else { $null }
    # Signed margin: this unit's surviving HP fraction minus the opponent's, averaged over fights (-1..1).
    $margins = foreach ($g in $games) {
        if ($g.unitA -eq $u) { [double]$g.survivorHpFracA - [double]$g.survivorHpFracB }
        else { [double]$g.survivorHpFracB - [double]$g.survivorHpFracA }
    }
    [pscustomobject]@{
        unit = $u; faction = $_.faction; cost = $_.cost
        skirmishScore = if ($null -ne $score) { [Math]::Round($score, 2) } else { '' }
        skirmishMargin = if ($games.Count) { [Math]::Round(($margins | Measure-Object -Average).Average, 2) } else { '' }
        built = $built[$u]
        dmgPerForm = if ($built[$u]) { [Math]::Round($damage[$u] / ($built[$u] * $_.cost), 2) } else { '' }
    }
} | Format-Table -AutoSize
