# Converts mp3 files to 48 kHz 16-bit WAV with the Windows built-in media transcoder (no extra software).  Usage: mp3_to_wav.ps1 -In file.mp3 -Out file.wav [-Channels 1|2]
param([string]$In, [string]$Out, [int]$Channels = 2)
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
$null = [Windows.Media.Transcoding.MediaTranscoder, Windows.Media.Transcoding, ContentType = WindowsRuntime]
$null = [Windows.Media.MediaProperties.MediaEncodingProfile, Windows.Media.MediaProperties, ContentType = WindowsRuntime]
$asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
function Await($op, [Type]$type) { $t = $asTaskGeneric.MakeGenericMethod($type).Invoke($null, @($op)); $t.Wait(-1) | Out-Null; $t.Result }
function AwaitAction($op) { $t = [System.WindowsRuntimeSystemExtensions]::AsTask($op); $t.Wait(-1) | Out-Null }
$src = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync([IO.Path]::GetFullPath($In))) ([Windows.Storage.StorageFile])
$full = [IO.Path]::GetFullPath($Out); $outDir = Split-Path $full -Parent; $outName = Split-Path $full -Leaf
$folder = Await ([Windows.Storage.StorageFolder]::GetFolderFromPathAsync($outDir)) ([Windows.Storage.StorageFolder])
$dst = Await ($folder.CreateFileAsync($outName, [Windows.Storage.CreationCollisionOption]::ReplaceExisting)) ([Windows.Storage.StorageFile])
$profile = [Windows.Media.MediaProperties.MediaEncodingProfile]::CreateWav([Windows.Media.MediaProperties.AudioEncodingQuality]::High)
$profile.Audio.SampleRate = 48000; $profile.Audio.ChannelCount = $Channels; $profile.Audio.BitsPerSample = 16
$tc = New-Object Windows.Media.Transcoding.MediaTranscoder
$prep = Await ($tc.PrepareFileTranscodeAsync($src, $dst, $profile)) ([Windows.Media.Transcoding.PrepareTranscodeResult])
if (-not $prep.CanTranscode) { throw "cannot transcode: $($prep.FailureReason)" }
$asTaskProg = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncActionWithProgress`1' })[0]
$tk = $asTaskProg.MakeGenericMethod([double]).Invoke($null, @($prep.TranscodeAsync())); $tk.Wait(-1) | Out-Null
"ok $Out"
