# Builds chain_strain.wav and chain_snap.wav from the author's own recording (Assets/Audio/SoundEffects/Chains rattling.wav), so they have the real metal in them:
# the recording is cut into its clinks (grains), which are re-laid with different pitch, density and level.
#   chain_strain.wav  3 s seamless loop: a dense, grinding stream of tiny clinks pitched up, as if the links were being dragged tight, with a few metallic squeals underneath
#   chain_snap.wav    2.4 s: one heavy clang built from the loudest clinks stacked and pitched down, a crack, a ring, then links falling away, spacing out and fading
param([string]$Src = "C:\Ashwin\Unity\Bystander\Assets\Audio\SoundEffects\Chains rattling.wav", [string]$Out = $PSScriptRoot)
$sr = 48000; $twoPi = 2.0 * [Math]::PI
$rnd = New-Object System.Random 11

# ---- read the recording (mono, 24 bit) ----
$b = [IO.File]::ReadAllBytes($Src); $pos = 12; $dataPos = 0; $dataLen = 0
while ($pos -lt $b.Length - 8) { $id = [Text.Encoding]::ASCII.GetString($b, $pos, 4); $len = [BitConverter]::ToInt32($b, $pos + 4); if ($id -eq 'data') { $dataPos = $pos + 8; $dataLen = $len; break }; $pos += 8 + $len + ($len % 2) }
$frames = [int]($dataLen / 3); $m = New-Object 'double[]' $frames
for ($i = 0; $i -lt $frames; $i++) { $o = $dataPos + 3 * $i; $m[$i] = ((([int]$b[$o + 2] -shl 24) -bor ([int]$b[$o + 1] -shl 16) -bor ([int]$b[$o] -shl 8)) / 2147483648.0) }

# ---- find the clinks: short bursts that rise sharply out of the surrounding level ----
$blk = 240; $nb = [int]($frames / $blk); $e = New-Object 'double[]' $nb
for ($k = 0; $k -lt $nb; $k++) { $s = 0.0; for ($i = 0; $i -lt $blk; $i++) { $v = $m[$k * $blk + $i]; $s += $v * $v }; $e[$k] = [Math]::Sqrt($s / $blk) }
$sorted = [double[]]$e.Clone(); [Array]::Sort($sorted); $med = $sorted[[int]($nb * 0.5)]; $thr = [Math]::Max(0.004, $med * 4.0)
$grains = New-Object System.Collections.ArrayList; $lastK = -100
for ($k = 2; $k -lt $nb - 40; $k++)
{
    if ($e[$k] -gt $thr -and $e[$k] -gt 1.8 * $e[$k - 1] -and ($k - $lastK) -gt 12)
    {
        $lastK = $k; $st = [Math]::Max(0, $k * $blk - 48); $glen = 7200   # 150 ms
        $g = New-Object 'double[]' $glen; $sq = 0.0
        for ($i = 0; $i -lt $glen; $i++) { $v = $m[$st + $i]; $f = [Math]::Min(1.0, $i / 144.0) * [Math]::Min(1.0, ($glen - $i) / 960.0); $g[$i] = $v * $f; $sq += $g[$i] * $g[$i] }
        [void]$grains.Add(@{ data = $g; rms = [Math]::Sqrt($sq / $glen) })
    }
}
"found $($grains.Count) clinks (threshold $([Math]::Round($thr, 4)))"
if ($grains.Count -lt 8) { throw "too few clinks found" }
$sortedG = @($grains | Sort-Object { $_.rms })
$quiet = $sortedG[0..([int]($sortedG.Count * 0.6))]; $loud = $sortedG[([int]($sortedG.Count * 0.8))..($sortedG.Count - 1)]

function Add-Grain([double[]]$dst, $grain, [double]$at, [double]$rate, [double]$amp, [bool]$wrap)
{
    $src = $grain.data; $n = $dst.Length; $norm = $amp / [Math]::Max(0.001, $grain.rms); $outLen = [int]($src.Length / $rate)
    for ($j = 0; $j -lt $outLen; $j++)
    {
        $p = $j * $rate; $i0 = [int][Math]::Floor($p); if ($i0 + 1 -ge $src.Length) { break }; $fr = $p - $i0
        $v = ($src[$i0] * (1.0 - $fr) + $src[$i0 + 1] * $fr) * $norm
        $idx = [int]$at + $j
        if ($wrap) { $idx = $idx % $n } elseif ($idx -ge $n) { break }
        $dst[$idx] += $v
    }
}
function Save-Wav([double[]]$x, [string]$path, [double]$target)
{
    $peak = 0.0; foreach ($v in $x) { $a = [Math]::Abs($v); if ($a -gt $peak) { $peak = $a } }; $g = $target / $peak
    $fs = [IO.File]::Create($path); $w = New-Object IO.BinaryWriter $fs; $cnt = $x.Length
    $w.Write([byte[]][char[]]"RIFF"); $w.Write([int](36 + $cnt * 2)); $w.Write([byte[]][char[]]"WAVE"); $w.Write([byte[]][char[]]"fmt ")
    $w.Write([int]16); $w.Write([int16]1); $w.Write([int16]1); $w.Write([int]$sr); $w.Write([int]($sr * 2)); $w.Write([int16]2); $w.Write([int16]16)
    $w.Write([byte[]][char[]]"data"); $w.Write([int]($cnt * 2))
    foreach ($v in $x) { $q = [int][Math]::Round([Math]::Max(-1.0, [Math]::Min(1.0, $v * $g)) * 32767); $w.Write([int16]$q) }
    $w.Close(); $fs.Close()
}

# ---- chain_strain: 3 s loop. Wrapping grains make the seam invisible. ----
$loopN = 3 * $sr; $y = New-Object 'double[]' $loopN
$count = 110                                                                     # about 36 clinks a second: a grinding stream rather than separate rattles
for ($c = 0; $c -lt $count; $c++)
{
    $at = $rnd.NextDouble() * $loopN
    $swell = 0.6 + 0.4 * [Math]::Sin($twoPi * ($at / $loopN) * 2.0)               # the load breathes: two slow swells per loop
    $g = $quiet[$rnd.Next($quiet.Count)]
    Add-Grain $y $g $at (1.05 + 0.5 * $rnd.NextDouble()) (0.10 + 0.10 * $rnd.NextDouble() * $swell) $true   # pitched up: tight, tense links
}
for ($c = 0; $c -lt 6; $c++)                                                      # a few metallic squeals as the links bind: short, rising, with inharmonic partials, kept low
{
    $at = $rnd.NextDouble() * $loopN; $len = [int]($sr * (0.12 + 0.18 * $rnd.NextDouble())); $f0 = 1500 + 1800 * $rnd.NextDouble(); $ph = 0.0
    for ($j = 0; $j -lt $len; $j++)
    {
        $u = $j / $len; $f = $f0 * (1.0 + 0.35 * $u); $ph += $twoPi * $f / $sr; $env = [Math]::Sin([Math]::PI * $u); $env *= $env
        $y[([int]$at + $j) % $loopN] += 0.03 * $env * ([Math]::Sin($ph) + 0.6 * [Math]::Sin(2.76 * $ph) + 0.3 * [Math]::Sin(5.4 * $ph))
    }
}
Save-Wav $y (Join-Path $Out "chain_strain.wav") 0.85

# ---- chain_snap: 2.4 s ----
$snapN = [int](2.4 * $sr); $z = New-Object 'double[]' $snapN
$big = $loud[$loud.Count - 1]
Add-Grain $z $big 0 0.8 1.0 $false                                                 # the heaviest clink, pitched down
Add-Grain $z $loud[[Math]::Max(0, $loud.Count - 2)] 300 1.0 0.9 $false
Add-Grain $z $loud[[Math]::Max(0, $loud.Count - 3)] 700 0.7 0.8 $false
$prev = 0.0
for ($i = 0; $i -lt [int](0.25 * $sr); $i++)                                       # the crack: a very short high-passed noise burst, and the thump of the load letting go
{
    $t = $i / $sr; $nz = $rnd.NextDouble() * 2.0 - 1.0; $hp = $nz - $prev; $prev = $nz
    $z[$i] += 0.7 * $hp * [Math]::Exp(-$t / 0.004) + 0.5 * [Math]::Sin($twoPi * 85.0 * $t) * [Math]::Exp(-$t / 0.05)
}
$ring = @(@(2100.0, 1.0, 0.5), @(3200.0, 0.7, 0.38), @(4700.0, 0.5, 0.28), @(6300.0, 0.3, 0.2))
for ($i = 0; $i -lt $snapN; $i++) { $t = $i / $sr; foreach ($r in $ring) { $z[$i] += 0.10 * $r[1] * [Math]::Sin($twoPi * $r[0] * $t) * [Math]::Exp(-$t / $r[2]) } }
$at = 0.10 * $sr; $amp = 0.55; $gap = 0.06                                         # the loose links falling: clinks that start close together and spread out as they bounce away, quieter each time
for ($c = 0; $c -lt 22; $c++)
{
    $g = $sortedG[$rnd.Next([int]($sortedG.Count * 0.9))]
    Add-Grain $z $g $at (0.85 + 0.5 * $rnd.NextDouble()) $amp $false
    $at += ($gap + 0.03 * $rnd.NextDouble()) * $sr; $gap *= 1.08; $amp *= 0.90
    if ($at -ge $snapN - 8000) { break }
}
$fe = [int]($sr * 0.2); for ($i = 0; $i -lt $fe; $i++) { $z[$snapN - 1 - $i] *= $i / $fe }
Save-Wav $z (Join-Path $Out "chain_snap.wav") 0.85
"done"
