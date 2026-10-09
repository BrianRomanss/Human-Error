using UnityEditor;
using UnityEngine;

namespace HumanError.EditorTools
{
    /// <summary>
    /// Import settings for everything under Assets/_Game/Art: UI sprites, no mipmaps, no compression
    /// artefacts on flat colours. Files named *_9s get a 9-slice border.
    /// </summary>
    public sealed class ArtImportRules : AssetPostprocessor
    {
        const string ArtFolder = "Assets/_Game/Art/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtFolder)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.wrapMode = TextureWrapMode.Clamp;
            if (assetPath.Contains("/Backgrounds/")) ti.maxTextureSize = 2048;
            if (assetPath.EndsWith("_9s.png"))
            {
                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                ti.SetTextureSettings(settings);
                ti.spriteBorder = new Vector4(20, 20, 20, 20);
            }
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/_Game/Audio/")) return;
            var ai = (AudioImporter)assetImporter;
            var s = ai.defaultSampleSettings;
            s.loadType = assetPath.Contains("music_") ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            ai.defaultSampleSettings = s;
        }
    }
}
