<#
.SYNOPSIS
  Headless balance testing: runs many AI-vs-AI matches and/or unit skirmishes in parallel and
  summarises the results.

.DESCRIPTION
  Full matches: every ordered faction pairing (so each faction plays from both spawns), N matches each,
  each with its own random seed.
  Skirmishes: round-robin of every producible unit against every other, in two formats:
  equal count ("X:10" vs "Y:10") and equal Form cost ("X:@600" vs "Y:@600"), alternating sides.

  Output goes to results/<timestamp>/:
    matches.csv              one row per full match
    summary.csv              win rate per faction pairing with sample size, average duration, win-condition split
    skirmish.csv             one row per skirmish
    skirmish_matrix_count.csv / skirmish_matrix_cost.csv   row unit's win rate against column unit (draw = 0.5)
    runs/*.json, logs/*.log  raw per-run results and player logs

.EXAMPLE
  ./tools/balance.ps1 -Mode both -Matches 20 -Parallel 6
.EXAMPLE
  ./tools/balance.ps1 -Mode skirmish -Balance tools/balance/proposed.json
#>
param(
    [ValidateSet('full', 'skirmish', 'both')]
    [string]$Mode = 'both',
    [Alias('Matches')]
    [int]$MatchCount = 20,
    [int]$SkirmishRuns = 4,
    [int]$SkirmishCount = 10,
    [int]$SkirmishBudget = 600,
    [int]$Parallel = [Math]::Max(1, [Environment]::ProcessorCount - 2),
    [double]$TimeScale = 8,
    [int]$MaxMatchTime = 2400,
    [int]$ProcessTimeoutSeconds = 900,
    [string]$Balance,
    [string]$Exe,
    [string]$OutDir,
    # Skip running and only rebuild the CSVs from an existing OutDir's runs/ folder.
    [switch]$AggregateOnly
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path $PSScriptRoot -Parent
if (-not $Exe) { $Exe = Join-Path $projectPath 'Builds/Windows/DawnOfShapes.exe' }
if (-not (Test-Path $Exe)) { throw "Player build not found at $Exe. Run build.ps1 first." }
if (-not $OutDir) { $OutDir = Join-Path $projectPath ("results/" + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$runsDir = Join-Path $OutDir 'runs'
$logsDir = Join-Path $OutDir 'logs'
New-Item -ItemType Directory -Force $runsDir, $logsDir | Out-Null
if ($Balance) { $Balance = (Resolve-Path $Balance).Path }

$common = @('-batchmode', '-nographics', '-timescale', "$TimeScale", '-maxMatchTime', "$MaxMatchTime")
if ($Balance) { $common += @('-balance', "`"$Balance`"") }

function Invoke-Player([string[]]$extraArgs, [string]$name) {
    $log = Join-Path $logsDir "$name.log"
    $p = Start-Process -FilePath $Exe -ArgumentList ($common + $extraArgs + @('-logFile', "`"$log`"")) -PassThru -WindowStyle Hidden
    $null = $p.Handle
    return $p
}

# ---- Unit catalogue (respects balance overrides) ----
$catalogPath = Join-Path $OutDir 'catalogue.json'
if (-not ($AggregateOnly -and (Test-Path $catalogPath))) {
    $p = Invoke-Player @('-listUnits', '-results', "`"$catalogPath`"") 'catalogue'
    $p.WaitForExit()
}
if (-not (Test-Path $catalogPath)) { throw "Could not read unit catalogue; see $logsDir/catalogue.log" }
$catalogue = Get-Content $catalogPath -Raw | ConvertFrom-Json
$factions = @($catalogue.factions)
$units = @($catalogue.units | Where-Object { $_.producible })
Write-Host "Factions: $($factions -join ', ')"
Write-Host "Units: $(($units | ForEach-Object { "$($_.key) ($($_.cost))" }) -join ', ')"

# ---- Job list ----
$rng = [System.Random]::new()
$jobs = [System.Collections.Generic.List[object]]::new()
if ($Mode -in 'full', 'both') {
    foreach ($a in $factions) {
        foreach ($b in $factions) {
            if ($a -eq $b) { continue }
            for ($m = 1; $m -le $MatchCount; $m++) {
                $seed = $rng.Next(1, [int]::MaxValue)
                $name = "full_${a}_vs_${b}_$m"
                $jobs.Add([pscustomobject]@{ Name = $name; Kind = 'full'
                    Args = @('-autoplay', '-faction', $a, '-opponent', $b, '-seed', "$seed", '-results', "`"$(Join-Path $runsDir "$name.json")`"") })
            }
        }
    }
}
if ($Mode -in 'skirmish', 'both') {
    for ($i = 0; $i -lt $units.Count; $i++) {
        for ($j = $i + 1; $j -lt $units.Count; $j++) {
            $x = $units[$i].key; $y = $units[$j].key
            foreach ($format in 'count', 'cost') {
                $qty = if ($format -eq 'count') { "$SkirmishCount" } else { "@$SkirmishBudget" }
                for ($r = 1; $r -le $SkirmishRuns; $r++) {
                    $first, $second = if ($r % 2 -eq 1) { $x, $y } else { $y, $x }
                    $seed = $rng.Next(1, [int]::MaxValue)
                    $name = "sk_${format}_${first}_vs_${second}_$r"
                    $jobs.Add([pscustomobject]@{ Name = $name; Kind = 'skirmish'; Format = $format
                        Args = @('-skirmish', "${first}:$qty", "${second}:$qty", '-seed', "$seed", '-results', "`"$(Join-Path $runsDir "$name.json")`"") })
                }
            }
        }
    }
}

# ---- Run with a parallel limit ----
if (-not $AggregateOnly) { Write-Host "Running $($jobs.Count) runs, $Parallel at a time, timescale $TimeScale. Output: $OutDir" }
if ($AggregateOnly) { $jobs.Clear() }
$queue = [System.Collections.Generic.Queue[object]]::new($jobs)
$running = @{}
$done = 0; $failed = 0
$started = Get-Date
while ($queue.Count -gt 0 -or $running.Count -gt 0) {
    while ($queue.Count -gt 0 -and $running.Count -lt $Parallel) {
        $job = $queue.Dequeue()
        $running[$job.Name] = [pscustomobject]@{ Job = $job; Process = (Invoke-Player $job.Args $job.Name); Start = Get-Date }
    }
    Start-Sleep -Milliseconds 250
    foreach ($name in @($running.Keys)) {
        $r = $running[$name]
        $timedOut = ((Get-Date) - $r.Start).TotalSeconds -gt $ProcessTimeoutSeconds
        if ($r.Process.HasExited -or $timedOut) {
            if ($timedOut -and -not $r.Process.HasExited) { Stop-Process -Id $r.Process.Id -Force; $failed++ }
            elseif (-not (Test-Path (Join-Path $runsDir "$name.json"))) { $failed++ }
            $running.Remove($name)
            $done++
            if ($done % 10 -eq 0 -or $done -eq $jobs.Count) {
                $elapsed = ((Get-Date) - $started).TotalSeconds
                Write-Host ("  {0}/{1} done ({2} failed), {3:F0}s elapsed" -f $done, $jobs.Count, $failed, $elapsed)
            }
        }
    }
}

# ---- Aggregate ----
function Format-Map($obj) {
    if ($null -eq $obj) { return '' }
    return (($obj.PSObject.Properties | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ';')
}

$results = Get-ChildItem $runsDir -Filter '*.json' | ForEach-Object { Get-Content $_.FullName -Raw | ConvertFrom-Json }

$full = @($results | Where-Object { $_.mode -eq 'full' })
if ($full.Count -gt 0) {
    $rows = foreach ($r in $full) {
        $a = $r.players[0]; $b = $r.players[1]
        [pscustomobject]@{
            seed = $r.seed; factionA = $a.faction; factionB = $b.faction
            winner = if ($r.winnerFaction) { $r.winnerFaction } else { 'draw' }
            winCondition = $r.winCondition; duration = $r.duration
            formEarnedA = $a.formEarned; formEarnedB = $b.formEarned
            formSpentA = $a.formSpent; formSpentB = $b.formSpent
            vpA = $a.victoryPoints; vpB = $b.victoryPoints
            unitsBuiltA = Format-Map $a.unitsBuilt; unitsBuiltB = Format-Map $b.unitsBuilt
            unitsLostA = Format-Map $a.unitsLost; unitsLostB = Format-Map $b.unitsLost
            killsA = Format-Map $a.kills; killsB = Format-Map $b.kills
            upgradesA = ($a.upgrades -join ';'); upgradesB = ($b.upgrades -join ';')
            pointsHeldA = ($a.pointsHeldSamples -join '|'); pointsHeldB = ($b.pointsHeldSamples -join '|')
        }
    }
    $rows | Export-Csv (Join-Path $OutDir 'matches.csv') -NoTypeInformation

    # One summary row per unordered pairing, from the first faction's point of view, plus each faction overall.
    $summary = [System.Collections.Generic.List[object]]::new()
    for ($i = 0; $i -lt $factions.Count; $i++) {
        for ($j = $i + 1; $j -lt $factions.Count; $j++) {
            $x = $factions[$i]; $y = $factions[$j]
            $games = @($rows | Where-Object { ($_.factionA -eq $x -and $_.factionB -eq $y) -or ($_.factionA -eq $y -and $_.factionB -eq $x) })
            if ($games.Count -eq 0) { continue }
            $summary.Add([pscustomobject]@{
                matchup = "$x vs $y"; games = $games.Count
                winsFirst = @($games | Where-Object winner -eq $x).Count
                winsSecond = @($games | Where-Object winner -eq $y).Count
                draws = @($games | Where-Object winner -eq 'draw').Count
                winRateFirst = [Math]::Round(@($games | Where-Object winner -eq $x).Count / $games.Count, 3)
                avgDurationSec = [Math]::Round(($games | Measure-Object duration -Average).Average, 0)
                domination = @($games | Where-Object winCondition -eq 'domination').Count
                annihilation = @($games | Where-Object winCondition -eq 'annihilation').Count
                victoryPoints = @($games | Where-Object winCondition -eq 'victory_points').Count
                timeout = @($games | Where-Object winCondition -eq 'timeout').Count
            })
        }
    }
    foreach ($x in $factions) {
        $games = @($rows | Where-Object { $_.factionA -eq $x -or $_.factionB -eq $x })
        if ($games.Count -eq 0) { continue }
        $wins = @($games | Where-Object winner -eq $x).Count
        $summary.Add([pscustomobject]@{
            matchup = "$x vs all"; games = $games.Count; winsFirst = $wins
            winsSecond = @($games | Where-Object { $_.winner -ne $x -and $_.winner -ne 'draw' }).Count
            draws = @($games | Where-Object winner -eq 'draw').Count
            winRateFirst = [Math]::Round($wins / $games.Count, 3)
            avgDurationSec = [Math]::Round(($games | Measure-Object duration -Average).Average, 0)
            domination = @($games | Where-Object { $_.winner -eq $x -and $_.winCondition -eq 'domination' }).Count
            annihilation = @($games | Where-Object { $_.winner -eq $x -and $_.winCondition -eq 'annihilation' }).Count
            victoryPoints = @($games | Where-Object { $_.winner -eq $x -and $_.winCondition -eq 'victory_points' }).Count
            timeout = @($games | Where-Object winCondition -eq 'timeout').Count
        })
    }
    $summary | Export-Csv (Join-Path $OutDir 'summary.csv') -NoTypeInformation
    Write-Host "`nFull matches (winRateFirst = first-named faction's win rate):"
    $summary | Format-Table -AutoSize | Out-String | Write-Host
}

$sk = @($results | Where-Object { $_.mode -eq 'skirmish' })
if ($sk.Count -gt 0) {
    $skRows = foreach ($r in $sk) {
        $a = $r.players[0]; $b = $r.players[1]
        $unitA = ($r.armyA -split ':')[0]; $unitB = ($r.armyB -split ':')[0]
        $format = if ($r.armyA.Contains(':@')) { 'cost' } else { 'count' }
        [pscustomobject]@{
            format = $format; unitA = $unitA; unitB = $unitB; armyA = $r.armyA; armyB = $r.armyB; seed = $r.seed
            winner = switch ($r.winnerTeam) { 0 { $unitA } 1 { $unitB } default { 'draw' } }
            duration = $r.duration
            startCostA = $a.formSpent; startCostB = $b.formSpent
            survivorsA = $a.survivors; survivorsB = $b.survivors
            survivorHpFracA = $a.survivorHealthFraction; survivorHpFracB = $b.survivorHealthFraction
            survivorCostA = $a.survivorCost; survivorCostB = $b.survivorCost
        }
    }
    $skRows | Export-Csv (Join-Path $OutDir 'skirmish.csv') -NoTypeInformation

    $keys = @($units | ForEach-Object key)
    foreach ($format in 'count', 'cost') {
        $matrix = foreach ($row in $keys) {
            $line = [ordered]@{ unit = $row }
            foreach ($col in $keys) {
                if ($row -eq $col) { $line[$col] = ''; continue }
                $games = @($skRows | Where-Object { $_.format -eq $format -and (($_.unitA -eq $row -and $_.unitB -eq $col) -or ($_.unitA -eq $col -and $_.unitB -eq $row)) })
                if ($games.Count -eq 0) { $line[$col] = ''; continue }
                $score = (@($games | Where-Object winner -eq $row).Count + 0.5 * @($games | Where-Object winner -eq 'draw').Count) / $games.Count
                $line[$col] = [Math]::Round($score, 2)
            }
            [pscustomobject]$line
        }
        $matrix | Export-Csv (Join-Path $OutDir "skirmish_matrix_$format.csv") -NoTypeInformation
        $label = if ($format -eq 'count') { "equal count ($SkirmishCount each)" } else { "equal Form ($SkirmishBudget each)" }
        Write-Host "`nSkirmish win rate of row unit vs column unit, ${label}:"
        $matrix | Format-Table -AutoSize | Out-String | Write-Host
    }
}

Write-Host "Done: $($results.Count) results, $failed failed. Output in $OutDir"
