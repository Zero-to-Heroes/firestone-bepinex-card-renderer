using Blizzard.T5.Core.Utils;
using HarmonyLib;
using Hearthstone;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FirestoneCardsRenderer
{
    class CardRenderer : MonoBehaviour
    {
        // PATCH: Track how many cards have been rendered since last GC
        private int _cardsRenderedSinceLastGC = 0;

        public IEnumerator BuildCardScreenshots()
        {
            yield return new WaitForSecondsRealtime(2);
            var premiums = ReleaseConfig.PREMIUM_TAGS_TO_RENDER;
            var locales = ReleaseConfig.LOCALES;

            string json = null;
            if (ReleaseConfig.USE_LOCAL_CARDS)
            {
                string txtPath = Path.Combine(Environment.CurrentDirectory, "cards-diff-short.json");
                if (!File.Exists(txtPath))
                {
                    RendererPlugin.Logger.LogInfo($"Cards file not found: {txtPath}");
                    yield return null;
                }
                json = File.ReadAllText(txtPath);
                RendererPlugin.Logger.LogInfo($"Read local file");
            }

            int currentId = 0;
            int lastProcessedId = ReadLastProcessedId();
            RendererPlugin.Logger.LogInfo($"Read last processed id {lastProcessedId}");

            using (WebClient wc = new WebClient())
            {
                if (json == null)
                {
                    var refFile = ReleaseConfig.USE_CARDS_DIFF
                        ? "https://raw.githubusercontent.com/Zero-to-Heroes/hs-reference-data/master/src/cards-diff-short.json"
                        : ("https://static.firestoneapp.com/data/cards/cards_enUS.gz.json?v=" + ReleaseConfig.PATCH_NUMBER);
                    RendererPlugin.Logger.LogInfo($"Calling json download from {refFile}");
                    json = wc.DownloadString(refFile);
                }

                RendererPlugin.Logger.LogInfo($"Downloaded json {json?.Length}");
                try
                {
                    var refJson2 = JsonConvert.DeserializeObject<ReferenceCard[]>(json);
                    RendererPlugin.Logger.LogInfo($"Deserialized refJson2");
                }
                catch (Exception e)
                {
                    RendererPlugin.Logger.LogInfo($"Error deserialization {e}");
                }
                var refJson = JsonConvert.DeserializeObject<ReferenceCard[]>(json);
                RendererPlugin.Logger.LogInfo($"Deserialized json");
                var allCards = refJson
                    .OrderBy(o => o.id)
                    .Where(o => o.type != "Enchantment")
                    .Distinct()
                    .ToList();
                var referenceCards = allCards
                    .Where(c => c.set != "Lettuce")
                    .Where(o => ReleaseConfig.CARD_IDS_TO_CLEAR.Count == 0 || ReleaseConfig.CARD_IDS_TO_CLEAR.Contains(o.id))
                    .Where(c => ReleaseConfig.CARD_PREDICATES.All(p => p.Invoke(c)))
                    .ToList();
                RendererPlugin.Logger.LogInfo($"Parsed cards {referenceCards.Count} / {allCards.Count}");
                RendererPlugin.Logger.LogInfo($"Sample {JsonConvert.SerializeObject(refJson.ToList().Find(c => c.id == "WW_010"))}");

                if (referenceCards.Count == 0)
                {
                    yield return null;
                }

                foreach (var locale in locales)
                {
                    RendererPlugin.Logger.LogInfo($"Handling locale {locale}");
                    Localization.SetLocale(locale);
                    RendererPlugin.Logger.LogInfo($"Reloading game strings");
                    GameStrings.ReloadAll();
                    RendererPlugin.Logger.LogInfo($"Resetting fields in game dbf");
                    ResetFieldsInGameDbf();
                    RendererPlugin.Logger.LogInfo($"Reloading gameDBF");
                    yield return GameDbf.Load(false, false);

                    yield return new WaitForSecondsRealtime(2f);

                    foreach (var card in referenceCards)
                    {
                        RendererPlugin.Logger.LogInfo($"Handling card {referenceCards.IndexOf(card)}/{referenceCards.Count} -> {card.name}");
                        DefLoader defLoader = DefLoader.Get();
                        var entityDef = defLoader.GetEntityDef(card.id);
                        var isBg = card.set == "Battlegrounds" || card.techLevel > 0 || card.battlegroundsNormalDbfId > 0 || card.battlegroundsPremiumDbfId > 0;
                        var premiumsForCard = premiums.ToList();
                        if (isBg && card.battlegroundsNormalDbfId > 0)
                        {
                            premiumsForCard.Remove(TAG_PREMIUM.NORMAL);
                        }
                        else if (isBg && (card.battlegroundsPremiumDbfId > 0 || entityDef.IsHeroPower() || entityDef.IsBaconSpell() || entityDef.IsBattlegroundTrinket()))
                        {
                            premiumsForCard.Remove(TAG_PREMIUM.GOLDEN);
                        }
                        if (card.availableAsDiamond == null || !card.availableAsDiamond.Value)
                        {
                            premiumsForCard.Remove(TAG_PREMIUM.DIAMOND);
                        }
                        if (card.availableAsSignature == null || !card.availableAsSignature.Value)
                        {
                            premiumsForCard.Remove(TAG_PREMIUM.SIGNATURE);
                        }

                        var bgs = new List<bool>() { false };
                        if (isBg)
                        {
                            bgs.Add(true);
                            bgs.Remove(false);
                        }

                        var heroes = new List<bool>() { false };
                        var isHero = entityDef.IsHero() || entityDef.IsHeroSkin() || entityDef.IsLettuceMercenary();
                        if (isHero)
                        {
                            heroes.Add(true);
                        }

                        var mercsNoStats = new List<bool>() { false };
                        if (entityDef.IsLettuceMercenary())
                        {
                            mercsNoStats.Add(true);
                        }
                        foreach (var premium in premiumsForCard)
                        {
                            foreach (var bg in bgs)
                            {
                                foreach (var hero in heroes)
                                {
                                    foreach (var mercNoStat in mercsNoStats)
                                    {
                                        if (ReleaseConfig.USE_SAVE && currentId++ < lastProcessedId)
                                        {
                                            RendererPlugin.Logger.LogInfo($"\tCatching up {currentId} / {lastProcessedId}");
                                            continue;
                                        }

                                        RendererPlugin.Logger.LogInfo($"\tWill handle card {card.id} {premium} {bg} {hero}");
                                        yield return StartCoroutine(BuildInternalRender(locale, card.id, entityDef, bg, hero, premium, mercNoStat));
                                    }
                                }
                            }
                        }
                        UpdateLastProcessedId(currentId);
                    }
                    UpdateLastProcessedId(currentId);
                }
            }

            RendererPlugin.Logger.LogInfo($"Job's done");
            yield break;
        }

        private IEnumerator BuildInternalRender(Locale locale, string cardId, EntityDef entityDef, bool isBg, bool isHero, TAG_PREMIUM premium, bool mercNoStat)
        {
            var bgs = (isBg ? "bgs" : "full_cards");
            var hero = (isHero ? "heroes_" : "");
            var noStats = (mercNoStat ? "noStats_" : "");
            var golden = BuildGoldenPath(premium);
            var dir = $"{ReleaseConfig.DESTINATION_ROOT_FOLDER}\\{bgs}_{hero}{noStats}{locale.ToString()}";
            string fileName = $"{cardId}{golden}.png";

            if (isBg && !entityDef.IsHeroPower())
            {
                entityDef.SetTag(GAME_TAG.HIDE_COST, 1);
            }
            var actorName = isHero
                ? entityDef.IsLettuceMercenary()
                    ? ActorNames.GetNameWithPremiumType(ActorNames.ACTOR_ASSET.PLAY_MERCENARY, premium)
                    : ActorNames.GetHeroSkinOrHandActor(entityDef, premium)
                : ActorNames.GetHandActor(entityDef, premium);

            var completionSource = new TaskCompletionSource<bool>();

            AssetLoader.Get().InstantiatePrefab(
                actorName,
                (AssetReference assetRef, GameObject instance, object callbackData) =>
                {
                    StartCoroutine(OnActorLoaded(assetRef, instance, callbackData, entityDef, premium, isBg, isHero, mercNoStat, dir, fileName, completionSource));
                },
                null,
                AssetLoadingOptions.IgnorePrefabPosition
            );

            yield return new WaitUntil(() => completionSource.Task.IsCompleted);
        }

        private IEnumerator OnActorLoaded(AssetReference assetRef, GameObject cardGo, object callbackData,
            EntityDef entityDef, TAG_PREMIUM premium, bool isBg, bool isHero, bool mercNoStat, string dir, string fileName,
            TaskCompletionSource<bool> completionSource)
        {
            Actor actor = cardGo.GetComponent<Actor>();
            actor.TurnOffCollider();
            SetupActor(actor, entityDef, premium);
            // Actor.cs L4221
            LayerUtils.SetLayer(actor.gameObject, GameLayer.IgnoreFullScreenEffects);
            yield return StartCoroutine(UpdateActor(actor, isBg, isHero, mercNoStat, entityDef, cardGo));

            yield return StartCoroutine(GetComponent<ScreenshotHandler>().CaptureScreenshot(
                targetObject: cardGo,
                destFolder: dir,
                fileName: fileName,
                targetWidths: new int[] { 512, 256 },
                width: ReleaseConfig.ScreeshotSizes.CardWidth,
                height: isHero ? ReleaseConfig.ScreeshotSizes.CardHeroHeight : ReleaseConfig.ScreeshotSizes.CardHeight,
                startX: ReleaseConfig.ScreeshotSizes.CardStartX,
                startY: (int)(isHero
                    ? entityDef.IsLettuceMercenary()
                        ? 384
                        : ReleaseConfig.ScreeshotSizes.CardHeroStartY
                    : ReleaseConfig.ScreeshotSizes.CardStartY))
            );

            DestroyImmediate(cardGo);

            // PATCH: Force GC and unload unused assets every 50 cards
            _cardsRenderedSinceLastGC++;
            if (_cardsRenderedSinceLastGC >= 50)
            {
                _cardsRenderedSinceLastGC = 0;
                Resources.UnloadUnusedAssets();
                System.GC.Collect();
                RendererPlugin.Logger.LogInfo("Forced GC and Resources.UnloadUnusedAssets after 50 cards");
            }
            // END PATCH

            //yield return new WaitForSecondsRealtime(0.6f);

            completionSource.SetResult(true);
            yield break;
        }

        private void SetupActor(Actor actor, EntityDef entityDef, TAG_PREMIUM premium)
        {
            actor.SetEntityDef(entityDef);
            actor.SetPremium(premium);
            actor.UpdateAllComponents();
        }

        private IEnumerator UpdateActor(Actor actor, bool isBg, bool isHero, bool mercNoStat, EntityDef entityDef, GameObject cardGo)
        {
            actor.transform.localPosition = Vector3.zero;
            actor.transform.localRotation = Quaternion.identity;
            actor.transform.localScale = new Vector3(20, 20, 20);
            actor.transform.parent = GetComponent<RendererPlugin>().root;
            actor.ContactShadow(false);

            if (isBg)
            {
                if (actor.m_manaObject != null)
                {
                    actor.m_manaObject.SetActive(false);
                }
                if (entityDef.IsHeroPower())
                {
                    actor.ActivateSpellBirthState(SpellType.COIN_MANA_GEM);
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                else if (entityDef.IsMinion())
                {
                    Spell techLevelManaGemSpell = actor.GetSpell(SpellType.TECH_LEVEL_MANA_GEM);
                    if (techLevelManaGemSpell != null)
                    {
                        techLevelManaGemSpell.GetComponent<PlayMakerFSM>().FsmVariables.GetFsmInt("TechLevel").Value = actor.GetEntityDef().GetTechLevel();
                        techLevelManaGemSpell.ActivateState(SpellStateType.BIRTH);
                        RemoveLeftoverShadows(techLevelManaGemSpell.transform);
                        yield return new WaitForSecondsRealtime(0.5f);
                    }
                }
                else if (entityDef.IsBaconSpell())
                {
                    Spell techLevelManaGemSpell = actor.GetSpell(SpellType.TECH_LEVEL_MANA_GEM);
                    if (techLevelManaGemSpell != null)
                    {
                        techLevelManaGemSpell.GetComponent<PlayMakerFSM>().FsmVariables.GetFsmInt("TechLevel").Value = actor.GetEntityDef().GetTechLevel();
                        techLevelManaGemSpell.ActivateState(SpellStateType.BIRTH);
                        RemoveLeftoverShadows(techLevelManaGemSpell.transform);
                        yield return new WaitForSecondsRealtime(0.5f);
                    }
                    actor.m_costTextMesh.Text = Convert.ToString(actor.GetEntityDef().GetTag(GAME_TAG.COST));
                    actor.EnableAlternateCostTextPosition(true);
                    actor.UpdateManaGemOffset();
                    actor.ActivateSpellBirthState(SpellType.COIN_MANA_GEM_BACON_SPELL);
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                else if (entityDef.IsBattlegroundTrinket())
                {
                    actor.m_costTextMesh.Text = Convert.ToString(actor.GetEntityDef().GetTag(GAME_TAG.COST));
                    actor.ActivateSpellBirthState(SpellType.COIN_MANA_GEM);
                    yield return new WaitForSecondsRealtime(0.5f);
                }
            }
            if (isHero)
            {
                var skinComponent = actor.GetComponent<CollectionHeroSkin>();
                if (skinComponent != null)
                {
                    try
                    {
                        skinComponent.SetClass(entityDef);
                    }
                    catch (Exception e)
                    {
                        // This can happen when trying to render some hero skins, like BOM ones.
                        // I get an error like The given key was not present in the dictionary.
                        // at Blizzard.T5.Core.Map`2[TKey,TValue].get_Item (TKey key)
                        // at CollectionHeroSkin.SetClass
                        RendererPlugin.Logger.LogInfo($"\t\tError while processing OnActorUpdated.SetClass {e.Message} {e.StackTrace}");
                    }
                    skinComponent.ShowFavoriteBanner(false);
                    skinComponent.ShowName = true;
                    skinComponent.m_classIcon.transform.parent.gameObject.SetActive(false);
                }

                if (entityDef.IsLettuceMercenary())
                {
                    RemoveNamedElements(actor.transform, new string[] { "shadow", "AttackUberText", "HealthUberText" });
                }
            }
            if (mercNoStat)
            {
                RemoveNamedElements(cardGo.transform, new string[] { "AttackUberText", "HealthUberText", "LevelText" });
            }

            // Not sure why, but diamond hero skins at least don't render correctly if we call this
            if (!isHero)
            {
                actor.SetUnlit();
            }
            try
            {
                GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                foreach (GameObject obj in allObjects)
                {
                    if (obj.name.Contains("Shadow") || obj.name.Contains("Anomaly_Highlight"))
                    {
                        //RendererPlugin.Logger.LogInfo($"\t\tDestroying {obj}");
                        GameObjectUtils.SafeDestroy(obj);
                    }
                }
                RemoveNamedElements(cardGo.transform, new string[] { 
                    //"RuneBanner" ,
                    //"RuneLayouts",
                    //"OneRune",
                    "Hover_Highlight",
                    //"Text",
                });
            }
            catch (Exception e)
            {
                RendererPlugin.Logger.LogInfo($"\t\tError while processing OnActorUpdated {e.Message} {e.StackTrace}");
            }

            //RendererPlugin.Logger.LogInfo($"\t\tUpdateActor over");
            yield break;
        }

        private object BuildGoldenPath(TAG_PREMIUM m_premium)
        {
            switch (m_premium)
            {
                case TAG_PREMIUM.GOLDEN:
                    return "_golden";
                case TAG_PREMIUM.DIAMOND:
                    return "_diamond";
                case TAG_PREMIUM.SIGNATURE:
                    return "_signature";
                default:
                    return "";
            }
        }

        private void RemoveNamedElements(Transform transform, string[] names)
        {
            if (names.Select(name => name.ToLower()).Any(name => transform.name.ToLower().Contains(name)))
            {
                //RendererPlugin.Logger.LogInfo($"\t\tRemoving {transform.gameObject}");
                Destroy(transform.gameObject);
            }
            Component[] components = transform.GetComponents(typeof(Component));
            foreach (Transform t in transform.transform)
            {
                RemoveNamedElements(t, names);
            }
        }

        private void RemoveLeftoverShadows(Transform transform, bool allShadows = false)
        {
            //RendererPlugin.Logger.LogInfo("Components for " + transform + " " + allShadows);
            if (transform.name == "Bacon_TechLevel_Shield_shadow" || (allShadows && transform.name.ToLower().Contains("shadow")))
            {
                //RendererPlugin.Logger.LogInfo("\tDestroying " + transform);
                Destroy(transform.gameObject);
            }
            Component[] components = transform.GetComponents(typeof(Component));
            foreach (Component component in components)
            {
                //RendererPlugin.Logger.LogInfo(component.ToString());
            }
            foreach (Transform t in transform.transform)
            {
                RemoveLeftoverShadows(t, allShadows);
            }
        }

        private void CrawlScene(Transform transform, int indent = 0)
        {
            RendererPlugin.Logger.LogInfo(new string('\t', indent) + "Components for " + transform);
            Component[] components = transform.GetComponents(typeof(Component));
            foreach (Component component in components)
            {
                RendererPlugin.Logger.LogInfo(new string('\t', indent + 1) + component.ToString());
            }
            foreach (Transform t in transform.transform)
            {
                CrawlScene(t, indent + 1);
            }
        }

        private void UpdateLastProcessedId(int id)
        {
            if (!ReleaseConfig.USE_SAVE)
            {
                return;
            }
            string filePath = $"{ReleaseConfig.PATCH_NUMBER}.progress.txt";
            File.WriteAllText(filePath, id.ToString());
            RendererPlugin.Logger.LogInfo($"Updated last processed id {id}");
        }

        private int ReadLastProcessedId()
        {
            if (!ReleaseConfig.USE_SAVE)
            {
                return 0;
            }
            string filePath = $"{ReleaseConfig.PATCH_NUMBER}.progress.txt";
            if (File.Exists(filePath))
            {
                string value = File.ReadAllText(filePath);
                return int.Parse(value);
            }
            else
            {
                return 0;
            }
        }

        private void ResetFieldsInGameDbf()
        {
            AccessTools.Field(typeof(GameDbf), "AccountLicense").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "Achieve").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "AchieveCondition").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "AchieveRegionData").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "Achievement").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "AchievementCategory").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "AchievementSection").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "AchievementSectionItem").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "AchievementSubcategory").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "Adventure").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "AdventureData").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "AdventureDeck").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "AdventureGuestHeroes").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "AdventureHeroPower").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "AdventureLoadoutTreasures").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "AdventureMission").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "AdventureMode").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "Banner").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "BattlegroundsBoardSkin").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "BattlegroundsEmote").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "BattlegroundsFinisher").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "BattlegroundsGuideSkin").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "BattlegroundsHeroSkin").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "BattlegroundsSeason").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "Board").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "BonusBountyDropChance").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "Booster").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "BoosterCardSet").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "BoxProductBanner").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "BuildingTier").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "Card").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "CardAdditonalSearchTerms").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "CardBack").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "CardChange").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "CardDiscoverString").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "CardEquipmentAltText").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "CardHero").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "CardPlayerDeckOverride").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "CardRace").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "CardSet").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "CardSetSpellOverride").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "CardSetTiming").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "CardTag").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "CardValue").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "CharacterDialog").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "CharacterDialogItems").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "Class").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "ClassExclusions").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "ClientString").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "CosmeticCoin").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "CreditsYear").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "Deck").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "DeckCard").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "DeckRuleset").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "DeckRulesetRule").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "DeckRulesetRuleSubset").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "DeckTemplate").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "DetailsVideoCue").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "DkRuneList").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "DraftContent").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "ExternalUrl").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "FixedReward").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "FixedRewardAction").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "FixedRewardMap").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "Formula").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "FormulaChangePoint").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "GameMode").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "GameSaveSubkey").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "Global").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "GuestHero").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "GuestHeroSelectionRatio").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "HiddenLicense").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "InitCardValue").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "KeywordText").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "League").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LeagueBgPublicRatingEquiv").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LeagueGameType").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LeagueRank").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LettuceAbility").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LettuceAbilityTier").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LettuceBounty").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LettuceBountyFinalRespresentiveRewards").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LettuceBountyFinalRewards").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LettuceBountySet").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LettuceEquipment").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LettuceEquipmentModifierData").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LettuceEquipmentTier").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LettuceMapNodeType").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LettuceMapNodeTypeAnomaly").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LettuceMercenary").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LettuceMercenaryAbility").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LettuceMercenaryEquipment").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LettuceMercenaryLevel").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LettuceMercenaryLevelStats").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LettuceMercenarySpecialization").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LettuceTreasure").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LettuceTutorialVo").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LoginPopupSequence").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LoginPopupSequencePopup").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LoginReward").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LuckyDrawBox").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "LuckyDrawRewards").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "MercTriggeredEvent").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "MercTriggeringEvent").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "MercenariesRandomReward").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "MercenariesRankedSeasonRewardRank").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "MercenaryAllowedTreasure").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "MercenaryArtVariation").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "MercenaryArtVariationPremium").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "MercenaryBuilding").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "MercenaryVillageTrigger").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "MercenaryVisitor").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "MiniSet").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "ModifiedLettuceAbilityCardTag").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "ModifiedLettuceAbilityValue").SetValue(null, null);
            //AccessTools.Field(typeof(GameDbf), "MultiClassGroup").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "MythicAbilityScalingCardTag").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "MythicEquipmentScalingCardTag").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "MythicEquipmentScalingDestinationCardTag").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "NextTiers").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "PowerDefinition").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "Product").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "ProductClientData").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "PvpdrSeason").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "Quest").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "QuestDialog").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "QuestDialogOnComplete").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "QuestDialogOnProgress1").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "QuestDialogOnProgress2").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "QuestDialogOnReceived").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "QuestModifier").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "QuestPool").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "RegionOverrides").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "RepeatableTaskList").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "RewardBag").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "RewardChest").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "RewardChestContents").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "RewardItem").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "RewardList").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "RewardTrack").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "RewardTrackLevel").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "ScalingTreasureCardTag").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "Scenario").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "ScenarioGuestHeroes").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "ScheduledCharacterDialog").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "ScoreLabel").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "SellableDeck").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "ShopTier").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "ShopTierProductSale").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "Subset").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "SubsetCard").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "SubsetRule").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "TaskList").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "TavernBrawlTicket").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "TierProperties").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "Trigger").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "VisitorTask").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "VisitorTaskChain").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "Wing").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "XpOnPlacement").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "XpOnPlacementGameTypeMultiplier").SetValue(null, null);
            AccessTools.Field(typeof(GameDbf), "XpPerTimeGameTypeMultiplier").SetValue(null, null);
        }
    }

    internal class InternalCardRender
    {
        public GameObject cardGo;
        public Actor actor;
        public string dir;
        public string fileName;
        public bool isHero;
        public bool isMercenary;
    }
}

// Update the cards-diff-short generation script when modifying this
[Serializable]
public class ReferenceCard
{
    public string id;
    public string name;
    public string set;
    public string type;
    public string rarity;
    public int? cost;
    public int? attack;
    public int? health;
    public int techLevel;
    public int battlegroundsNormalDbfId;
    public int battlegroundsPremiumDbfId;
    public bool? availableAsDiamond;
    public bool? availableAsSignature;
    public AdditionalCosts additionalCosts;
}
[Serializable]
public class AdditionalCosts
{
    public int? BLOODRUNE;
    public int? UNHOLYRUNE;
    public int? FROSTRUNE;
}