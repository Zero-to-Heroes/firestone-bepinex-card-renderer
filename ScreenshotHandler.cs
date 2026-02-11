using System;
using System.Collections;
using System.Threading.Tasks;

namespace FirestoneCardsRenderer
{
    using System.IO;
    using UnityEngine;

    public class ScreenshotHandler : MonoBehaviour
    {
        private static Material _cachedWhiteMat;
        private static Material _cachedTransparentMat;

        /// <summary>
        /// The last alpha mask captured by CaptureAlphaMask.
        /// Caller is responsible for cleanup via DestroyImmediate.
        /// </summary>
        public Texture2D LastAlphaMask { get; set; }

        public IEnumerator CaptureScreenshot(
            string destFolder,
            string fileName,
            GameObject targetObject,
            int[] targetWidths,
            int width = 500, int height = 650, int startX = 617, int startY = 310)
        {
            yield return CaptureWithAlpha(destFolder, fileName, targetObject, targetWidths, startX, startY, width, height);
        }

        public IEnumerator CaptureWithAlpha(
            string destFolder,
            string fileName,
            GameObject targetObject,
            int[] targetWidths,
            int x, int y, int width, int height)
        {
            yield return new WaitForEndOfFrame();

            // 1. Capture RGB image
            Texture2D rgbImage = new Texture2D(width, height, TextureFormat.RGB24, false);
            var destPathRgb = Path.Combine(destFolder, fileName.Replace(".png", "_rgb.png"));
            rgbImage.ReadPixels(new Rect(x, y, width, height), 0, 0);
            rgbImage.Apply();
            var encoded = rgbImage.EncodeToPNG();
            if (!Directory.Exists(destFolder))
            {
                Directory.CreateDirectory(destFolder);
            }
            SavePNGAsync(destPathRgb, encoded);
            //yield return new WaitForSeconds(99999);

            // 2. Apply white material (cached)
            ApplyWhiteMaterial(targetObject);
            yield return new WaitForEndOfFrame();

            // 3. Capture alpha mask
            Texture2D alphaImage = new Texture2D(width, height, TextureFormat.RGB24, false);
            Texture2D finalImage = null;
            var destPathAlpha = Path.Combine(destFolder, fileName.Replace(".png", "_alphaSource.png"));
            try
            {

                alphaImage.ReadPixels(new Rect(x, y, width, height), 0, 0);
                alphaImage.Apply();
                SavePNGAsync(destPathAlpha, alphaImage.EncodeToPNG());

                // 4. Combine RGB + alpha
                finalImage = new Texture2D(width, height, TextureFormat.RGBA32, false);
                for (int yy = 0; yy < height; yy++)
                {
                    for (int xx = 0; xx < width; xx++)
                    {
                        Color rgb = rgbImage.GetPixel(xx, yy);
                        float alpha = alphaImage.GetPixel(xx, yy).r > 0 ? 255 : 0; // assumes white mask on black
                        finalImage.SetPixel(xx, yy, new Color(rgb.r, rgb.g, rgb.b, alpha));
                    }
                }
                finalImage.Apply();
            }
            catch (Exception e)
            {
                RendererPlugin.Logger.LogInfo($"\t\t\tCould not capture alpha mask {e}");
            }

            //yield return new WaitForSeconds(999999);
            // 5. Resize to target width
            yield return new WaitForEndOfFrame();
            try
            {

                foreach (var targetWidth in targetWidths)
                {
                    Texture2D scaledImage = ScaleTexture(finalImage, targetWidth);
                    var targetDir = Path.Combine(destFolder, targetWidth.ToString());
                    var destPath = Path.Combine(targetDir, fileName);
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }
                    SavePNGAsync(destPath, scaledImage.EncodeToPNG());
                    DestroyImmediate(scaledImage);
                    scaledImage = null; // PATCH: Null out reference
                }

                // Cleanup
                if (rgbImage != null)
                {
                    DestroyImmediate(rgbImage);
                    rgbImage = null;
                }
                if (alphaImage != null)
                {
                    DestroyImmediate(alphaImage);
                    alphaImage = null;
                }
                if (finalImage != null)
                {
                    DestroyImmediate(finalImage);
                    finalImage = null;
                }
            }
            catch (Exception e)
            {
                RendererPlugin.Logger.LogInfo($"\t\t\tCould not clean up 1 {e}");
            }
            //try
            //{
            //    File.Delete(destPathAlpha);
            //    File.Delete(destPathRgb);
            //}
            //catch (Exception e)
            //{
            //    RendererPlugin.Logger.LogInfo($"\t\t\tCould not clean up {e}");
            //}

            yield break;
        }

        /// <summary>
        /// Captures a silhouette-based alpha mask from the target object using the white material technique.
        /// The result is stored in LastAlphaMask.
        /// WARNING: This permanently replaces the target object's materials. Destroy the object after calling.
        /// </summary>
        public IEnumerator CaptureAlphaMask(
            GameObject targetObject,
            int x, int y, int width, int height)
        {
            // Let the object render one frame so it is fully initialized
            yield return new WaitForEndOfFrame();

            // Apply white material (destructive - replaces all materials)
            ApplyWhiteMaterial(targetObject);

            // Wait for the white material to be rendered
            yield return new WaitForEndOfFrame();

            // Capture the white silhouette
            Texture2D alphaSource = new Texture2D(width, height, TextureFormat.RGB24, false);
            alphaSource.ReadPixels(new Rect(x, y, width, height), 0, 0);
            alphaSource.Apply();

            // Build alpha mask: white pixels = opaque, black pixels = transparent
            Texture2D alphaMask = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] sourcePixels = alphaSource.GetPixels();
            Color[] maskPixels = new Color[sourcePixels.Length];
            for (int i = 0; i < sourcePixels.Length; i++)
            {
                float a = sourcePixels[i].r > 0 ? 1f : 0f;
                maskPixels[i] = new Color(1f, 1f, 1f, a);
            }
            alphaMask.SetPixels(maskPixels);
            alphaMask.Apply();

            DestroyImmediate(alphaSource);

            // Store for caller to retrieve
            if (LastAlphaMask != null)
                DestroyImmediate(LastAlphaMask);
            LastAlphaMask = alphaMask;

            RendererPlugin.Logger.LogInfo($"\t\tAlpha mask captured ({width}x{height})");
        }

        /// <summary>
        /// Captures a sequence of animation frames, applies a pre-captured alpha mask,
        /// performs cross-fade blending on the overlap region for seamless looping,
        /// and encodes to WebM VP9 with alpha via FFmpeg.
        /// </summary>
        public IEnumerator CaptureAnimationFrames(
            string destFolder,
            string fileName,
            Texture2D alphaMask,
            int x, int y, int width, int height,
            int targetWidth,
            int fps = 30,
            int frameCount = 90,
            int overlapFrames = 15)
        {
            int totalFrames = frameCount + overlapFrames;
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            string framesDir = Path.Combine(destFolder, $"frames_{baseName}");

            if (!Directory.Exists(framesDir))
                Directory.CreateDirectory(framesDir);
            if (!Directory.Exists(destFolder))
                Directory.CreateDirectory(destFolder);

            // Pre-fetch alpha pixel data once (reused every frame)
            Color[] alphaPixels = alphaMask.GetPixels();

            // Arrays to hold overlap region pixel data for cross-fade blending
            Color[][] headFramePixels = new Color[overlapFrames][];
            Color[][] tailFramePixels = new Color[overlapFrames][];

            RendererPlugin.Logger.LogInfo($"\t\tCapturing {totalFrames} frames ({frameCount} main + {overlapFrames} overlap)...");

            for (int i = 0; i < totalFrames; i++)
            {
                yield return new WaitForEndOfFrame();

                // Capture RGB from screen
                Texture2D rgbFrame = new Texture2D(width, height, TextureFormat.RGB24, false);
                rgbFrame.ReadPixels(new Rect(x, y, width, height), 0, 0);
                rgbFrame.Apply();

                // Combine RGB with pre-captured alpha mask
                Texture2D rgbaFrame = new Texture2D(width, height, TextureFormat.RGBA32, false);
                Color[] rgbPixels = rgbFrame.GetPixels();
                Color[] finalPixels = new Color[rgbPixels.Length];
                for (int p = 0; p < rgbPixels.Length; p++)
                {
                    finalPixels[p] = new Color(
                        rgbPixels[p].r, rgbPixels[p].g, rgbPixels[p].b,
                        alphaPixels[p].a);
                }
                rgbaFrame.SetPixels(finalPixels);
                rgbaFrame.Apply();
                DestroyImmediate(rgbFrame);

                // Keep pixel data for cross-fade overlap region
                if (i < overlapFrames)
                    headFramePixels[i] = finalPixels;
                if (i >= frameCount)
                    tailFramePixels[i - frameCount] = finalPixels;

                // Scale to target width and save
                Texture2D scaledFrame = ScaleTexture(rgbaFrame, targetWidth);
                DestroyImmediate(rgbaFrame);

                string framePath = Path.Combine(framesDir, $"frame_{i:D4}.png");
                File.WriteAllBytes(framePath, scaledFrame.EncodeToPNG());
                DestroyImmediate(scaledFrame);

                if (i % 30 == 0)
                    RendererPlugin.Logger.LogInfo($"\t\t  Frame {i}/{totalFrames}");
            }

            // Cross-fade blend the overlap frames for seamless looping
            // At i=0: output = 100% tail frame (seamless continuation from last main frame)
            // At i=overlapFrames-1: output ≈ 100% head frame (matches non-blended region)
            RendererPlugin.Logger.LogInfo($"\t\tBlending {overlapFrames} overlap frames for seamless loop...");
            for (int i = 0; i < overlapFrames; i++)
            {
                float t = (float)i / overlapFrames;
                Color[] blended = new Color[headFramePixels[i].Length];
                for (int p = 0; p < blended.Length; p++)
                    blended[p] = Color.Lerp(tailFramePixels[i][p], headFramePixels[i][p], t);

                Texture2D blendedTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                blendedTex.SetPixels(blended);
                blendedTex.Apply();

                Texture2D scaledBlended = ScaleTexture(blendedTex, targetWidth);
                DestroyImmediate(blendedTex);

                // Overwrite the original head frame with the blended version
                string framePath = Path.Combine(framesDir, $"frame_{i:D4}.png");
                File.WriteAllBytes(framePath, scaledBlended.EncodeToPNG());
                DestroyImmediate(scaledBlended);
            }

            // Delete the extra tail frames (they were only needed for blending)
            for (int i = frameCount; i < totalFrames; i++)
            {
                string framePath = Path.Combine(framesDir, $"frame_{i:D4}.png");
                try { File.Delete(framePath); }
                catch (Exception e) { RendererPlugin.Logger.LogInfo($"\t\tFailed to delete tail frame: {e.Message}"); }
            }

            // Free overlap memory
            headFramePixels = null;
            tailFramePixels = null;

            // Encode frames to WebM via FFmpeg
            yield return EncodeToWebM(framesDir, destFolder, baseName, fps, frameCount);
        }

        /// <summary>
        /// Encodes a PNG frame sequence to WebM VP9 with alpha transparency using FFmpeg.
        /// If FFmpeg is not found, logs the manual command and leaves frames on disk.
        /// </summary>
        private IEnumerator EncodeToWebM(
            string framesDir, string destFolder, string baseName, int fps, int frameCount)
        {
            string outputPath = Path.Combine(destFolder, $"{baseName}.webm");
            string inputPattern = Path.Combine(framesDir, "frame_%04d.png");

            if (!File.Exists(ReleaseConfig.FFMPEG_PATH))
            {
                RendererPlugin.Logger.LogInfo($"\t\tFFmpeg not found at '{ReleaseConfig.FFMPEG_PATH}'. " +
                    $"Frames saved to: {framesDir}");
                RendererPlugin.Logger.LogInfo($"\t\tEncode manually with: ffmpeg -y -framerate {fps} " +
                    $"-i \"{inputPattern}\" -frames:v {frameCount} -c:v libvpx-vp9 " +
                    $"-pix_fmt yuva420p -crf 30 -b:v 0 -deadline good -cpu-used 4 " +
                    $"-auto-alt-ref 0 \"{outputPath}\"");
                yield break;
            }

            RendererPlugin.Logger.LogInfo($"\t\tEncoding to WebM: {outputPath}");

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ReleaseConfig.FFMPEG_PATH,
                // -deadline good -cpu-used 4: fast encoding (default is "best" which is extremely slow)
                // -crf 30 -b:v 0: constant quality mode (single pass, no target bitrate)
                // -auto-alt-ref 0: required for alpha transparency in VP9
                Arguments = $"-y -framerate {fps} -i \"{inputPattern}\" " +
                            $"-frames:v {frameCount} " +
                            $"-c:v libvpx-vp9 -pix_fmt yuva420p " +
                            $"-crf 30 -b:v 0 " +
                            $"-deadline good -cpu-used 4 " +
                            $"-auto-alt-ref 0 " +
                            $"\"{outputPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            var process = System.Diagnostics.Process.Start(psi);

            // Poll until FFmpeg finishes (non-blocking via coroutine yield)
            while (!process.HasExited)
                yield return new WaitForSecondsRealtime(0.2f);

            if (process.ExitCode != 0)
            {
                string error = process.StandardError.ReadToEnd();
                RendererPlugin.Logger.LogInfo($"\t\tFFmpeg error (exit {process.ExitCode}): {error}");
            }
            else
            {
                RendererPlugin.Logger.LogInfo($"\t\tEncoded successfully: {outputPath}");
                // Clean up frame directory
                try { Directory.Delete(framesDir, true); }
                catch (Exception e) { RendererPlugin.Logger.LogInfo($"\t\tFailed to clean up frames: {e.Message}"); }
            }
        }

        private void ApplyWhiteMaterial(GameObject targetObject)
        {
            if (_cachedWhiteMat == null)
            {
                Shader shader = Shader.Find("Unlit/Texture");
                if (shader == null)
                {
                    RendererPlugin.Logger.LogInfo("Unlit/Texture shader not found.");
                    return;
                }
                _cachedWhiteMat = new Material(shader);
                _cachedWhiteMat.SetTexture("_MainTex", Texture2D.whiteTexture);
            }
            foreach (Renderer renderer in targetObject.GetComponentsInChildren<Renderer>(true))
            {
                // Check if any of the materials have "shadow" in their name
                bool hasShadowMaterial = false;
                bool dontTouchMaterial = false;
                foreach (Material mat in renderer.materials)
                {
                    if (mat != null && (mat.name.Contains("Rune_Unholy_sm")
                        || mat.name.Contains("Rune_Blood_sm")
                        || mat.name.Contains("Rune_Frost_sm")
                        || mat.name.Contains("Hidden/TextOutline_Unlit")
                        || mat.name.Contains("Hidden/TextOutline_Unlit_NoVertColor")
                        || mat.name.Contains("Card_Inhand_Weapon_Drake_shadow")
                    ))
                    {
                        dontTouchMaterial = true;
                        break;
                    }
                    if (mat != null
                        && (mat.name.ToLower().Contains("shadow") || mat.name.Contains("R2TTransparent") 
                    ))
                    {
                        hasShadowMaterial = true;
                        break;
                    }
                }
                if (hasShadowMaterial)
                {
                    // Disable the entire renderer for shadow materials
                    // This prevents them from interfering with the alpha mask
                    renderer.enabled = false;
                }
                else if (dontTouchMaterial)
                {
                    // Do nothing - it won't be rendered as white, but as long as it's not black that's good enough
                }
                else
                {
                    // Apply white material to non-shadow renderers
                    int matCount = renderer.materials.Length;
                    Material[] whiteMaterials = new Material[matCount];
                    for (int i = 0; i < matCount; i++)
                    {
                        whiteMaterials[i] = _cachedWhiteMat;
                    }
                    renderer.materials = whiteMaterials;
                }
            }
        }

        private Texture2D ScaleTexture(Texture2D source, int targetWidth)
        {
            float aspect = (float)source.height / source.width;
            int targetHeight = Mathf.RoundToInt(targetWidth * aspect);

            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0);
            RenderTexture.active = rt;

            // Blit using default scale and offset (no distortion)
            Graphics.Blit(source, rt, new Vector2(1, 1), new Vector2(0, 0));

            // Read pixels into new texture
            Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            return result;
        }

        private void SavePNGAsync(string filePath, byte[] data)
        {
            Task.Run(() =>
            {
                try
                {
                    File.WriteAllBytes(filePath, data);
                    //Debug.Log($"Saved PNG async to: {filePath}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to save PNG to {filePath}: {ex}");
                }
            });
        }
    }
}
