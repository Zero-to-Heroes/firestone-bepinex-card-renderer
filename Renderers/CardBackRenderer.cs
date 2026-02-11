using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FirestoneCardsRenderer
{
    class CardBackRenderer : MonoBehaviour
    {
        private static string CARD_BACK_REF_FILE = "https://raw.githubusercontent.com/Zero-to-Heroes/hs-reference-data/master/src/card-backs.json";
        //private static string CARD_BACK_REF_FILE = "https://raw.githubusercontent.com/Zero-to-Heroes/hs-reference-data/master/src/card-backs-diff.json";

        private int m_cardBackId;
        private GameObject m_cardBackGo;
        private bool m_loadComplete;

        // ──────────────────────────────────────────────────────────────
        // Static screenshot capture (existing functionality)
        // ──────────────────────────────────────────────────────────────

        public IEnumerator BuildCardBackScreenshots()
        {
            using (WebClient wc = new WebClient())
            {
                RendererPlugin.Logger.LogInfo($"\tCalling json download");
                var json = wc.DownloadString(CARD_BACK_REF_FILE);
                RendererPlugin.Logger.LogInfo($"\tDownloaded json {json.Length}");
                var refCardBacks = JsonConvert.DeserializeObject<CardBack[]>(json)
                    //.Reverse<CardBack>()
                    .ToList();
                RendererPlugin.Logger.LogInfo($"\tParsed card backs {refCardBacks.Count}");
                foreach (var cardBack in refCardBacks)
                {
                    m_cardBackId = cardBack.id;
                    yield return StartCoroutine(BuildCardBackScreenshotCoroutine());
                }
            }

            yield return null;
        }

        private IEnumerator BuildCardBackScreenshotCoroutine()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            if (m_cardBackGo != null)
            {
                Destroy(m_cardBackGo);
            }
            yield return new WaitForSecondsRealtime(0.5f);
            RendererPlugin.Logger.LogInfo($"\tBuilding card back image for {m_cardBackId}");
            CardBackManager.Get().LoadCardBackByIndex(m_cardBackId, delegate (CardBackManager.LoadCardBackData cardBackData)
            {
                SetupCardBackGameObject(cardBackData);

                var dir = $"{ReleaseConfig.DESTINATION_ROOT_FOLDER}\\card_backs";
                StartCoroutine(GetComponent<ScreenshotHandler>().CaptureScreenshot(
                    targetObject: m_cardBackGo,
                    destFolder: dir,
                    fileName: $"{m_cardBackId}.png",
                    targetWidths: new int[] { 512, 256 },
                    width: ReleaseConfig.ScreeshotSizes.CardBackWidth,
                    height: ReleaseConfig.ScreeshotSizes.CardBackHeight,
                    startX: ReleaseConfig.ScreeshotSizes.CardBackStartX,
                    startY: ReleaseConfig.ScreeshotSizes.CardBackStartY
                ));
            }, true);
            RendererPlugin.Logger.LogInfo($"\tCard back loaded");
            yield return new WaitForSecondsRealtime(1f);
            if (m_cardBackGo != null)
            {
                Destroy(m_cardBackGo);
            }
            yield return null;
        }

        // ──────────────────────────────────────────────────────────────
        // Animated capture (looping WebM with cross-fade blending)
        // ──────────────────────────────────────────────────────────────

        public IEnumerator BuildCardBackAnimations()
        {
            using (WebClient wc = new WebClient())
            {
                RendererPlugin.Logger.LogInfo($"\tCalling json download for animated card backs");
                var json = wc.DownloadString(CARD_BACK_REF_FILE);
                RendererPlugin.Logger.LogInfo($"\tDownloaded json {json.Length}");
                var refCardBacks = JsonConvert.DeserializeObject<CardBack[]>(json)
                    .ToList();
                RendererPlugin.Logger.LogInfo($"\tParsed card backs {refCardBacks.Count}");
                foreach (var cardBack in refCardBacks)
                {
                    m_cardBackId = cardBack.id;
                    yield return StartCoroutine(BuildCardBackAnimationCoroutine());
                }
            }

            yield return null;
        }

        private IEnumerator BuildCardBackAnimationCoroutine()
        {
            var screenshotHandler = GetComponent<ScreenshotHandler>();
            int cbStartX = ReleaseConfig.ScreeshotSizes.CardBackStartX;
            int cbStartY = ReleaseConfig.ScreeshotSizes.CardBackStartY;
            int cbWidth = ReleaseConfig.ScreeshotSizes.CardBackWidth;
            int cbHeight = ReleaseConfig.ScreeshotSizes.CardBackHeight;

            // Clean up any previous card back
            yield return new WaitForSecondsRealtime(0.5f);
            if (m_cardBackGo != null)
            {
                Destroy(m_cardBackGo);
                m_cardBackGo = null;
            }
            yield return new WaitForSecondsRealtime(0.5f);

            RendererPlugin.Logger.LogInfo($"\tCapturing animated card back {m_cardBackId}");

            // ── Step 1: Load card back and capture alpha mask ──
            // This instance's materials will be destroyed by the white material pass.
            LoadCardBack(m_cardBackId);
            while (!m_loadComplete) yield return null;

            yield return StartCoroutine(screenshotHandler.CaptureAlphaMask(
                m_cardBackGo, cbStartX, cbStartY, cbWidth, cbHeight));
            Texture2D alphaMask = screenshotHandler.LastAlphaMask;

            // Destroy the alpha-pass instance (materials are now white)
            Destroy(m_cardBackGo);
            m_cardBackGo = null;
            yield return new WaitForSecondsRealtime(0.5f);

            // ── Step 2: Load a fresh card back for animation capture ──
            LoadCardBack(m_cardBackId);
            while (!m_loadComplete) yield return null;

            // Enable time so shader animations play
            Time.timeScale = 1f;
            Time.captureFramerate = ReleaseConfig.ANIMATION_FPS;

            // Warm up: let particle systems and shader animations reach steady state
            RendererPlugin.Logger.LogInfo($"\t\tWarming up animation ({ReleaseConfig.ANIMATION_WARMUP_FRAMES} frames)...");
            for (int i = 0; i < ReleaseConfig.ANIMATION_WARMUP_FRAMES; i++)
                yield return new WaitForEndOfFrame();

            // ── Step 3: Capture animation frames ──
            var dir = $"{ReleaseConfig.DESTINATION_ROOT_FOLDER}\\card_backs_animated";
            yield return StartCoroutine(screenshotHandler.CaptureAnimationFrames(
                destFolder: dir,
                fileName: $"{m_cardBackId}.webm",
                alphaMask: alphaMask,
                x: cbStartX,
                y: cbStartY,
                width: cbWidth,
                height: cbHeight,
                targetWidth: 512,
                fps: ReleaseConfig.ANIMATION_FPS,
                frameCount: ReleaseConfig.ANIMATION_FRAME_COUNT,
                overlapFrames: ReleaseConfig.ANIMATION_OVERLAP_FRAMES
            ));

            // Restore frozen time
            Time.captureFramerate = 0;
            Time.timeScale = 0f;

            // Cleanup
            if (alphaMask != null)
            {
                DestroyImmediate(alphaMask);
                screenshotHandler.LastAlphaMask = null;
            }
            if (m_cardBackGo != null)
            {
                Destroy(m_cardBackGo);
                m_cardBackGo = null;
            }

            RendererPlugin.Logger.LogInfo($"\tDone with animated card back {m_cardBackId}");
        }

        // ──────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Starts an async card back load. Sets m_loadComplete = true when done.
        /// The loaded game object is stored in m_cardBackGo.
        /// </summary>
        private void LoadCardBack(int cardBackId)
        {
            m_loadComplete = false;
            CardBackManager.Get().LoadCardBackByIndex(cardBackId, delegate (CardBackManager.LoadCardBackData cardBackData)
            {
                SetupCardBackGameObject(cardBackData);
                m_loadComplete = true;
            }, true);
        }

        /// <summary>
        /// Common setup for a loaded card back: configures actor, parents to root, positions and scales.
        /// </summary>
        private void SetupCardBackGameObject(CardBackManager.LoadCardBackData cardBackData)
        {
            GameObject gameObject = cardBackData.m_GameObject;
            RendererPlugin.Logger.LogInfo($"\t gameObject {gameObject}");
            gameObject.name = "CARD_BACK_" + cardBackData.m_CardBackIndex;
            Actor component = gameObject.GetComponent<Actor>();
            RendererPlugin.Logger.LogInfo($"\t component {component}");
            if (component != null)
            {
                GameObject cardMesh = component.m_cardMesh;
                RendererPlugin.Logger.LogInfo($"\t cardMesh {cardMesh}");
                component.SetCardbackUpdateIgnore(ignoreUpdate: true);
                component.SetUnlit();
                if (cardMesh != null)
                {
                    Material material = cardMesh.GetComponent<Renderer>().material;
                }
            }
            m_cardBackGo = gameObject;
            m_cardBackGo.transform.parent = GetComponent<RendererPlugin>().root;
            m_cardBackGo.transform.localPosition = new Vector3(0, 0, 10);
            m_cardBackGo.transform.localScale = new Vector3(18, 18, 18);
            m_cardBackGo.transform.localRotation = Quaternion.Euler(-90, 180, 0);
        }
    }

    [Serializable]
    class CardBack
    {
        public int id;
        public string name;
        public string description;
    }
}
