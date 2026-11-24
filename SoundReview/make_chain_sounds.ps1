# Synthesizes two draft chain sounds for the author to approve (mono, 48 kHz, 16 bit):
#   chain_strain.wav  a 3 s loop of metal under tension: a creaking tone that wanders in pitch with stick-slip jerks and grit
#   chain_snap.wav    2 s: a sharp crack and ring, then loose links falling and bouncing away
param([string]$Out = $PSScriptRoot)
$sr = 48000
$rnd = New-Object System.Random 7

function Save-Wav([double[]]$x, [string]$path)
{
    $peak = 0.0; foreach ($v in $x) { $a = [Math]::Abs($v); if ($a -gt $peak) { $peak = $a } }
    $g = 0.85 / $peak
    $fs = [System.IO.File]::Create($path); $w = New-Object System.IO.BinaryWriter $fs
    $n = $x.Length
    $w.Write([byte[]][char[]]"RIFF"); $w.Write([int](36 + $n * 2)); $w.Write([byte[]][char[]]"WAVE"); $w.Write([byte[]][char[]]"fmt ")
    $w.Write([int]16); $w.Write([int16]1); $w.Write([int16]1); $w.Write([int]$sr); $w.Write([int]($sr * 2)); $w.Write([int16]2); $w.Write([int16]16)
    $w.Write([byte[]][char[]]"data"); $w.Write([int]($n * 2))
    foreach ($v in $x) { $s = [int][Math]::Round([Math]::Max(-1.0, [Math]::Min(1.0, $v * $g)) * 32767); $w.Write([int16]$s) }
    $w.Close(); $fs.Close()
}

# ---- chain_strain: 3 s loop ----
$loopLen = 3.0; $nStrain = [int]($sr * $loopLen)
$y = New-Object 'double[]' $nStrain
$phase = 0.0; $lp = 0.0; $lp2 = 0.0
$twoPi = 2.0 * [Math]::PI
for ($i = 0; $i -lt $nStrain; $i++)
{
    $t = $i / $sr
    $f = 330.0 + 110.0 * [Math]::Sin($twoPi * $t / $loopLen) + 45.0 * [Math]::Sin($twoPi * 3.0 * $t / $loopLen)     # pitch wanders; the mean is 330 so the phase closes on itself over the loop
    $phase += $twoPi * $f / $sr
    $slip = [Math]::Abs([Math]::Sin($twoPi * (6.0 * $t / $loopLen * 3.0 + 0.35 * [Math]::Sin($twoPi * $t / $loopLen))))   # stick and slip: jerky bursts a few times a second
    $slip = [Math]::Pow($slip, 2.5)
    $tone = [Math]::Sin($phase) + 0.55 * [Math]::Sin(2.0 * $phase + 0.6) + 0.35 * [Math]::Sin(3.0 * $phase + 1.3) + 0.2 * [Math]::Sin(5.0 * $phase)
    $nz = $rnd.NextDouble() * 2.0 - 1.0
    $lp += 0.35 * ($nz - $lp); $grit = $lp - $lp2; $lp2 += 0.02 * ($lp - $lp2)                              # band-passed noise: grit
    $y[$i] = (0.35 + 0.65 * $slip) * (0.55 * $tone + 0.9 * $grit * $slip)
}
$fade = [int]($sr * 0.03)
for ($i = 0; $i -lt $fade; $i++) { $k = $i / $fade; $y[$i] *= $k; $y[$nStrain - 1 - $i] *= $k }
Save-Wav $y (Join-Path $Out "chain_strain.wav")

# ---- chain_snap: 2 s ----
$snapLen = 2.0; $nSnap = [int]($sr * $snapLen)
$z = New-Object 'double[]' $nSnap
$prev = 0.0
$ring = @(@(1900.0, 1.0, 0.55), @(2870.0, 0.7, 0.4), @(4210.0, 0.5, 0.3), @(5980.0, 0.35, 0.22), @(7400.0, 0.2, 0.15))   # frequency, amplitude, decay seconds (metal ring)
for ($i = 0; $i -lt $nSnap; $i++)
{
    $t = $i / $sr; $s = 0.0
    $nz = $rnd.NextDouble() * 2.0 - 1.0; $hp = $nz - $prev; $prev = $nz
    $s += 1.6 * $hp * [Math]::Exp(-$t / 0.004)                                                                 # the crack
    $s += 0.9 * [Math]::Sin($twoPi * 90.0 * $t) * [Math]::Exp(-$t / 0.06)                                     # the thump of the load letting go
    foreach ($r in $ring) { $s += 0.35 * $r[1] * [Math]::Sin($twoPi * $r[0] * $t) * [Math]::Exp(-$t / $r[2]) }  # the ring
    $z[$i] = $s
}
# loose links falling and bouncing away: clinks that come closer and closer together and die away
$times = @(0.14, 0.31, 0.45, 0.57, 0.67, 0.75, 0.82, 0.88, 0.93, 0.97) + @(0.22, 0.38, 0.52, 0.71, 0.79, 1.05, 1.18, 1.34)
for ($c = 0; $c -lt $times.Count; $c++)
{
    $t0 = $times[$c]; $amp = 0.55 * [Math]::Pow(0.93, $c) * (0.6 + 0.4 * $rnd.NextDouble()); $fq = 2400.0 + 3600.0 * $rnd.NextDouble(); $dec = 0.05 + 0.08 * $rnd.NextDouble()
    $start = [int]($t0 * $sr); $len = [int]($sr * 0.4)
    for ($j = 0; $j -lt $len -and ($start + $j) -lt $nSnap; $j++)
    {
        $tt = $j / $sr
        $z[$start + $j] += $amp * ([Math]::Sin($twoPi * $fq * $tt) + 0.5 * [Math]::Sin($twoPi * $fq * 1.52 * $tt)) * [Math]::Exp(-$tt / $dec) * [Math]::Min(1.0, $tt / 0.001)
    }
}
$fadeEnd = [int]($sr * 0.15)
for ($i = 0; $i -lt $fadeEnd; $i++) { $z[$nSnap - 1 - $i] *= $i / $fadeEnd }
Save-Wav $z (Join-Path $Out "chain_snap.wav")
"done"
