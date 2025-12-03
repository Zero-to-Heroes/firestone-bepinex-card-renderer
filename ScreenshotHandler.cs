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
