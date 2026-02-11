using BepInEx;
using BepInEx.Logging;
using UnityEngine.SceneManagement;
using UnityEngine;
using System.Linq;
using System;
using Blizzard.T5.Core.Utils;
using HarmonyLib;

namespace FirestoneCardsRenderer;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class RendererPlugin : BaseUnityPlugin
{
    public static RendererPlugin Instance { get; private set; }

    internal static new ManualLogSource Logger;

    public Transform root;
    private ScreenshotHandler screenshotHandler;

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Instance = this;

        Harmony.CreateAndPatchAll(typeof(DialogManagerPatcher));
        Harmony.CreateAndPatchAll(typeof(EntityDefPatcher));

        //var sceneRoots = SceneManager.GetActiveScene().GetRootGameObjects();
        //var parent = sceneRoots.First(r => r.name == "TheBox(Clone)");
        //var obj = new GameObject("RenderCardImage root");
        //obj.AddComponent<RenderCardImageController>();
        //obj.transform.SetParent(parent.transform);
    }

    private void Update()
    {
        if (root != null)
        {
            root.LookAt(Camera.main.transform);
        }

        if (Input.GetKeyDown(KeyCode.Delete))
        {
            Utils.CleanBaseScene();

            Vector3 pos = new Vector3(0, 50, 0);
            root = base.gameObject.transform;
            root.position = pos;

            Instance.screenshotHandler = Instance.gameObject.AddComponent<ScreenshotHandler>();

            Time.timeScale = 0f;
            return;
        }

        if (Input.GetKeyDown(KeyCode.F12))
        {
            var component = base.gameObject.AddComponent<CardRenderer>();
            StartCoroutine(component.BuildCardScreenshots());
        }
        if (Input.GetKeyDown(KeyCode.F11))
        {
            var component = base.gameObject.AddComponent<CardBackRenderer>();
            StartCoroutine(component.BuildCardBackScreenshots());
        }
        if (Input.GetKeyDown(KeyCode.F10))
        {
            var component = base.gameObject.AddComponent<PackRenderer>();
            StartCoroutine(component.BuildPackScreenshots());
        }
        if (Input.GetKeyDown(KeyCode.F9))
        {
            var component = base.gameObject.AddComponent<CardBackRenderer>();
            StartCoroutine(component.BuildCardBackAnimations());
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                Utils.CleanScene(root.transform, 1);
            }
        }

        if (Input.GetKeyDown(KeyCode.F1))
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                Utils.CrawlScene(root.transform, 1);
            }

            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            foreach (GameObject obj in allObjects)
            {
                Logger.LogInfo($"GameObject for {obj} with parent {obj.transform.parent}");
            }
        }
    }
}
