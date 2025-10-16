using HutongGames.PlayMaker.Actions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FirestoneCardsRenderer
{
    public class ReleaseConfig 
    {
        public static long PATCH_NUMBER = 229543;

        // Cards config
        public static bool USE_CARDS_DIFF = true;
        public static bool OVERRIDE_EXISTING_FILES = true;
        public static bool USE_LOCAL_CARDS = false;
        public static bool USE_SAVE = false;

        public static string DESTINATION_ROOT_FOLDER = $"D:\\hearthstone_images\\{ReleaseConfig.PATCH_NUMBER}";

        public static List<Locale> LOCALES = new List<Locale>() {
            Locale.enUS,
            Locale.frFR,
            Locale.jaJP,
            Locale.deDE,
            Locale.zhCN,
            Locale.zhTW,
            Locale.ruRU,
            Locale.itIT,
            Locale.esES,
            Locale.plPL,
            Locale.ptBR,
            Locale.thTH,
            Locale.koKR,
            Locale.esMX,
        };
        public static List<TAG_PREMIUM> PREMIUM_TAGS_TO_RENDER = new List<TAG_PREMIUM>() {
            TAG_PREMIUM.NORMAL,
            TAG_PREMIUM.GOLDEN,
            TAG_PREMIUM.DIAMOND,
            TAG_PREMIUM.SIGNATURE,
        };
        public static List<Predicate<ReferenceCard>> CARD_PREDICATES = new List<Predicate<ReferenceCard>>()
        {
            //(ReferenceCard card) => card.set != "Hero_skins"
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

        };

        public static List<int> PACKS_TO_RENDER = new List<int>()
        {
            //1033, 945
        };

        public static ScreeshotSizes ScreeshotSizes = new FullScreen_1920_1080();

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

    public class FullScreen_1920_1080 : ScreeshotSizes
    {
        private static int offsetX = 75;
        private static int offsetY = 0;
        private static int offsetWidth = 10;
        private static int offsetHeight = 20;

        public int CardWidth { get; set; } = 510; // 500 + offsetWidth;
        public int CardHeight { get; set; } = 670; // 650 + offsetHeight;
        public int CardStartX { get; set; } = 692; // 617 + offsetX;
        public int CardStartY { get; set; } = 310; // 310 + offsetY;
        public int CardHeroHeight { get; set; } = 470 + offsetHeight;
        public int CardHeroStartY { get; set; } = 490 + offsetY;
        public int CardBackWidth { get; set; } = 500 + offsetWidth;
        public int CardBackHeight { get; set; } = 650 + offsetHeight;
        public int CardBackStartX { get; set; } = 631 + offsetX;
        public int CardBackStartY { get; set; } = 310 + offsetY;
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
