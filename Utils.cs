using Blizzard.T5.Core.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FirestoneCardsRenderer
{
    internal class Utils
    {
        public static void CleanBaseScene()
        {
            try
            {
                Scene scene = SceneManager.GetActiveScene();
                GameObject[] roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    RendererPlugin.Logger.LogInfo($"\tChild {root.name}");
                    if (root.name == "iTweenManager")
                    {
                        RendererPlugin.Logger.LogInfo($"\t\tDestroying {root.name}");
                        GameObjectUtils.SafeDestroy(root);
                    }
                    else if (root.name == "TheBox(Clone)")
                    {
                        Utils.CleanTheBox(root);
                    }
                }

                GameObject[] roots2 = scene.GetRootGameObjects();
                foreach (var t in roots2)
                {
                    RendererPlugin.Logger.LogInfo($"\tChild after clean {t.gameObject.name} with {t.name}");
                }
            }
            catch (Exception e)
            {
                RendererPlugin.Logger.LogInfo($"\tIssue while cleaning hub scene {e}");
            }
        }

        private static void CleanTheBox(GameObject root)
        {
            RendererPlugin.Logger.LogInfo($"\t\tCleaning: {root}");
            var childrenToKeep = new List<string>()
            {
                "TheBoxCamera",
                "RenderCardImage root"
            };
            var childrenCount = 0;
            while (childrenCount != root.transform.childCount)
            {
                childrenCount = root.transform.childCount;
                RendererPlugin.Logger.LogInfo($"\t\tChild tarnsforms: {root.transform.childCount}");
                foreach (Transform t2 in root.transform)
                {
                    RendererPlugin.Logger.LogInfo($"\t\tChild tarnsform {t2.gameObject.name}");
                    if (!childrenToKeep.Contains(t2.gameObject.name))
                    {
                        RendererPlugin.Logger.LogInfo($"\t\t\t\tDestroying {t2.gameObject.name}");
                        GameObjectUtils.SafeDestroy(t2.gameObject);
                    }
                }
            }
            foreach (Transform t2 in root.transform)
            {
                CleanTheBox(t2.gameObject);
            }
        }

        public static void CrawlScene(Transform transform, int indent = 0)
        {
            RendererPlugin.Logger.LogInfo(new string('\t', indent) + "Components for " + transform + $", isActive {transform.gameObject.activeInHierarchy}");
            Component[] components = transform.GetComponents(typeof(Component));
            foreach (Component component in components)
            {
                RendererPlugin.Logger.LogInfo(new string('\t', indent + 1) + component.ToString());
                LogTexturesForComponent(component, indent + 2);
            }
            foreach (Transform t in transform.transform)
            {
                CrawlScene(t, indent + 1);
            }
        }

        public static void CleanScene(Transform transform, int indent = 0)
        {
            RendererPlugin.Logger.LogInfo(new string('\t', indent) + "Components for " + transform + $", gameObject {transform.gameObject}");
            Component[] components = transform.GetComponents(typeof(Component));

            var names = new List<string>()
            {
                "Shadow",
                "Anomaly_Highlight",
                "Unique_Ally_Dragon",
                "FX_Dragon_Motes_Stars",
                "Card_Hand_Ally_Diamond",
                "FX"
            };
            //if (names.Any(name => transform.gameObject.name.Contains(name)) {
            //    DestroyImmediate(transform.gameObject);
            //}
            foreach (Component component in components)
            {
                if (component == null) continue;

                RendererPlugin.Logger.LogInfo(new string('\t', indent + 1) + component.ToString() + " type: " + component.GetType().Name);
                //if (component.gameObject.name.Contains("Shadow")
                //    || component.gameObject.name.Contains("Anomaly_Highlight")
                //    || component.gameObject.name.Contains("AllyInHandGhostCard_Diamond")
                //    || component.gameObject.name.Contains("Prismatic")
                //    || component.gameObject.name.Contains("FX_Ghost_Quad")
                //    || component.gameObject.name.Contains("Unique_Ally_Dragon")
                //    || component.gameObject.name.Contains("FX_Dragon_Motes_Stars")
                //    || component.GetType().Name.Contains("Shadow")
                //)
                //{
                //    Plugin.Logger.LogInfo(new string('\t', indent + 2) + "Disabling rendering for: " + component.gameObject.name);

                //    // Disable the entire GameObject to turn off all rendering and effects
                //    component.gameObject.SetActive(false);
                //    break; // No need to check other components on this GameObject
                //}

                // Also disable specific rendering components even if GameObject name doesn't match
                if (component is Renderer renderer)
                {
                    if (names.Any(name => component.GetType().Name.Contains(name) || renderer.material?.name?.Contains(name) == true))
                    {
                        RendererPlugin.Logger.LogInfo(new string('\t', indent + 2) + "Disabling renderer: " + component.GetType().Name);
                        renderer.enabled = false;
                    }
                }
                else if (component is ParticleSystem particleSystem)
                {
                    if (names.Any(name => component.GetType().Name.Contains(name) || component.gameObject.name?.Contains(name) == true))
                    {
                        RendererPlugin.Logger.LogInfo(new string('\t', indent + 2) + "Stopping particle system: " + component.GetType().Name);
                        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        particleSystem.gameObject.SetActive(false);
                    }
                }
            }
            foreach (Transform t in transform.transform)
            {
                CleanScene(t, indent + 1);
            }
        }

        private static void LogTexturesForComponent(Component component, int indent)
        {
            if (component == null) return;

            // Check for Renderer components (MeshRenderer, SpriteRenderer, etc.)
            if (component is Renderer renderer)
            {
                if (renderer.materials != null)
                {
                    for (int i = 0; i < renderer.materials.Length; i++)
                    {
                        Material material = renderer.materials[i];
                        if (material != null)
                        {
                            RendererPlugin.Logger.LogInfo(new string('\t', indent) + $"Material[{i}]: {material.name}");
                            LogTexturesFromMaterial(material, indent + 1);
                        }
                    }
                }
            }

            // Check for UI Image components
            if (component is UnityEngine.UI.Image image && image.sprite != null)
            {
                RendererPlugin.Logger.LogInfo(new string('\t', indent) + $"UI Image Sprite: {image.sprite.name}");
                if (image.sprite.texture != null)
                {
                    RendererPlugin.Logger.LogInfo(new string('\t', indent + 1) + $"Texture: {image.sprite.texture.name} ({image.sprite.texture.width}x{image.sprite.texture.height})");
                }
            }

            // Check for UI RawImage components
            if (component is UnityEngine.UI.RawImage rawImage && rawImage.texture != null)
            {
                RendererPlugin.Logger.LogInfo(new string('\t', indent) + $"UI RawImage Texture: {rawImage.texture.name} ({rawImage.texture.width}x{rawImage.texture.height})");
            }

            // Check for SpriteRenderer components
            if (component is SpriteRenderer spriteRenderer && spriteRenderer.sprite != null)
            {
                RendererPlugin.Logger.LogInfo(new string('\t', indent) + $"SpriteRenderer Sprite: {spriteRenderer.sprite.name}");
                if (spriteRenderer.sprite.texture != null)
                {
                    RendererPlugin.Logger.LogInfo(new string('\t', indent + 1) + $"Texture: {spriteRenderer.sprite.texture.name} ({spriteRenderer.sprite.texture.width}x{spriteRenderer.sprite.texture.height})");
                }
            }

            // Check for Camera components (for render textures)
            if (component is Camera camera && camera.targetTexture != null)
            {
                RendererPlugin.Logger.LogInfo(new string('\t', indent) + $"Camera Target Texture: {camera.targetTexture.name} ({camera.targetTexture.width}x{camera.targetTexture.height})");
            }
        }

        private static void LogTexturesFromMaterial(Material material, int indent)
        {
            if (material == null) return;

            // Common texture property names in Unity shaders
            string[] textureProperties = {
                "_MainTex", "_AlbedoTex", "_BaseMap", "_BaseColorMap",
                "_BumpMap", "_NormalMap", "_DetailNormalMap",
                "_MetallicGlossMap", "_OcclusionMap", "_ParallaxMap",
                "_DetailMask", "_DetailAlbedoMap", "_EmissionMap",
                "_SpecGlossMap", "_Cube", "_ReflectionTex"
            };

            foreach (string propName in textureProperties)
            {
                if (material.HasProperty(propName))
                {
                    Texture texture = material.GetTexture(propName);
                    if (texture != null)
                    {
                        RendererPlugin.Logger.LogInfo(new string('\t', indent) + $"{propName}: {texture.name} ({texture.width}x{texture.height})");
                    }
                }
            }

            // Also check all texture properties dynamically
            var shader = material.shader;
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                if (shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Texture)
                {
                    string propName = shader.GetPropertyName(i);
                    if (material.HasProperty(propName))
                    {
                        Texture texture = material.GetTexture(propName);
                        if (texture != null)
                        {
                            // Only log if we haven't already logged this property
                            bool alreadyLogged = false;
                            foreach (string knownProp in textureProperties)
                            {
                                if (knownProp == propName)
                                {
                                    alreadyLogged = true;
                                    break;
                                }
                            }
                            if (!alreadyLogged)
                            {
                                RendererPlugin.Logger.LogInfo(new string('\t', indent) + $"{propName}: {texture.name} ({texture.width}x{texture.height})");
                            }
                        }
                    }
                }
            }
        }

    }
}
