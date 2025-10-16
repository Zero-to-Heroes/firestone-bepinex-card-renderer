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
                m_cardBackGo.transform.parent = GetComponent<RendererPlugin>().root;
                m_cardBackGo.transform.localRotation = Quaternion.Euler(-90, 180, 0);


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
                //yield return new WaitForSecondsRealtime(1f);
            }, true);
            RendererPlugin.Logger.LogInfo($"\tCard back loaded");
            yield return new WaitForSecondsRealtime(1f);
            if (m_cardBackGo != null)
            {
                Destroy(m_cardBackGo);
            }
            yield return null;
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
