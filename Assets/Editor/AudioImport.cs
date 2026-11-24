using UnityEditor;
using UnityEngine;

// Import settings for the film's sounds (Assets/Resources/Audio), applied when Unity imports them, so the Quest build stays small and nothing hitches:
// short sounds are compressed in memory (Vorbis), anything longer than 60 s (only the scared-crowd loop, 133 s) streams from disk. All stay mono as made.
public class AudioImport : AssetPostprocessor
{
    [MenuItem("Bystander/Reimport Audio")]
    static void Reimport() { AssetDatabase.ImportAsset("Assets/Resources/Audio", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate); Debug.Log("Bystander: audio reimported with the film's import settings"); }

    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/Resources/Audio/")) return;
        var imp = (AudioImporter)assetImporter;
        float len = new System.IO.FileInfo(assetPath).Length / 96000f;   // seconds, from the file size (48 kHz, 16 bit, mono): the clip cannot be loaded yet while it is being imported
        var s = imp.defaultSampleSettings;
        s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = 0.75f;
        s.loadType = len > 60f ? AudioClipLoadType.Streaming : AudioClipLoadType.CompressedInMemory;
        s.preloadAudioData = len <= 60f; imp.defaultSampleSettings = s; imp.forceToMono = false;
        var a = imp.GetOverrideSampleSettings("Android"); a = s; imp.SetOverrideSampleSettings("Android", a);
    }
}
