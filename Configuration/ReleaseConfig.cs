using HutongGames.PlayMaker.Actions;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FirestoneCardsRenderer
{
    public class ReleaseConfig 
    {
        public static long PATCH_NUMBER = 646;

        // Cards config
        public static bool USE_CARDS_DIFF = false;
        public static bool OVERRIDE_EXISTING_FILES = true;
        public static bool USE_LOCAL_CARDS = false;
        public static bool USE_SAVE = false;

        public static string DESTINATION_ROOT_FOLDER = $"E:\\hearthstone_images\\{ReleaseConfig.PATCH_NUMBER}";

        public static List<Locale> LOCALES = new List<Locale>() {
            Locale.enUS,
            //Locale.frFR,
            //Locale.jaJP,
            //Locale.deDE,
            //Locale.zhCN,
            //Locale.zhTW,
            //Locale.ruRU,
            //Locale.itIT,
            //Locale.esES,
            //Locale.plPL,
            //Locale.ptBR,
            //Locale.thTH,
            //Locale.koKR,
            //Locale.esMX,
        };
        public static List<TAG_PREMIUM> PREMIUM_TAGS_TO_RENDER = new List<TAG_PREMIUM>() {
            TAG_PREMIUM.NORMAL,
            TAG_PREMIUM.GOLDEN,
            TAG_PREMIUM.DIAMOND,
            TAG_PREMIUM.SIGNATURE,
        };
        public static List<Predicate<ReferenceCard>> CARD_PREDICATES = new List<Predicate<ReferenceCard>>()
        {
            //(ReferenceCard card) => card.mechanics?.Contains("BACON_TIMEWARPED") ?? false
            //    && card.set != "Lettuce"
            //    && card.type != "Hero"
            //    && (card.type == "Location" 
            //        || (card.cost != null && card.cost >= 10)
            //        || (card.attack != null && card.attack >= 10)
            //        || (card.health != null && card.health >= 10)
            //        || (card.type == "Weapon" && card.rarity == "Legendary"))
        };
        public static List<string> CARD_IDS_TO_CLEAR = new List<string>()
        {
            "BG34_Treasure_902", "BG34_Treasure_903", "BGS_104"
        };

        public static List<int> PACKS_TO_RENDER = new List<int>()
        {
            1056, 1055, 1045, 1044, 989, 982
        };

        // Animation capture config
        public static string FFMPEG_PATH = @"C:\ffmpeg\bin\ffmpeg.exe";
        public static int ANIMATION_FPS = 30;
        public static int ANIMATION_FRAME_COUNT = 90;       // 3 seconds at 30fps
        public static int ANIMATION_OVERLAP_FRAMES = 15;    // 0.5 seconds cross-fade for seamless looping
        public static int ANIMATION_WARMUP_FRAMES = 30;     // 1 second warm-up for particles/shaders

        public static ScreeshotSizes ScreeshotSizes = new DynamicScreenshotSizes();

    }

    public interface ScreeshotSizes {
        public int CardWidth { get; set; }
        public int CardHeight { get; set; }
        public int CardHeroHeight { get; set; }
        public int CardStartX { get; set; }
        public int CardStartY { get; set; }
        public int CardHeroStartY { get; set; }
        public int CardBackWidth { get; set; }
        public int CardBackHeight { get; set; }
        public int CardBackStartX { get; set; }
        public int CardBackStartY { get; set; }
        public int CardPackWidth { get; set; }
        public int CardPackHeight { get; set; }
        public int CardPackStartX { get; set; }
        public int CardPackStartY { get; set; }
    }

    /// <summary>
    /// Dynamically calculates screenshot sizes based on current screen resolution.
    /// The game uses all available screen height and centers the game viewport width.
    /// Uses 1920x1080 as the reference resolution and scales proportionally.
    /// </summary>
    public class DynamicScreenshotSizes : ScreeshotSizes
    {
        // Reference resolution (1920x1080) - used as the base for scaling
        private const int REFERENCE_WIDTH = 1920;
        private const int REFERENCE_HEIGHT = 1080;
        
        // Reference values from FullScreen_1920_1080 configuration
        private const int REF_CARD_WIDTH = 510;
        private const int REF_CARD_HEIGHT = 670;
        private const int REF_CARD_START_X = 692;
        private const int REF_CARD_START_Y = 310;
        private const int REF_CARD_HERO_HEIGHT = 490;
        private const int REF_CARD_HERO_START_Y = 490;
        
        private const int REF_CARD_BACK_WIDTH = 510;
        private const int REF_CARD_BACK_HEIGHT = 670;
        private const int REF_CARD_BACK_START_X = 706;
        private const int REF_CARD_BACK_START_Y = 310;
        
        private const int REF_CARD_PACK_WIDTH = 478;
        private const int REF_CARD_PACK_HEIGHT = 610;
        private const int REF_CARD_PACK_START_X = 734;
        private const int REF_CARD_PACK_START_Y = 335;

        private float _scaleFactor = -1f;
        private int _gameViewportStartX = -1;

        private void CalculateScale()
        {
            if (_scaleFactor > 0) return; // Already calculated

            int screenWidth = UnityEngine.Screen.width;
            int screenHeight = UnityEngine.Screen.height;

            // The game uses all available screen height
            // Scale factor is based on height ratio
            _scaleFactor = (float)screenHeight / REFERENCE_HEIGHT;

            // The game viewport width is calculated based on the available height (maintaining aspect ratio)
            // Assuming the game maintains a 16:9 aspect ratio for the viewport
            float gameAspectRatio = (float)REFERENCE_WIDTH / REFERENCE_HEIGHT; // 16:9
            int gameViewportWidth = Mathf.RoundToInt(screenHeight * gameAspectRatio);

            // The game viewport is centered horizontally
            _gameViewportStartX = (screenWidth - gameViewportWidth) / 2;

            // Log for debugging
            RendererPlugin.Logger.LogInfo($"Dynamic screenshot sizes: Screen={screenWidth}x{screenHeight}, " +
                $"Scale={_scaleFactor:F3}, Viewport={gameViewportWidth}x{screenHeight}, ViewportStartX={_gameViewportStartX}");
        }

        private int ScaleValue(int referenceValue)
        {
            CalculateScale();
            return Mathf.RoundToInt(referenceValue * _scaleFactor);
        }

        private int ScaleX(int referenceX)
        {
            CalculateScale();
            // X coordinates are relative to the game viewport, which is centered
            // So we scale the reference X and add the viewport offset
            return _gameViewportStartX + Mathf.RoundToInt(referenceX * _scaleFactor);
        }

        public int CardWidth { get; set; }
        public int CardHeight { get; set; }
        public int CardHeroHeight { get; set; }
        public int CardStartX { get; set; }
        public int CardStartY { get; set; }
        public int CardHeroStartY { get; set; }
        public int CardBackWidth { get; set; }
        public int CardBackHeight { get; set; }
        public int CardBackStartX { get; set; }
        public int CardBackStartY { get; set; }
        public int CardPackWidth { get; set; }
        public int CardPackHeight { get; set; }
        public int CardPackStartX { get; set; }
        public int CardPackStartY { get; set; }

        public DynamicScreenshotSizes()
        {
            // Calculate all values dynamically
            CardWidth = ScaleValue(REF_CARD_WIDTH);
            CardHeight = ScaleValue(REF_CARD_HEIGHT);
            CardHeroHeight = ScaleValue(REF_CARD_HERO_HEIGHT);
            CardStartX = ScaleX(REF_CARD_START_X);
            CardStartY = ScaleValue(REF_CARD_START_Y);
            CardHeroStartY = ScaleValue(REF_CARD_HERO_START_Y);
            
            CardBackWidth = ScaleValue(REF_CARD_BACK_WIDTH);
            CardBackHeight = ScaleValue(REF_CARD_BACK_HEIGHT);
            CardBackStartX = ScaleX(REF_CARD_BACK_START_X);
            CardBackStartY = ScaleValue(REF_CARD_BACK_START_Y);
            
            CardPackWidth = ScaleValue(REF_CARD_PACK_WIDTH);
            CardPackHeight = ScaleValue(REF_CARD_PACK_HEIGHT);
            CardPackStartX = ScaleX(REF_CARD_PACK_START_X);
            CardPackStartY = ScaleValue(REF_CARD_PACK_START_Y);
        }
    }

    public class FullScreen_1920_1200 : ScreeshotSizes
    {
        private static int offsetX = 83;
        private static int offsetY = 0;
        private static int offsetWidth = 10;
        private static int offsetHeight = 20;

        public int CardWidth { get; set; } = 510;
        public int CardHeight { get; set; } = 603;
        public int CardStartX { get; set; } = 692;
        public int CardStartY { get; set; } = 340;
        // Not tested yet
        public int CardHeroHeight { get; set; } = 470 + offsetHeight;
        public int CardHeroStartY { get; set; } = 490 + offsetY;
        public int CardBackWidth { get; set; } = 500 + offsetWidth;
        public int CardBackHeight { get; set; } = 650 + offsetHeight;
        public int CardBackStartX { get; set; } = 631 + offsetX;
        public int CardBackStartY { get; set; } = 310 + offsetY;
        // Ok as well
        public int CardPackWidth { get; set; } = 468 + offsetWidth;
        public int CardPackHeight { get; set; } = 590 + offsetHeight;
        public int CardPackStartX { get; set; } = 659 + offsetX;
        public int CardPackStartY { get; set; } = 335 + offsetY;
    }

    public class FullScreen_1920_1080 : ScreeshotSizes
    {
        private static int offsetX = 75;
        private static int offsetY = 0;
        private static int offsetWidth = 10;
        private static int offsetHeight = 20;

        // Ok
        public int CardWidth { get; set; } = 510;
        public int CardHeight { get; set; } = 670;
        public int CardStartX { get; set; } = 692;
        public int CardStartY { get; set; } = 310;
        // Not tested yet
        public int CardHeroHeight { get; set; } = 470 + offsetHeight;
        public int CardHeroStartY { get; set; } = 490 + offsetY;
        public int CardBackWidth { get; set; } = 500 + offsetWidth;
        public int CardBackHeight { get; set; } = 650 + offsetHeight;
        public int CardBackStartX { get; set; } = 631 + offsetX;
        public int CardBackStartY { get; set; } = 310 + offsetY;
        // Ok as well
        public int CardPackWidth { get; set; } = 468 + offsetWidth;
        public int CardPackHeight { get; set; } = 590 + offsetHeight;
        public int CardPackStartX { get; set; } = 659 + offsetX;
        public int CardPackStartY { get; set; } = 335 + offsetY;
    }

    public class NoisyLaptopScreenshotSizes : ScreeshotSizes
    {
        public int CardWidth { get; set; } = 500;
        public int CardHeight { get; set; } = 650;
        public int CardHeroHeight { get; set; } = 470;
        public int CardStartX { get; set; } = 617;
        public int CardStartY { get; set; } = 310;
        public int CardHeroStartY { get; set; } = 490;
        public int CardBackWidth { get; set; } = 500;
        public int CardBackHeight { get; set; } = 650;
        public int CardBackStartX { get; set; } = 631;
        public int CardBackStartY { get; set; } = 310;
        public int CardPackWidth { get; set; } = 468;
        public int CardPackHeight { get; set; } = 590;
        public int CardPackStartX { get; set; } = 659;
        public int CardPackStartY { get; set; } = 335;
    }
}
