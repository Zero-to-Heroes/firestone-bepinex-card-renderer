using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace FirestoneCardsRenderer
{
    class PackRenderer : MonoBehaviour
    {
        private static string CARD_BACK_REF_FILE = "https://raw.githubusercontent.com/Zero-to-Heroes/hs-reference-data/master/src/enums/booster-type.ts";
        //private static string CARD_BACK_REF_FILE = "https://raw.githubusercontent.com/Zero-to-Heroes/hs-reference-data/master/src/booster-types-diff.json";

        private int m_packId;
        private GameObject m_packGo;
        private UnopenedPack m_unopenedPack;

        public IEnumerator BuildPackScreenshots()
        {
            using (WebClient wc = new WebClient())
            {
                RendererPlugin.Logger.LogInfo($"\tCalling json download");
                var enumAsString = wc.DownloadString(CARD_BACK_REF_FILE);
                var refPacks = BuildRefPacks(enumAsString)
                    .Where(id => ReleaseConfig.PACKS_TO_RENDER.Count == 0 || ReleaseConfig.PACKS_TO_RENDER.Contains(id))
                    .Reverse()
                    .ToList();
                RendererPlugin.Logger.LogInfo($"\tpack IDs {string.Join(", ", refPacks)}");
                //var refPacks = new List<int>() { 922, 894 };
                RendererPlugin.Logger.LogInfo($"\tParsed card packs {refPacks.Count}");
                foreach (var pack in refPacks)
                {
                    CleanUp();
                    m_packId = pack;
                    yield return StartCoroutine(BuildPackScreenshotCoroutine());
                }
            }

            yield return null;
        }

        private IEnumerator BuildPackScreenshotCoroutine()
        {
            if (m_packGo != null)
            {
                Destroy(m_packGo);
            }
            RendererPlugin.Logger.LogInfo($"\tBuilding card pack image for {m_packId}");
            BoosterDbfRecord record = GameDbf.Booster.GetRecord(m_packId);
            RendererPlugin.Logger.LogInfo($"\t\trecord {record} {record.ID} {record.PackOpeningPrefab}");
            var instantiated = AssetLoader.Get().InstantiatePrefab(record.PackOpeningPrefab, OnUnopenedPackLoaded, record, AssetLoadingOptions.IgnorePrefabPosition);
            if (!instantiated)
            {
                RendererPlugin.Logger.LogInfo($"\t\tERROR: could not instantiate pack info");
                yield break;
            }
            RendererPlugin.Logger.LogInfo($"\t\tCard pack loaded");

            yield return new WaitForSecondsRealtime(1f);
            if (m_packGo == null)
            {
                RendererPlugin.Logger.LogInfo($"\t\tERROR: could not load pack info");
                yield break;
            }

            RendererPlugin.Logger.LogInfo($"\t\tBuilding 512x screenshot {m_packGo.layer}");
            var dir = $"{ReleaseConfig.DESTINATION_ROOT_FOLDER}\\card_packs";
            yield return StartCoroutine(GetComponent<ScreenshotHandler>().CaptureScreenshot(
                targetObject: m_packGo,
                destFolder: dir,
                fileName: $"{m_packId}.png",
                targetWidths: new int[] { 512, 256 },
                width: ReleaseConfig.ScreeshotSizes.CardPackWidth,
                height: ReleaseConfig.ScreeshotSizes.CardPackHeight,
                startX: ReleaseConfig.ScreeshotSizes.CardPackStartX,
                startY: ReleaseConfig.ScreeshotSizes.CardPackStartY
            ));
            yield return new WaitForSecondsRealtime(1f);


            if (m_packGo != null)
            {
                Destroy(m_packGo);
            }
            RendererPlugin.Logger.LogInfo($"\t\tScreenshot taken");
            yield return null;
        }

        private void OnUnopenedPackLoaded(AssetReference assetRef, GameObject go, object callbackData)
        {
            try
            {
                RendererPlugin.Logger.LogInfo($"\t\tLoaded pack {go}");
                m_packGo = go;
                BoosterDbfRecord record = (BoosterDbfRecord)callbackData;
                m_unopenedPack = go.GetComponent<UnopenedPack>();
                m_unopenedPack.SetBoosterId(record.ID);
                m_unopenedPack.SetCount(1);
                go.SetActive(value: true);
                m_unopenedPack.gameObject.SetActive(value: true);

                m_unopenedPack.transform.parent = GetComponent<RendererPlugin>().root;
                m_unopenedPack.transform.localPosition = new Vector3(0, 0, 10);
                m_unopenedPack.transform.parent = GetComponent<RendererPlugin>().root;
                m_unopenedPack.transform.localScale = new Vector3(13, 13, 13);
                m_unopenedPack.transform.localRotation = Quaternion.Euler(-90, 180, 0);
                RemoveNamedElements(m_unopenedPack.transform, new string[] { "shadow", "Pack_Golden_Ribbon" });

                // Check if this is a catchup pack and apply freeze
                bool isCatchupPack = go.name.ToUpper().Contains("CUP") || go.name.ToUpper().Contains("WW2");
                if (isCatchupPack)
                {
                    RendererPlugin.Logger.LogInfo($"\t=== CATCHUP PACK DETECTED ===");
                    ForceWatermarksVisible(m_unopenedPack.transform);
                }
                else
                {
                    RendererPlugin.Logger.LogInfo($"\t(Regular pack - no changes needed)");
                }

                RendererPlugin.Logger.LogInfo($"\t--- Pack Setup Complete ---");
            }
            catch (Exception e)
            {
                RendererPlugin.Logger.LogInfo($"\t\tError while loading pack game object {e.Message} {e.StackTrace}");
            }
        }

        private void CleanUp()
        {
            if (m_packGo != null)
            {
                DestroyImmediate(m_packGo);
            }
            if (m_unopenedPack != null)
            {
                DestroyImmediate(m_unopenedPack);
            }
        }


        private void ForceWatermarksVisible(Transform packTransform)
        {
            try
            {
                // Apply watermark changes
                ForceWatermarkVisibilityRecursive(packTransform);

                RendererPlugin.Logger.LogInfo($"\t✅ Watermarks forced and pack frozen");
            }
            catch (Exception e)
            {
                RendererPlugin.Logger.LogInfo($"\t❌ Error: {e.Message}");
            }
        }

        private void ForceWatermarkVisibilityRecursive(Transform transform)
        {
            var renderers = transform.GetComponents<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.materials == null) continue;

                foreach (Material mat in renderer.materials)
                {
                    if (mat == null || mat.shader == null) continue;

                    // Look for materials that contain the catchup watermark texture
                    bool foundWatermark = false;
                    for (int propIndex = 0; propIndex < mat.shader.GetPropertyCount(); propIndex++)
                    {
                        string propName = mat.shader.GetPropertyName(propIndex);
                        var propType = mat.shader.GetPropertyType(propIndex);

                        if (propType == UnityEngine.Rendering.ShaderPropertyType.Texture)
                        {
                            var texture = mat.GetTexture(propName);
                            if (texture != null && texture.name.Contains("Pack_Modular_CUP"))
                            {
                                foundWatermark = true;
                                break;
                            }
                        }
                    }

                    if (foundWatermark)
                    {
                        // Process all shader properties to enhance watermark visibility
                        for (int propIndex = 0; propIndex < mat.shader.GetPropertyCount(); propIndex++)
                        {
                            string propName = mat.shader.GetPropertyName(propIndex);
                            var propType = mat.shader.GetPropertyType(propIndex);

                            if (propType == UnityEngine.Rendering.ShaderPropertyType.Color)
                            {
                                Color currentColor = mat.GetColor(propName);

                                // Force Layer1 (watermark) to full visibility
                                if (propName.Contains("Layer1") && propName.Contains("Mask"))
                                {
                                    Color forcedMask = new Color(1.0f, currentColor.g, currentColor.b, currentColor.a);
                                    mat.SetColor(propName, forcedMask);
                                }
                                // Disable Layer2 and Layer3 (animated effects)
                                else if ((propName.Contains("Layer2") || propName.Contains("Layer3")) && propName.Contains("Mask"))
                                {
                                    Color reducedMask = new Color(0.0f, 0.0f, 0.0f, 0.0f);
                                    mat.SetColor(propName, reducedMask);
                                }
                            }
                            else if (propType == UnityEngine.Rendering.ShaderPropertyType.Float || propType == UnityEngine.Rendering.ShaderPropertyType.Range)
                            {
                                // Enhance Layer1 properties
                                if (propName.Contains("Layer1") && (propName.Contains("Alpha") || propName.Contains("Blend") || propName.Contains("Intensity")))
                                {
                                    mat.SetFloat(propName, 1.0f);
                                }
                                // Disable Layer2 and Layer3 properties
                                else if ((propName.Contains("Layer2") || propName.Contains("Layer3")) && (propName.Contains("Alpha") || propName.Contains("Blend") || propName.Contains("Intensity")))
                                {
                                    mat.SetFloat(propName, 0.0f);
                                }
                            }
                        }
                    }
                }
            }

            // Recursively process all children
            foreach (Transform child in transform)
            {
                ForceWatermarkVisibilityRecursive(child);
            }
        }

        private void RemoveNamedElements(Transform transform, string[] names)
        {
            if (names.Select(name => name.ToLower()).Any(name => transform.name.ToLower().Contains(name)))
            {
                Destroy(transform.gameObject);
            }
            foreach (Transform t in transform.transform)
            {
                RemoveNamedElements(t, names);
            }
        }

        private List<int> BuildRefPacks(string enumAsString)
        {
            List<int> result = new List<int>();
            string pattern = @"\b\d+\b";
            MatchCollection matches = Regex.Matches(enumAsString, pattern);
            foreach (Match match in matches)
            {
                result.Add(int.Parse(match.Value));
            }
            return result;
        }
    }

    [Serializable]
    class Pack
    {
        public int id;
        public string name;
    }
}
