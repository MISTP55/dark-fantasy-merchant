using System.Collections.Generic;
using System.IO;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>
    /// Generates the world map scene, the city marker, ship and ship route prefabs and the
    /// sample content. Safe to run again: existing assets are left untouched, and an
    /// existing scene only gains the ships, ship route, ship panel, world clock, time HUD,
    /// player treasury, treasury HUD, player notifications, notifications HUD, ship
    /// arrival notifier, player expenses, world economy, AI merchants and debug panel objects when
    /// it has none, each in its group at the root of the scene, and the references to them
    /// that are empty. Cities of the map that produce nothing are given the productions of
    /// the sample content.
    /// </summary>
    public static class WorldMapSetup
    {
        private const string ArtFolder = "Assets/Art/WorldMap";
        private const string CitiesFolder = "Assets/Data/Cities";
        private const string MapDataFolder = "Assets/Data/WorldMap";
        private const string PrefabFolder = "Assets/Prefabs/WorldMap";
        private const string UiFolder = "Assets/UI/WorldMap";

        private const string MapTexturePath = ArtFolder + "/WorldMap.jpg";
        private const string MarkerPrefabPath = PrefabFolder + "/CityMarker.prefab";
        private const string DefinitionPath = MapDataFolder + "/WorldMap.asset";
        internal const string PanelSettingsPath = UiFolder + "/WorldMapPanelSettings.asset";
        private const string PanelTemplatePath = UiFolder + "/CityInfoPanel.uxml";
        private const string ShipPanelTemplatePath = UiFolder + "/ShipInfoPanel.uxml";
        private const string ThemePath = "Assets/UI/DefaultRuntimeTheme.tss";
        internal const string ScenePath = "Assets/Scenes/WorldMap.unity";

        // The groups at the root of the scene, which hold all its objects.
        internal const string RenderingGroup = "Rendering";
        internal const string WorldGroup = "World";
        internal const string UiGroup = "UI";
        private const string SimulationGroup = "Simulation";
        private const string DebugGroup = "Debug";

        private const string CalendarFolder = "Assets/Data/Calendar";
        private const string CalendarPath = CalendarFolder + "/Calendar.asset";
        private const string TimeHudTemplatePath = UiFolder + "/TimeHud.uxml";

        private const string PlayerDataFolder = "Assets/Data/Player";
        private const string PlayerStartPath = PlayerDataFolder + "/PlayerStart.asset";
        private const string TreasuryHudTemplatePath = UiFolder + "/TreasuryHud.uxml";
        private const string NotificationsHudTemplatePath = UiFolder + "/NotificationsHud.uxml";

        private const string EconomyFolder = "Assets/Data/Economy";
        private const string ExpensesPath = EconomyFolder + "/Expenses.asset";
        private const string EconomyPath = EconomyFolder + "/Economy.asset";
        private const string BuildingsFolder = "Assets/Data/Buildings";
        private const string BuildingsPath = BuildingsFolder + "/Buildings.asset";
        private const string HousesFolder = BuildingsFolder + "/Houses";
        private const string WorkshopsFolder = BuildingsFolder + "/Workshops";
        private const string PlacementFolder = BuildingsFolder + "/Placement";
        private const string CityLayoutsFolder = "Assets/Data/CityLayouts";
        private const string RoadPath = BuildingsFolder + "/Road.asset";
        private const string TownHallPath = BuildingsFolder + "/TownHall.asset";
        private const string PortPath = BuildingsFolder + "/Port.asset";
        private const string RandomPlacementPath = PlacementFolder + "/RandomPlacement.asset";
        private const string RoadsidePlacementPath = PlacementFolder + "/RoadsidePlacement.asset";
        private static readonly Vector2Int HouseFootprint = new Vector2Int(2, 1);
        private static readonly Vector2Int WorkshopFootprint = new Vector2Int(2, 2);
        private static readonly Vector2Int RoadFootprint = new Vector2Int(1, 1);
        private static readonly Vector2Int TownHallFootprint = new Vector2Int(3, 2);
        private static readonly Vector2Int PortFootprint = new Vector2Int(4, 3);

        private const string GoodsFolder = "Assets/Data/Goods";
        private const string GoodIconsFolder = "Assets/Art/Goods/Icons";

        private const string AiFolder = "Assets/Data/AI";
        private const string AiMerchantsPath = AiFolder + "/AiMerchants.asset";

        private const string DebugUiFolder = "Assets/UI/Debug";
        private const string DebugPanelTemplatePath = DebugUiFolder + "/DebugPanel.uxml";
        private const float DebugSortingOrder = 100f;

        private const string ShipTexturePath = "Assets/Art/Ships/MerchantShip.png";
        private const string ShipSpritePrefix = "MerchantShip_";
        private const string ShipDataFolder = "Assets/Data/Ships";
        private const string ShipPrefabFolder = "Assets/Prefabs/Ships";
        private const string ShipDefinitionPath = ShipDataFolder + "/MerchantShip.asset";
        private const string ShipPrefabPath = ShipPrefabFolder + "/Ship.prefab";

        private const string ShipArtFolder = "Assets/Art/Ships";
        private const string RouteDashTexturePath = ShipArtFolder + "/ShipRouteDash.png";
        private const string RouteMarkerSpritePath = ShipArtFolder + "/ShipRouteDestination.png";
        private const string RouteSailedMaterialPath = ShipArtFolder + "/ShipRouteSailed.mat";
        private const string RouteRemainingMaterialPath = ShipArtFolder + "/ShipRouteRemaining.mat";
        private const string ShipRoutePrefabPath = ShipPrefabFolder + "/ShipRoute.prefab";
        internal const string UnlitSpriteShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";

        private const float MerchantShipSpeed = 1.5f;
        private const int MerchantShipCrewCapacity = 28;
        private const int MerchantShipCargoCapacity = 200;
        private const int ShipSortingOrder = 20;

        // Above the map, below the city markers.
        private const int RouteSortingOrder = 5;

        private const float MarkerPixelsPerUnit = 16f;

        private readonly struct SampleGood
        {
            public SampleGood(
                string assetName,
                string displayName,
                string workshopName,
                GoodCategory category,
                GoodUnit unit,
                double basePrice,
                PerClass<double> weeklyConsumptionPerThousand,
                PerClass<int> satisfactionWeights,
                int maxWorkers,
                double dailyOutputPerWorker,
                double merchantDailyOutputPerWorker,
                int aiWeight)
            {
                AssetName = assetName;
                DisplayName = displayName;
                WorkshopName = workshopName;
                Category = category;
                Unit = unit;
                BasePrice = basePrice;
                WeeklyConsumptionPerThousand = weeklyConsumptionPerThousand;
                SatisfactionWeights = satisfactionWeights;
                MaxWorkers = maxWorkers;
                DailyOutputPerWorker = dailyOutputPerWorker;
                MerchantDailyOutputPerWorker = merchantDailyOutputPerWorker;
                AiWeight = aiWeight;
            }

            public string AssetName { get; }
            public string DisplayName { get; }

            /// <summary>The name of the player's workshop of the good.</summary>
            public string WorkshopName { get; }
            public GoodCategory Category { get; }
            public GoodUnit Unit { get; }
            public double BasePrice { get; }
            public PerClass<double> WeeklyConsumptionPerThousand { get; }
            public PerClass<int> SatisfactionWeights { get; }
            public int MaxWorkers { get; }
            public double DailyOutputPerWorker { get; }
            public double MerchantDailyOutputPerWorker { get; }
            public int AiWeight { get; }
        }

        // The goods, in the order of the catalogue. Their name, the name of the player's workshop of
        // them, category, unit, base price, what 1 000 rich, wealthy and poor consume in a week and
        // how much the stock weighs in their satisfaction, then the workshop's maximum of workers, a
        // worker's daily output in the city's workshop and in a merchant's (without the city's
        // coefficient): the values a good that is missing is created with, which are then balanced
        // by hand in the asset.
        private static PerClass<double> Weekly(double rich, double wealthy, double poor) => new PerClass<double>(rich, wealthy, poor);

        private static PerClass<int> Weights(int rich, int wealthy, int poor) => new PerClass<int>(rich, wealthy, poor);

        private static readonly SampleGood[] SampleGoods =
        {
            new SampleGood("Grain", "Céréales", "Ferme", GoodCategory.Food, GoodUnit.Bushel, 110, Weekly(3.15, 4.2, 5.25), Weights(2, 4, 8), 68, 0.04333, 0.03333, 16),
            new SampleGood("Meat", "Viande", "Élevage", GoodCategory.Food, GoodUnit.Bushel, 957, Weekly(3.85, 3.045, 0.42), Weights(5, 4, 4), 60, 0.01333, 0.00833, 9),
            new SampleGood("Fish", "Poisson", "Pêcherie", GoodCategory.Food, GoodUnit.Bushel, 440, Weekly(1.4, 2.8, 3.5), Weights(2, 6, 6), 65, 0.03066, 0.03333, 9),
            new SampleGood("Beer", "Bière", "Brasserie", GoodCategory.Food, GoodUnit.Barrel, 34.8, Weekly(22.75, 45.5, 22.75), Weights(2, 6, 6), 68, 0.37591, 0.235, 9),
            new SampleGood("Salt", "Sel", "Saline", GoodCategory.RawMaterial, GoodUnit.Barrel, 28.5, Weekly(0.35, 0.35, 0.35), Weights(2, 2, 4), 54, 0.167, 0.167, 16),
            new SampleGood("Honey", "Miel", "Rucher", GoodCategory.RawMaterial, GoodUnit.Barrel, 110, Weekly(17.5, 8.75, 1.75), Weights(3, 2, 0), 60, 0.08, 0.06667, 9),
            new SampleGood("Spices", "Épices", "Jardin d'épices", GoodCategory.RawMaterial, GoodUnit.Barrel, 280, Weekly(1.4, 0.7, 0.7), Weights(3, 0, 0), 60, 0.02038, 0.02038, 9),
            new SampleGood("Wine", "Vin", "Vignoble", GoodCategory.RawMaterial, GoodUnit.Barrel, 220, Weekly(52.5, 13.3, 0), Weights(5, 2, 0), 93, 0.268, 0.067, 9),
            new SampleGood("Cloth", "Étoffe", "Tisserand", GoodCategory.RawMaterial, GoodUnit.Barrel, 206.8, Weekly(17.5, 12.25, 5.25), Weights(5, 4, 0), 60, 0.15, 0.1, 9),
            new SampleGood("Skins", "Peaux", "Trappeur", GoodCategory.RawMaterial, GoodUnit.Barrel, 676.5, Weekly(21, 10.5, 0), Weights(3, 2, 0), 72, 0.06667, 0.03333, 9),
            new SampleGood("Oil", "Huile", "Pressoir à huile", GoodCategory.RawMaterial, GoodUnit.Barrel, 82.5, Weekly(17.5, 12.25, 3.5), Weights(3, 4, 4), 60, 0.135, 0.135, 9),
            new SampleGood("Timber", "Bois", "Bûcheron", GoodCategory.RawMaterial, GoodUnit.Bushel, 55, Weekly(2.8, 2.8, 1.4), Weights(3, 4, 6), 48, 0.08, 0.06667, 11),
            new SampleGood("Metal", "Métal", "Forge", GoodCategory.RawMaterial, GoodUnit.Barrel, 255.6, Weekly(35, 26.25, 8.75), Weights(2, 2, 0), 78, 0.2, 0.1, 11),
            new SampleGood("Leather", "Cuir", "Tannerie", GoodCategory.RawMaterial, GoodUnit.Barrel, 224, Weekly(15.4, 12.25, 1.75), Weights(2, 2, 4), 60, 0.05332, 0.03333, 9),
            new SampleGood("Wool", "Laine", "Bergerie", GoodCategory.RawMaterial, GoodUnit.Bushel, 880, Weekly(0.35, 1.4, 0.7), Weights(2, 6, 4), 50, 0.02004, 0.0167, 11),
            new SampleGood("Pitch", "Brai", "Goudronnerie", GoodCategory.RawMaterial, GoodUnit.Barrel, 55.6, Weekly(0, 0, 0), Weights(0, 0, 0), 26, 0.067, 0.067, 8),
            new SampleGood("PigIron", "Fonte brute", "Fonderie", GoodCategory.RawMaterial, GoodUnit.Bushel, 880, Weekly(0, 0, 0), Weights(0, 0, 0), 70, 0.01667, 0.01667, 8),
            new SampleGood("Hemp", "Chanvre", "Chanvrière", GoodCategory.RawMaterial, GoodUnit.Bushel, 440, Weekly(0.175, 0.105, 0.07), Weights(0, 0, 0), 62, 0.00833, 0.00833, 9),
            new SampleGood("Pottery", "Poterie", "Atelier de potier", GoodCategory.RawMaterial, GoodUnit.Barrel, 171, Weekly(10.5, 6.3, 4.2), Weights(3, 2, 4), 58, 0.067, 0.067, 9),
            new SampleGood("Tiles", "Tuiles", "Tuilerie", GoodCategory.RawMaterial, GoodUnit.Bushel, 79.8, Weekly(0.035, 0.035, 0), Weights(0, 0, 0), 27, 0.067, 0.067, 8),
        };

        // The inputs of the city's workshop of a good, by asset name: the asset name of each input and
        // its units per unit made, in the units of each. A good not listed takes none.
        private static readonly Dictionary<string, (string input, double unitsPerUnit)[]> SampleInputs =
            new Dictionary<string, (string input, double unitsPerUnit)[]>
            {
                ["Beer"] = new[] { ("Grain", 0.025), ("Timber", 0.01) },
                ["Metal"] = new[] { ("Timber", 0.1), ("PigIron", 0.1) },
                ["Cloth"] = new[] { ("Wool", 0.1) },
                ["Salt"] = new[] { ("Timber", 0.08) },
                ["Pottery"] = new[] { ("Timber", 0.05) },
                ["Tiles"] = new[] { ("Timber", 0.5) },
                ["Pitch"] = new[] { ("Timber", 0.1) },
                ["Skins"] = new[] { ("Metal", 0.05), ("Hemp", 0.005) },
                ["Fish"] = new[] { ("Salt", 2.0), ("Hemp", 0.1) },
                ["Meat"] = new[] { ("Salt", 2.5) },
                ["Leather"] = new[] { ("Salt", 0.1), ("Timber", 0.02) },
            };

        private readonly struct SampleCity
        {
            public SampleCity(
                string assetName,
                string name,
                float x,
                float y,
                CityAccess access,
                CitySize size,
                string[] efficientGoods,
                string[] inefficientGoods,
                string description)
            {
                AssetName = assetName;
                Name = name;
                Position = new Vector2(x, y);
                Access = access;
                Size = size;
                EfficientGoods = efficientGoods;
                InefficientGoods = inefficientGoods;
                Description = description;
            }

            /// <summary>Name of the city's asset, in English like every asset name.</summary>
            public string AssetName { get; }

            /// <summary>Name the player reads, in French.</summary>
            public string Name { get; }

            public Vector2 Position { get; }
            public CityAccess Access { get; }
            public CitySize Size { get; }

            /// <summary>Asset names of the goods the city has a workshop for, at the full output.</summary>
            public string[] EfficientGoods { get; }

            /// <summary>Asset names of the goods the city has a workshop for, at the inefficient share of the output.</summary>
            public string[] InefficientGoods { get; }

            public string Description { get; }
        }

        // The 24 towns of the map, with the goods they produce there. A town's cattle makes
        // meat and leather, its whale oil comes with an inefficient fishery, and its hidden supply of
        // skins, wine or salt is an inefficient workshop. Each city stands on the navigable
        // cell nearest a town drawn on the placeholder image. The seven first keep the
        // places of the cities they replaced, so the player still starts where they did.
        private static readonly SampleCity[] SampleCities =
        {
            new SampleCity("Bruges", "Bruges", 0.125f, 0.627f, CityAccess.Coastal, CitySize.Town,
                new[] { "Salt", "Wool", "Hemp", "Pottery" }, new[] { "Grain", "Honey", "Wine", "Tiles" },
                "Une ville portuaire battue par les vents sur le cap occidental, première terre en vue des navires qui traversent la mer d'Aedean occidentale."),
            new SampleCity("Groningen", "Groningen", 0.177f, 0.604f, CityAccess.Coastal, CitySize.Village,
                new[] { "Grain", "Timber", "Hemp", "Tiles" }, new[] { "Salt", "Wine", "Pottery" },
                "Un village de pêcheurs abrité par les bois du sud, connu pour sa morue salée et ses contrebandiers discrets."),
            new SampleCity("Hamburg", "Hambourg", 0.387f, 0.551f, CityAccess.Coastal, CitySize.Town,
                new[] { "Grain", "Fish", "Beer", "Hemp" }, new[] { "Salt", "Timber", "Pottery" },
                "Un port marchand animé sur la mer d'Eamiq, où les barges fluviales rencontrent les navires de haute mer."),
            new SampleCity("Lubeck", "Lübeck", 0.463f, 0.577f, CityAccess.Coastal, CitySize.Village,
                new[] { "Fish", "Metal", "Pitch", "Tiles" }, new[] { "Grain", "Salt", "Wine", "Timber", "Hemp" },
                "Un village perché au-dessus d'une crique étroite, qui vend laine et pierre aux caboteurs de passage."),
            new SampleCity("Stettin", "Stettin", 0.559f, 0.489f, CityAccess.Coastal, CitySize.Village,
                new[] { "Grain", "Fish", "Beer", "Salt", "Hemp" }, new[] { "Timber" },
                "Un mouillage isolé à la pointe de la langue de sable méridionale, dernier abri avant le large de la mer de Rakmitag."),
            new SampleCity("Gdansk", "Gdansk", 0.584f, 0.626f, CityAccess.Coastal, CitySize.Capital,
                new[] { "Grain", "Meat", "Beer", "Leather", "Pitch", "Hemp" }, new[] { "Wool" },
                "La capitale impériale qui garde le détroit, dont les douanes taxent chaque navire en route vers la côte du désert."),
            new SampleCity("Cologne", "Cologne", 0.314f, 0.633f, CityAccess.River, CitySize.Capital,
                new[] { "Honey", "Wine", "Pottery" }, new[] { "Grain", "Meat", "Timber", "Leather", "Wool" },
                "Une capitale fluviale hérissée de clochers au cœur des Bois de la Couronne, que l'on ne rejoint qu'en barge."),
            new SampleCity("Edinburgh", "Édimbourg", 0.061f, 0.912f, CityAccess.Coastal, CitySize.Town,
                new[] { "Fish", "Cloth", "Metal" }, new[] { "Meat", "Timber", "Leather", "Wool", "PigIron" },
                "Une cité royale adossée aux Monts Wispsong, dont les quais ferment au nord la mer de Kharan."),
            new SampleCity("Scarborough", "Scarborough", 0.1f, 0.866f, CityAccess.Coastal, CitySize.Village,
                new[] { "Beer", "Cloth", "Timber", "Metal", "Wool" }, new[] { "Meat", "Leather", "Tiles" },
                "Un bourg de pêcheurs au bout d'une presqu'île de la mer de Kharan, où l'on fume le hareng sur les galets."),
            new SampleCity("London", "Londres", 0.123f, 0.791f, CityAccess.Coastal, CitySize.Capital,
                new[] { "Meat", "Beer", "Cloth", "Leather", "Wool", "PigIron" }, new[] { "Grain", "Wine" },
                "La capitale de l'Empire de Roshan, dont la forteresse noire domine les quais les plus actifs de la mer de Kharan."),
            new SampleCity("Bremen", "Brême", 0.294f, 0.666f, CityAccess.River, CitySize.Town,
                new[] { "Beer", "Cloth", "Metal", "Tiles" }, new[] { "Grain", "Wool", "PigIron" },
                "Une ville de bateliers sur le fleuve qui descend des Bois de la Couronne, où brasseurs et drapiers chargent les barges."),
            new SampleCity("Ripen", "Ripen", 0.159f, 0.474f, CityAccess.Coastal, CitySize.Town,
                new[] { "Salt", "Oil", "PigIron", "Pottery", "Tiles" }, new[] { "Grain", "Meat", "Fish", "Timber", "Leather" },
                "Une ville fortifiée au nord de la mer de Sudraqin, dont les salines et les fours à tuiles fument jour et nuit."),
            new SampleCity("Rostock", "Rostock", 0.431f, 0.448f, CityAccess.Coastal, CitySize.Town,
                new[] { "Grain", "Salt", "Honey", "Hemp", "Pottery" }, new[] { "Meat", "Leather", "Tiles" },
                "Une ville de meuniers et de potiers à l'embouchure d'une rivière, face aux mers d'Halvard."),
            new SampleCity("Bergen", "Bergen", 0.355f, 0.441f, CityAccess.Coastal, CitySize.Town,
                new[] { "Meat", "Oil", "Metal", "Leather", "Pitch" }, new[] { "Fish", "Skins", "Timber", "PigIron" },
                "Un comptoir fortifié à la pointe des forêts du sud, où l'on vient de loin pour le brai, le métal et l'huile de baleine."),
            new SampleCity("Oslo", "Oslo", 0.18f, 0.709f, CityAccess.Coastal, CitySize.Village,
                new[] { "Oil", "Timber", "Pitch", "PigIron", "Tiles" }, new[] { "Fish", "Skins" },
                "Un fort de pierre au bord du lac Malo, où les bûcherons des forêts du nord vendent leur bois et leur brai."),
            new SampleCity("Aalborg", "Aalborg", 0.193f, 0.516f, CityAccess.Coastal, CitySize.Village,
                new[] { "Meat", "Oil", "Timber", "Leather", "PigIron" }, new[] { "Fish", "Pottery", "Tiles" },
                "Un village au bord des marais du sud, dont les baleiniers rapportent l'huile des eaux froides."),
            new SampleCity("Malmo", "Malmö", 0.27f, 0.486f, CityAccess.Coastal, CitySize.Town,
                new[] { "Meat", "Cloth", "Leather", "Wool" }, new[] { "Honey", "Skins", "Timber", "PigIron", "Tiles" },
                "Un port d'éleveurs sur la mer de Sudraqin, où le bétail des plaines du sud est abattu et mis en barils."),
            new SampleCity("Stockholm", "Stockholm", 0.667f, 0.847f, CityAccess.Coastal, CitySize.Town,
                new[] { "Oil", "Timber", "Metal", "PigIron" }, new[] { "Meat", "Fish", "Leather", "Pottery" },
                "Une cité de mineurs bâtie sur un promontoire des mers inconnues, dont les forges fondent le minerai des montagnes voisines."),
            new SampleCity("Visby", "Visby", 0.72f, 0.838f, CityAccess.Coastal, CitySize.Village,
                new[] { "Honey", "Cloth", "Wool", "Pottery" }, new[] { "Grain", "Hemp", "Tiles" },
                "Un port de la côte nord, où tisserands et potiers vendent aux navires qui longent les mers inconnues."),
            new SampleCity("Thorn", "Thorn", 0.653f, 0.729f, CityAccess.Coastal, CitySize.Town,
                new[] { "Meat", "Honey", "Timber", "Leather", "Wool", "Pottery" }, new[] { "Grain", "Skins" },
                "Une ville d'artisans au fond de la baie, à l'abri des tempêtes, réputée pour son cuir, sa laine et son miel."),
            new SampleCity("Riga", "Riga", 0.62f, 0.485f, CityAccess.Coastal, CitySize.Town,
                new[] { "Fish", "Salt", "Honey", "Skins", "Pitch" }, new[] { "Timber" },
                "Un royaume côtier à la pointe du désert d'Alharia, où les caravanes déposent peaux, sel et miel sur la mer de Rakmitag."),
            new SampleCity("Reval", "Reval", 0.737f, 0.454f, CityAccess.Coastal, CitySize.Capital,
                new[] { "Grain", "Salt", "Skins", "Metal" }, new[] { "Meat", "Leather" },
                "Une cité du désert posée au bord de la mer de Rakmitag, dont les greniers nourrissent les tribus de l'intérieur."),
            new SampleCity("Ladoga", "Ladoga", 0.827f, 0.55f, CityAccess.River, CitySize.Village,
                new[] { "Grain", "Fish", "Skins", "PigIron", "Hemp" }, new[] { "Honey", "Timber", "Wool" },
                "Un village sur une rivière de l'est, où trappeurs et fondeurs descendent vendre leurs peaux et leur fonte."),
            new SampleCity("Novgorod", "Novgorod", 0.888f, 0.524f, CityAccess.River, CitySize.Capital,
                new[] { "Meat", "Beer", "Skins", "Timber", "Leather", "Pitch" }, new[] { "Honey", "PigIron", "Tiles" },
                "Une capitale de l'est lointain que l'on rejoint en remontant le fleuve, grand marché des peaux, du brai et du bétail."),
        };

        [MenuItem("Tools/Dark Fantasy Merchant/Build World Map Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("The world map setup cannot run in Play mode.");
                return;
            }

            // Read before anything is created: building a prefab instantiates a temporary
            // object in the open scene and marks it as modified.
            Scene openMapScene = SceneManager.GetSceneByPath(ScenePath);
            bool mapSceneHadUnsavedChanges = openMapScene.isLoaded && openMapScene.isDirty;

            EnsureFolders();

            Sprite mapSprite = ImportMapSprite();
            Sprite villageSprite = CreateMarkerSprite("CityMarkerVillage", 12);
            Sprite townSprite = CreateMarkerSprite("CityMarkerTown", 16);
            Sprite capitalSprite = CreateMarkerSprite("CityMarkerCapital", 20);

            CreateMarkerPrefab(villageSprite, townSprite, capitalSprite);
            CreateDefinition(mapSprite);
            CreatePanelSettings();

            ShipDefinition shipDefinition = CreateShipDefinition();
            CreateShipPrefab(shipDefinition);
            CreateShipRoutePrefab();
            CreateCalendar();
            CreatePlayerStart();
            CreateExpenses();
            CreateEconomy();
            CreateBuildings();
            FillCityEconomies();
            CreateCityLayouts();
            CreateAiMerchants(shipDefinition);

            AssetDatabase.SaveAssets();
            BuildScene();
            AddShipsToScene(mapSceneHadUnsavedChanges);
            AddTimeToScene(mapSceneHadUnsavedChanges);
            AddTreasuryToScene(mapSceneHadUnsavedChanges);

            // After the HUDs: in their group, the UI documents keep the order they are made in.
            AddShipPanelToScene(mapSceneHadUnsavedChanges);
            AddNotificationsToScene(mapSceneHadUnsavedChanges);
            AddExpensesToScene(mapSceneHadUnsavedChanges);
            AddEconomyToScene(mapSceneHadUnsavedChanges);
            AddAiMerchantsToScene(mapSceneHadUnsavedChanges);
            AddPlayerBuilderToScene(mapSceneHadUnsavedChanges);
            AddSimulationServicesToScene(mapSceneHadUnsavedChanges);
            AddDebugToolsToScene(mapSceneHadUnsavedChanges);
            CityViewSetup.Build();
            AddCityViewLoaderToScene(mapSceneHadUnsavedChanges);
            AssetDatabase.SaveAssets();

            Debug.Log("World map setup finished.");
        }

        private static void EnsureFolders()
        {
            foreach (string folder in new[]
            {
                ArtFolder, CitiesFolder, MapDataFolder, PrefabFolder, UiFolder,
                ShipArtFolder, ShipDataFolder, ShipPrefabFolder, CalendarFolder, PlayerDataFolder,
                EconomyFolder, GoodsFolder, BuildingsFolder, HousesFolder, WorkshopsFolder, PlacementFolder, CityLayoutsFolder, AiFolder,
                "Assets/Scenes",
            })
            {
                Directory.CreateDirectory(folder);
            }

            AssetDatabase.Refresh();
        }

        private static Sprite ImportMapSprite()
        {
            var importer = AssetImporter.GetAtPath(MapTexturePath) as TextureImporter;

            if (importer == null)
            {
                throw new FileNotFoundException("The placeholder map image is missing.", MapTexturePath);
            }

            if (importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(MapTexturePath);
        }

        // Placeholder marker: a light disc with a dark outline, light enough to be tinted.
        private static Sprite CreateMarkerSprite(string assetName, int sizePixels)
        {
            string path = $"{ArtFolder}/{assetName}.png";

            if (!File.Exists(path))
            {
                var fill = new Color(0.96f, 0.93f, 0.86f, 1f);
                var outline = new Color(0.12f, 0.08f, 0.06f, 1f);
                var texture = new Texture2D(sizePixels, sizePixels, TextureFormat.RGBA32, false);
                float center = (sizePixels - 1) * 0.5f;
                float outerRadius = sizePixels * 0.5f;
                float innerRadius = outerRadius - 2f;

                for (int y = 0; y < sizePixels; y++)
                {
                    for (int x = 0; x < sizePixels; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                        Color color = distance <= innerRadius ? fill : distance <= outerRadius ? outline : Color.clear;
                        texture.SetPixel(x, y, color);
                    }
                }

                SaveAsPixelSprite(texture, path);
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Writes the texture to a PNG imported as one pixel art sprite, and destroys it.</summary>
        private static void SaveAsPixelSprite(Texture2D texture, string path)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = MarkerPixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        private static CityMarkerView CreateMarkerPrefab(Sprite village, Sprite town, Sprite capital)
        {
            var existing = AssetDatabase.LoadAssetAtPath<CityMarkerView>(MarkerPrefabPath);

            if (existing != null)
            {
                return existing;
            }

            // ObjectFactory applies the render pipeline's default sprite material.
            GameObject instance = ObjectFactory.CreateGameObject(
                "CityMarker", typeof(SpriteRenderer), typeof(CityMarkerView));

            var spriteRenderer = instance.GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = village;
            spriteRenderer.sortingOrder = 10;

            var marker = instance.GetComponent<CityMarkerView>();
            SetReference(marker, "spriteRenderer", spriteRenderer);
            SetReference(marker, "villageSprite", village);
            SetReference(marker, "townSprite", town);
            SetReference(marker, "capitalSprite", capital);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, MarkerPrefabPath);
            Object.DestroyImmediate(instance);
            return prefab.GetComponent<CityMarkerView>();
        }

        private static WorldMapDefinition CreateDefinition(Sprite mapSprite)
        {
            var existing = AssetDatabase.LoadAssetAtPath<WorldMapDefinition>(DefinitionPath);

            if (existing != null)
            {
                return existing;
            }

            var definition = ScriptableObject.CreateInstance<WorldMapDefinition>();
            AssetDatabase.CreateAsset(definition, DefinitionPath);

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("mapSprite").objectReferenceValue = mapSprite;

            SerializedProperty cities = serialized.FindProperty("cities");
            cities.arraySize = SampleCities.Length;

            for (int i = 0; i < SampleCities.Length; i++)
            {
                SerializedProperty entry = cities.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("city").objectReferenceValue = CreateCity(SampleCities[i]);
                entry.FindPropertyRelative("active").boolValue = true;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static CityDefinition CreateCity(SampleCity sample)
        {
            string path = $"{CitiesFolder}/{sample.AssetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<CityDefinition>(path);

            if (existing != null)
            {
                return existing;
            }

            var city = ScriptableObject.CreateInstance<CityDefinition>();
            AssetDatabase.CreateAsset(city, path);

            var serialized = new SerializedObject(city);
            serialized.FindProperty("displayName").stringValue = sample.Name;
            serialized.FindProperty("description").stringValue = sample.Description;
            serialized.FindProperty("mapPosition").vector2Value = sample.Position;
            serialized.FindProperty("access").enumValueIndex = (int)sample.Access;
            serialized.FindProperty("size").enumValueIndex = (int)sample.Size;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return city;
        }

        private static PanelSettings CreatePanelSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);

            if (existing != null)
            {
                return existing;
            }

            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);

            if (theme == null)
            {
                throw new FileNotFoundException("The runtime UI theme is missing.", ThemePath);
            }

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.themeStyleSheet = theme;
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            return panelSettings;
        }

        // A new calendar definition is the game's calendar: its defaults are the content.
        private static CalendarDefinition CreateCalendar()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CalendarDefinition>(CalendarPath);

            if (existing != null)
            {
                return existing;
            }

            var calendar = ScriptableObject.CreateInstance<CalendarDefinition>();
            AssetDatabase.CreateAsset(calendar, CalendarPath);
            return calendar;
        }

        // Like the calendar, a new player start is the game's: its defaults are the content.
        private static PlayerStartDefinition CreatePlayerStart()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PlayerStartDefinition>(PlayerStartPath);

            if (existing != null)
            {
                return existing;
            }

            var playerStart = ScriptableObject.CreateInstance<PlayerStartDefinition>();
            AssetDatabase.CreateAsset(playerStart, PlayerStartPath);
            return playerStart;
        }

        // Like the calendar, new expenses are the game's: their defaults are the content.
        private static ExpensesDefinition CreateExpenses()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ExpensesDefinition>(ExpensesPath);

            if (existing != null)
            {
                return existing;
            }

            var expenses = ScriptableObject.CreateInstance<ExpensesDefinition>();
            AssetDatabase.CreateAsset(expenses, ExpensesPath);
            return expenses;
        }

        // Like the expenses, new AI merchants are the game's: their defaults are the content.
        private static AiMerchantsDefinition CreateAiMerchants(ShipDefinition shipDefinition)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AiMerchantsDefinition>(AiMerchantsPath);

            if (existing != null)
            {
                return existing;
            }

            // Filled in before the asset is created, so the file is written complete.
            var merchants = ScriptableObject.CreateInstance<AiMerchantsDefinition>();
            var serialized = new SerializedObject(merchants);
            serialized.FindProperty("shipDefinition").objectReferenceValue = shipDefinition;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(merchants, AiMerchantsPath);
            return merchants;
        }

        // The goods are created even when the economy exists, so that one deleted by mistake comes back.
        private static EconomyDefinition CreateEconomy()
        {
            var goods = new GoodDefinition[SampleGoods.Length];
            var createdGoods = new List<GoodDefinition>();

            for (int i = 0; i < SampleGoods.Length; i++)
            {
                goods[i] = CreateGood(SampleGoods[i], out bool isNew);

                if (isNew)
                {
                    createdGoods.Add(goods[i]);
                }
            }

            // Once every good exists, since a recipe names others. A good that existed is the user's.
            foreach (GoodDefinition good in createdGoods)
            {
                SetSampleInputs(good);
            }

            var existing = AssetDatabase.LoadAssetAtPath<EconomyDefinition>(EconomyPath);

            if (existing != null)
            {
                return existing;
            }

            // Filled in before the asset is created, so the file is written complete. The
            // settings keep their defaults: like the calendar's, they are the game's.
            var economy = ScriptableObject.CreateInstance<EconomyDefinition>();
            var serialized = new SerializedObject(economy);
            SerializedProperty catalogue = serialized.FindProperty("goods");
            catalogue.arraySize = goods.Length;

            for (int i = 0; i < goods.Length; i++)
            {
                SerializedProperty entry = catalogue.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("good").objectReferenceValue = goods[i];
                entry.FindPropertyRelative("active").boolValue = true;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(economy, EconomyPath);
            return economy;
        }

        /// <summary>
        /// Creates the buildings of the player when missing: the catalogue, a house per class and a
        /// workshop per sample good, whose materials are the timber and the metal of the goods,
        /// then completes the catalogue (<see cref="CompleteBuildings"/>). Like the expenses, new
        /// buildings are the game's: their defaults are the content. The goods are made by
        /// <see cref="CreateEconomy"/>, called first.
        /// </summary>
        public static BuildingsDefinition CreateBuildings()
        {
            var timber = AssetDatabase.LoadAssetAtPath<GoodDefinition>(GoodPath("Timber"));
            var metal = AssetDatabase.LoadAssetAtPath<GoodDefinition>(GoodPath("Metal"));

            var tiles = AssetDatabase.LoadAssetAtPath<GoodDefinition>(GoodPath("Tiles"));

            if (timber == null || metal == null || tiles == null)
            {
                throw new FileNotFoundException("The timber, the metal or the tiles could not be loaded.", GoodsFolder);
            }

            var buildings = AssetDatabase.LoadAssetAtPath<BuildingsDefinition>(BuildingsPath);
            bool isNew = buildings == null;

            if (isNew)
            {
                buildings = ScriptableObject.CreateInstance<BuildingsDefinition>();
                AssetDatabase.CreateAsset(buildings, BuildingsPath);
            }

            var goods = new List<GoodDefinition>();
            var samples = new Dictionary<GoodDefinition, SampleGood>();

            foreach (SampleGood sample in SampleGoods)
            {
                var good = AssetDatabase.LoadAssetAtPath<GoodDefinition>(GoodPath(sample.AssetName));

                if (good != null)
                {
                    goods.Add(good);
                    samples[good] = sample;
                }
            }

            CompleteBuildings(
                buildings, isNew, goods,
                housedClass => LoadOrCreateHouse(housedClass, timber, metal, tiles),
                good => LoadOrCreateWorkshop(samples[good], good, timber, metal),
                LoadOrCreateRoad,
                LoadOrCreateTownHall,
                LoadOrCreatePort);
            CompletePlacement(buildings);
            EditorUtility.SetDirty(buildings);
            return buildings;
        }

        /// <summary>
        /// Gives the catalogue what it lacks: its empty entries (a deleted definition) go, then a
        /// class without a house, or a good without a workshop, gets the one <paramref name="houseOf"/>
        /// or <paramref name="workshopOf"/> gives, when that one was just created or the catalogue
        /// itself is new. A definition that exists and was taken out of the catalogue by hand stays
        /// out, and one the catalogue holds is never looked for, whatever its asset is called. The road and
        /// the town hall are single fields: each is filled when null.
        /// </summary>
        /// <param name="houseOf">The house of a class, and whether it was just created; null when there is none.</param>
        /// <param name="workshopOf">The workshop of a good, and whether it was just created; null when there is none.</param>
        /// <param name="roadOf">The road, made when missing.</param>
        /// <param name="townHallOf">The town hall, made when missing.</param>
        /// <param name="portOf">The port, made when missing.</param>
        public static void CompleteBuildings(
            BuildingsDefinition buildings,
            bool isNew,
            IEnumerable<GoodDefinition> goods,
            System.Func<SocialClass, (HouseDefinition house, bool created)> houseOf,
            System.Func<GoodDefinition, (WorkshopDefinition workshop, bool created)> workshopOf,
            System.Func<RoadDefinition> roadOf,
            System.Func<TownHallDefinition> townHallOf,
            System.Func<PortDefinition> portOf)
        {
            var catalogue = new SerializedObject(buildings);
            SerializedProperty houses = catalogue.FindProperty("houses");
            SerializedProperty workshops = catalogue.FindProperty("workshops");
            RemoveEmptyEntries(houses);
            RemoveEmptyEntries(workshops);
            catalogue.ApplyModifiedPropertiesWithoutUndo();

            foreach (SocialClass housedClass in new[] { SocialClass.Rich, SocialClass.Wealthy, SocialClass.Poor })
            {
                if (buildings.HouseOf(housedClass) == null)
                {
                    (HouseDefinition house, bool created) = houseOf(housedClass);
                    AddIf(houses, house, created || isNew);
                }
            }

            foreach (GoodDefinition good in goods)
            {
                if (buildings.WorkshopOf(good) == null)
                {
                    (WorkshopDefinition workshop, bool created) = workshopOf(good);
                    AddIf(workshops, workshop, created || isNew);
                }
            }

            SerializedProperty road = catalogue.FindProperty("road");
            SerializedProperty townHall = catalogue.FindProperty("townHall");
            SerializedProperty port = catalogue.FindProperty("port");

            if (road.objectReferenceValue == null)
            {
                road.objectReferenceValue = roadOf();
            }

            if (townHall.objectReferenceValue == null)
            {
                townHall.objectReferenceValue = townHallOf();
            }

            if (port.objectReferenceValue == null)
            {
                port.objectReferenceValue = portOf();
            }

            catalogue.ApplyModifiedPropertiesWithoutUndo();
            CompleteFootprints(buildings.Houses, HouseFootprint);
            CompleteFootprints(buildings.Workshops, WorkshopFootprint);
            CompleteFootprints(new BuildingDefinition[] { buildings.Road }, RoadFootprint);
            CompleteFootprints(new BuildingDefinition[] { buildings.TownHall }, TownHallFootprint);
            CompleteFootprints(new BuildingDefinition[] { buildings.Port }, PortFootprint);
        }

        /// <summary>
        /// Gives the catalogue the roadside placement strategy, made when missing, when it has none or the random
        /// one, which stays an asset but is no longer used.
        /// </summary>
        public static void CompletePlacement(BuildingsDefinition buildings)
        {
            var catalogue = new SerializedObject(buildings);
            SerializedProperty strategy = catalogue.FindProperty("placementStrategy");

            if (strategy.objectReferenceValue != null && !(strategy.objectReferenceValue is RandomPlacementDefinition))
            {
                return;
            }

            var roadside = AssetDatabase.LoadAssetAtPath<RoadsidePlacementDefinition>(RoadsidePlacementPath);

            if (roadside == null)
            {
                Directory.CreateDirectory(PlacementFolder);
                roadside = ScriptableObject.CreateInstance<RoadsidePlacementDefinition>();
                AssetDatabase.CreateAsset(roadside, RoadsidePlacementPath);
            }

            strategy.objectReferenceValue = roadside;
            catalogue.ApplyModifiedPropertiesWithoutUndo();
        }

        // Only a footprint nobody set (0 by 0) is filled: a footprint tuned by hand stays.
        private static void CompleteFootprints(IEnumerable<BuildingDefinition> definitions, Vector2Int footprint)
        {
            foreach (BuildingDefinition definition in definitions)
            {
                if (definition != null && definition.FootprintSize == Vector2Int.zero)
                {
                    SetFootprint(definition, footprint);
                }
            }
        }

        private static void SetFootprint(BuildingDefinition definition, Vector2Int footprint)
        {
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("footprint").vector2IntValue = footprint;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static void RemoveEmptyEntries(SerializedProperty list)
        {
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == null)
                {
                    // Cleared first: deleting an element that still holds a reference, even a missing one, only clears it.
                    list.GetArrayElementAtIndex(i).objectReferenceValue = null;
                    list.DeleteArrayElementAtIndex(i);
                }
            }
        }

        private static void AddIf(SerializedProperty list, UnityEngine.Object definition, bool add)
        {
            if (definition != null && add)
            {
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = definition;
            }
        }

        private static (HouseDefinition house, bool created) LoadOrCreateHouse(
            SocialClass housedClass, GoodDefinition timber, GoodDefinition metal, GoodDefinition tiles)
        {
            switch (housedClass)
            {
                case SocialClass.Rich:
                    return LoadOrCreateHouse("RichHouse", "Maison de riches", housedClass, 40, 25000, timber, 25, metal, 30, tiles, 50);
                case SocialClass.Wealthy:
                    return LoadOrCreateHouse("WealthyHouse", "Maison d'aisés", housedClass, 70, 10000, timber, 15, metal, 20, tiles, 50);
                case SocialClass.Poor:
                    return LoadOrCreateHouse("PoorHouse", "Maison de pauvres", housedClass, 140, 5000, timber, 10, metal, 10, tiles, 40);
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(housedClass));
            }
        }

        private static (HouseDefinition house, bool created) LoadOrCreateHouse(
            string assetName, string displayName, SocialClass housedClass, int room,
            long gold, GoodDefinition timber, int timberUnits, GoodDefinition metal, int metalUnits,
            GoodDefinition tiles, int tilesUnits)
        {
            string path = $"{HousesFolder}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<HouseDefinition>(path);

            if (existing != null)
            {
                return (existing, false);
            }

            // Filled in before the asset is created, so the file is written complete.
            var house = ScriptableObject.CreateInstance<HouseDefinition>();
            var serialized = new SerializedObject(house);
            serialized.FindProperty("socialClass").enumValueIndex = (int)housedClass;
            serialized.FindProperty("room").intValue = room;
            serialized.FindProperty("footprint").vector2IntValue = HouseFootprint;
            FillBuilding(serialized, displayName, gold, (timber, timberUnits), (metal, metalUnits), (tiles, tilesUnits));
            AssetDatabase.CreateAsset(house, path);
            return (house, true);
        }

        private static RoadDefinition LoadOrCreateRoad()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RoadDefinition>(RoadPath);

            if (existing != null)
            {
                return existing;
            }

            var road = ScriptableObject.CreateInstance<RoadDefinition>();
            var serialized = new SerializedObject(road);
            serialized.FindProperty("footprint").vector2IntValue = RoadFootprint;
            FillBuilding(serialized, "Route", 50);
            AssetDatabase.CreateAsset(road, RoadPath);
            return road;
        }

        private static TownHallDefinition LoadOrCreateTownHall()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TownHallDefinition>(TownHallPath);

            if (existing != null)
            {
                return existing;
            }

            var townHall = ScriptableObject.CreateInstance<TownHallDefinition>();
            var serialized = new SerializedObject(townHall);
            serialized.FindProperty("footprint").vector2IntValue = TownHallFootprint;
            FillBuilding(serialized, "Hôtel de ville", 0);
            AssetDatabase.CreateAsset(townHall, TownHallPath);
            return townHall;
        }

        private static PortDefinition LoadOrCreatePort()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PortDefinition>(PortPath);

            if (existing != null)
            {
                return existing;
            }

            var port = ScriptableObject.CreateInstance<PortDefinition>();
            var serialized = new SerializedObject(port);
            serialized.FindProperty("footprint").vector2IntValue = PortFootprint;
            FillBuilding(serialized, "Port", 0);
            AssetDatabase.CreateAsset(port, PortPath);
            return port;
        }

        private static (WorkshopDefinition workshop, bool created) LoadOrCreateWorkshop(
            SampleGood sample, GoodDefinition good, GoodDefinition timber, GoodDefinition metal)
        {
            string path = $"{WorkshopsFolder}/{sample.AssetName}Workshop.asset";
            var existing = AssetDatabase.LoadAssetAtPath<WorkshopDefinition>(path);

            if (existing != null)
            {
                return (existing, false);
            }

            var workshop = ScriptableObject.CreateInstance<WorkshopDefinition>();
            var serialized = new SerializedObject(workshop);
            serialized.FindProperty("good").objectReferenceValue = good;
            serialized.FindProperty("jobs").intValue = 30;
            serialized.FindProperty("footprint").vector2IntValue = WorkshopFootprint;
            FillBuilding(serialized, sample.WorkshopName, 10000, (timber, 10), (metal, 30));
            AssetDatabase.CreateAsset(workshop, path);
            return (workshop, true);
        }

        private static void FillBuilding(
            SerializedObject building, string displayName, long gold, params (GoodDefinition good, int units)[] costs)
        {
            building.FindProperty("displayName").stringValue = displayName;
            building.FindProperty("gold").longValue = gold;
            SerializedProperty materials = building.FindProperty("materials");
            materials.arraySize = costs.Length;

            for (int i = 0; i < costs.Length; i++)
            {
                SerializedProperty material = materials.GetArrayElementAtIndex(i);
                material.FindPropertyRelative("good").objectReferenceValue = costs[i].good;
                material.FindPropertyRelative("units").intValue = costs[i].units;
            }

            building.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Gives a good of the sample content, known by its asset name, the inputs of the sample
        /// content, in place of those it has; a good with none there gets none.
        /// </summary>
        public static void SetSampleInputs(GoodDefinition good)
        {
            if (good == null)
            {
                throw new System.ArgumentNullException(nameof(good));
            }

            if (!SampleInputs.TryGetValue(good.name, out (string input, double unitsPerUnit)[] inputs))
            {
                inputs = System.Array.Empty<(string, double)>();
            }

            var serialized = new SerializedObject(good);
            SerializedProperty list = serialized.FindProperty("inputs");
            list.arraySize = inputs.Length;

            for (int i = 0; i < inputs.Length; i++)
            {
                var input = AssetDatabase.LoadAssetAtPath<GoodDefinition>(GoodPath(inputs[i].input));

                if (input == null)
                {
                    throw new FileNotFoundException("An input of a sample good could not be loaded.", GoodPath(inputs[i].input));
                }

                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("good").objectReferenceValue = input;
                entry.FindPropertyRelative("unitsPerUnit").doubleValue = inputs[i].unitsPerUnit;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GoodDefinition CreateGood(SampleGood sample, out bool isNew)
        {
            string path = GoodPath(sample.AssetName);
            var existing = AssetDatabase.LoadAssetAtPath<GoodDefinition>(path);
            isNew = existing == null;

            if (existing != null)
            {
                return existing;
            }

            var good = ScriptableObject.CreateInstance<GoodDefinition>();
            var serialized = new SerializedObject(good);
            serialized.FindProperty("displayName").stringValue = sample.DisplayName;
            serialized.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(GoodIconPath(sample.AssetName));
            serialized.FindProperty("category").enumValueIndex = (int)sample.Category;
            serialized.FindProperty("unit").enumValueIndex = (int)sample.Unit;
            serialized.FindProperty("basePrice").doubleValue = sample.BasePrice;
            serialized.FindProperty("richWeeklyConsumptionPerThousand").doubleValue = sample.WeeklyConsumptionPerThousand.Rich;
            serialized.FindProperty("wealthyWeeklyConsumptionPerThousand").doubleValue = sample.WeeklyConsumptionPerThousand.Wealthy;
            serialized.FindProperty("poorWeeklyConsumptionPerThousand").doubleValue = sample.WeeklyConsumptionPerThousand.Poor;
            serialized.FindProperty("richSatisfactionWeight").intValue = sample.SatisfactionWeights.Rich;
            serialized.FindProperty("wealthySatisfactionWeight").intValue = sample.SatisfactionWeights.Wealthy;
            serialized.FindProperty("poorSatisfactionWeight").intValue = sample.SatisfactionWeights.Poor;
            serialized.FindProperty("maxWorkers").intValue = sample.MaxWorkers;
            serialized.FindProperty("dailyOutputPerWorker").doubleValue = sample.DailyOutputPerWorker;
            serialized.FindProperty("merchantDailyOutputPerWorker").doubleValue = sample.MerchantDailyOutputPerWorker;
            serialized.FindProperty("aiWeight").intValue = sample.AiWeight;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(good, path);
            return good;
        }

        private static string GoodPath(string assetName)
        {
            return $"{GoodsFolder}/{assetName}.asset";
        }

        // Named like the good's asset. A missing icon leaves the good without one.
        private static string GoodIconPath(string assetName)
        {
            return $"{GoodIconsFolder}/{assetName}.png";
        }

        // A city without a layout gets a generated grid (CityLayoutEditing.TryGenerate), the same every run: the seed
        // is the city's index. A city with a layout is the user's and is left untouched.
        private static void CreateCityLayouts()
        {
            var definition = AssetDatabase.LoadAssetAtPath<WorldMapDefinition>(DefinitionPath);
            var economy = AssetDatabase.LoadAssetAtPath<EconomyDefinition>(EconomyPath);
            var buildings = AssetDatabase.LoadAssetAtPath<BuildingsDefinition>(BuildingsPath);

            if (definition == null || economy == null || buildings == null)
            {
                return;
            }

            NavigationGrid mask = definition.NavigationMask != null ? definition.NavigationMask.CreateGrid() : null;

            for (int index = 0; index < definition.MapCities.Count; index++)
            {
                CityDefinition city = definition.MapCities[index].City;

                if (city == null || city.Layout != null)
                {
                    continue;
                }

                TryCreateLayout(city, economy, buildings, mask, index);
            }
        }

        /// <summary>
        /// Makes the default layout asset of a city and links it: a generated grid
        /// (<see cref="CityLayoutEditing.TryGenerate"/>) with water on the side of the sea, the port, the town hall,
        /// and just the houses its starting population needs, placed with the seed. A layout asset already at the
        /// city's path is linked instead, its content untouched. False (and an error logged) when the port or the
        /// houses do not fit, or something else holds the path. The caller checks that the city has no layout.
        /// </summary>
        /// <param name="mask">
        /// Where the ships sail on the world map, which puts the water on the side of the sea; null puts it to the south.
        /// </param>
        internal static bool TryCreateLayout(
            CityDefinition city, EconomyDefinition economy, BuildingsDefinition buildings, NavigationGrid mask, int seed)
        {
            string path = $"{CityLayoutsFolder}/{city.name}Layout.asset";
            Object existing = AssetDatabase.LoadMainAssetAtPath(path);

            // A layout made by hand whose link was lost: CreateAsset would replace it.
            if (existing is CityLayoutDefinition existingLayout)
            {
                LinkLayout(city, existingLayout);
                return true;
            }

            if (existing != null)
            {
                Debug.LogError($"CityDefinition '{city.name}': '{path}' is not a city layout; no layout was made.", existing);
                return false;
            }

            if (!CityLayoutEditing.TryGenerate(
                city, economy, buildings, mask, seed, CityLayoutDefinition.DefaultSize, CityLayoutDefinition.DefaultSize,
                out CityGrid grid, out string problem))
            {
                Debug.LogError($"CityDefinition '{city.name}': {problem}; no layout was made.", city);
                return false;
            }

            var layout = ScriptableObject.CreateInstance<CityLayoutDefinition>();
            layout.SetContent(grid);
            Directory.CreateDirectory(CityLayoutsFolder);
            AssetDatabase.CreateAsset(layout, path);
            LinkLayout(city, layout);
            return true;
        }

        private static void LinkLayout(CityDefinition city, CityLayoutDefinition layout)
        {
            var serialized = new SerializedObject(city);
            serialized.FindProperty("layout").objectReferenceValue = layout;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(city);
        }

        // A city that produces nothing has no economy yet: one made before the economy existed, or
        // just created. A city that produces something is the user's and is left untouched.
        private static void FillCityEconomies()
        {
            var definition = AssetDatabase.LoadAssetAtPath<WorldMapDefinition>(DefinitionPath);

            if (definition == null)
            {
                return;
            }

            // Inactive cities too: a city switched back on has its economy.
            foreach (MapCity entry in definition.MapCities)
            {
                CityDefinition city = entry.City;

                if (city == null || city.EfficientGoods.Count + city.InefficientGoods.Count > 0)
                {
                    continue;
                }

                foreach (SampleCity sample in SampleCities)
                {
                    if (sample.AssetName == city.name)
                    {
                        FillCityEconomy(city, sample);
                        break;
                    }
                }
            }
        }

        private static void FillCityEconomy(CityDefinition city, SampleCity sample)
        {
            var serialized = new SerializedObject(city);
            SetGoods(serialized.FindProperty("efficientGoods"), sample.EfficientGoods);
            SetGoods(serialized.FindProperty("inefficientGoods"), sample.InefficientGoods);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(city);
        }

        private static void SetGoods(SerializedProperty list, string[] assetNames)
        {
            list.arraySize = assetNames.Length;

            for (int i = 0; i < assetNames.Length; i++)
            {
                string path = GoodPath(assetNames[i]);
                var good = AssetDatabase.LoadAssetAtPath<GoodDefinition>(path);

                if (good == null)
                {
                    throw new FileNotFoundException("A good could not be loaded.", path);
                }

                list.GetArrayElementAtIndex(i).objectReferenceValue = good;
            }
        }

        private static ShipDefinition CreateShipDefinition()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ShipDefinition>(ShipDefinitionPath);

            if (existing != null)
            {
                return existing;
            }

            Object[] subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(ShipTexturePath);
            string[] directionNames = System.Enum.GetNames(typeof(CompassDirection));

            // Filled in before the asset is created, so the file is written complete.
            var definition = ScriptableObject.CreateInstance<ShipDefinition>();
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("displayName").stringValue = "Navire marchand";
            serialized.FindProperty("speed").floatValue = MerchantShipSpeed;
            serialized.FindProperty("crewCapacity").intValue = MerchantShipCrewCapacity;
            serialized.FindProperty("cargoCapacity").intValue = MerchantShipCargoCapacity;

            SerializedProperty sprites = serialized.FindProperty("directionSprites");
            sprites.arraySize = directionNames.Length;

            for (int i = 0; i < directionNames.Length; i++)
            {
                sprites.GetArrayElementAtIndex(i).objectReferenceValue =
                    FindShipSprite(subAssets, ShipSpritePrefix + directionNames[i]);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definition, ShipDefinitionPath);
            return definition;
        }

        private static Sprite FindShipSprite(Object[] subAssets, string spriteName)
        {
            foreach (Object subAsset in subAssets)
            {
                if (subAsset is Sprite sprite && sprite.name == spriteName)
                {
                    return sprite;
                }
            }

            throw new FileNotFoundException($"Sprite '{spriteName}' is missing from the ship sprite sheet.", ShipTexturePath);
        }

        private static ShipView CreateShipPrefab(ShipDefinition definition)
        {
            var existing = AssetDatabase.LoadAssetAtPath<ShipView>(ShipPrefabPath);

            if (existing != null)
            {
                return existing;
            }

            // ObjectFactory applies the render pipeline's default sprite material.
            GameObject instance = ObjectFactory.CreateGameObject("Ship", typeof(SpriteRenderer), typeof(ShipView));

            var spriteRenderer = instance.GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = definition.SpriteFor(CompassDirection.S);
            spriteRenderer.sortingOrder = ShipSortingOrder;

            SetReference(instance.GetComponent<ShipView>(), "spriteRenderer", spriteRenderer);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, ShipPrefabPath);
            Object.DestroyImmediate(instance);
            return prefab.GetComponent<ShipView>();
        }

        private static ShipRouteView CreateShipRoutePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ShipRouteView>(ShipRoutePrefabPath);

            if (existing != null)
            {
                return existing;
            }

            Material sailedMaterial = CreateRouteMaterial(RouteSailedMaterialPath, null);
            Material remainingMaterial = CreateRouteMaterial(RouteRemainingMaterialPath, CreateRouteDashTexture());
            Sprite markerSprite = CreateRouteMarkerSprite();

            var instance = new GameObject("ShipRoute", typeof(ShipRouteView));
            LineRenderer sailedLine = CreateRouteLine("Sailed", instance.transform, sailedMaterial);
            LineRenderer remainingLine = CreateRouteLine("Remaining", instance.transform, remainingMaterial);

            // The dashes are the material's texture, repeated along the line.
            remainingLine.textureMode = LineTextureMode.Tile;

            // ObjectFactory applies the render pipeline's default sprite material.
            GameObject markerObject = ObjectFactory.CreateGameObject("Destination", typeof(SpriteRenderer));
            markerObject.transform.SetParent(instance.transform, false);

            var markerRenderer = markerObject.GetComponent<SpriteRenderer>();
            markerRenderer.sprite = markerSprite;
            markerRenderer.sortingOrder = RouteSortingOrder + 1;

            var routeView = instance.GetComponent<ShipRouteView>();
            SetReference(routeView, "sailedLine", sailedLine);
            SetReference(routeView, "remainingLine", remainingLine);
            SetReference(routeView, "destinationMarker", markerRenderer);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, ShipRoutePrefabPath);
            Object.DestroyImmediate(instance);
            return prefab.GetComponent<ShipRouteView>();
        }

        private static LineRenderer CreateRouteLine(string objectName, Transform parent, Material material)
        {
            var lineObject = new GameObject(objectName, typeof(LineRenderer));
            lineObject.transform.SetParent(parent, false);

            var line = lineObject.GetComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 0;
            line.numCornerVertices = 2;
            line.sortingOrder = RouteSortingOrder;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        // Unlit: the route is drawn over the map, not lit as a part of it.
        private static Material CreateRouteMaterial(string path, Texture2D texture)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (existing != null)
            {
                // A material that outlived its texture would draw the dashed line solid.
                if (texture != null && existing.mainTexture == null)
                {
                    existing.mainTexture = texture;
                    EditorUtility.SetDirty(existing);
                }

                return existing;
            }

            Shader shader = Shader.Find(UnlitSpriteShaderName);

            if (shader == null)
            {
                throw new FileNotFoundException($"Shader '{UnlitSpriteShaderName}' is missing.");
            }

            var material = new Material(shader);

            if (texture != null)
            {
                material.mainTexture = texture;
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // One dash and the gap after it: a white pixel and a clear one, repeated.
        private static Texture2D CreateRouteDashTexture()
        {
            if (!File.Exists(RouteDashTexturePath))
            {
                var texture = new Texture2D(2, 1, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, Color.white);
                texture.SetPixel(1, 0, Color.clear);

                File.WriteAllBytes(RouteDashTexturePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(RouteDashTexturePath);

                // Not a sprite, which the 2D project default would make it: a line repeats it.
                var importer = (TextureImporter)AssetImporter.GetAtPath(RouteDashTexturePath);
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Point;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(RouteDashTexturePath);
        }

        // Placeholder marker: a white diagonal cross, tinted by the route view.
        private static Sprite CreateRouteMarkerSprite()
        {
            if (!File.Exists(RouteMarkerSpritePath))
            {
                const int sizePixels = 12;
                var texture = new Texture2D(sizePixels, sizePixels, TextureFormat.RGBA32, false);

                for (int y = 0; y < sizePixels; y++)
                {
                    for (int x = 0; x < sizePixels; x++)
                    {
                        bool onCross = Mathf.Abs(x - y) <= 1 || Mathf.Abs(x + y - (sizePixels - 1)) <= 1;
                        texture.SetPixel(x, y, onCross ? Color.white : Color.clear);
                    }
                }

                SaveAsPixelSprite(texture, RouteMarkerSpritePath);
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(RouteMarkerSpritePath);
        }

        private static void BuildScene()
        {
            if (File.Exists(ScenePath))
            {
                Debug.Log($"{ScenePath} already exists; scene left untouched.");
                return;
            }

            var panelTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelTemplatePath);

            if (panelTemplate == null)
            {
                throw new FileNotFoundException("The city panel UXML is missing.", PanelTemplatePath);
            }

            // Creating the scene replaces the open one; never lose unsaved work for it.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("World map scene not created: the open scene has unsaved changes.");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Opening a new scene unloads unreferenced assets, which turns asset references
            // obtained earlier into null. Load them only now.
            var definition = AssetDatabase.LoadAssetAtPath<WorldMapDefinition>(DefinitionPath);
            var markerPrefab = AssetDatabase.LoadAssetAtPath<CityMarkerView>(MarkerPrefabPath);
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);

            if (definition == null || markerPrefab == null || panelSettings == null)
            {
                throw new FileNotFoundException("A generated world map asset could not be loaded.");
            }

            GameObject cameraObject = ObjectFactory.CreateGameObject(
                "Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(SceneGroup(scene, RenderingGroup), false);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var mapCamera = cameraObject.GetComponent<Camera>();
            mapCamera.orthographic = true;
            mapCamera.clearFlags = CameraClearFlags.SolidColor;
            mapCamera.backgroundColor = Color.black;

            // Lit sprites render black under the 2D renderer without a light.
            GameObject lightObject = ObjectFactory.CreateGameObject("Global Light 2D", typeof(Light2D));
            lightObject.GetComponent<Light2D>().lightType = Light2D.LightType.Global;
            lightObject.transform.SetParent(SceneGroup(scene, RenderingGroup), false);

            GameObject mapObject = CreateInGroup(scene, WorldGroup, "World Map");
            GameObject spriteObject = ObjectFactory.CreateGameObject("Map Sprite", typeof(SpriteRenderer));
            spriteObject.transform.SetParent(mapObject.transform, false);
            var markersObject = new GameObject("City Markers");
            markersObject.transform.SetParent(mapObject.transform, false);

            var mapView = mapObject.AddComponent<WorldMapView>();
            SetReference(mapView, "definition", definition);
            SetReference(mapView, "mapRenderer", spriteObject.GetComponent<SpriteRenderer>());
            SetReference(mapView, "markerPrefab", markerPrefab);
            SetReference(mapView, "markerRoot", markersObject.transform);
            mapView.LayOutMap();

            GameObject uiObject = CreateInGroup(scene, UiGroup, "World Map UI");
            var document = uiObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = panelTemplate;

            var panelController = uiObject.AddComponent<CityInfoPanelController>();
            SetReference(panelController, "mapView", mapView);
            SetReference(panelController, "mapCamera", mapCamera);

            var input = mapObject.AddComponent<WorldMapInput>();
            SetReference(input, "ui", panelController);

            var cameraController = cameraObject.AddComponent<WorldMapCameraController>();
            SetReference(cameraController, "mapView", mapView);
            SetReference(cameraController, "input", input);

            var interaction = mapObject.AddComponent<WorldMapInteraction>();
            SetReference(interaction, "mapView", mapView);
            SetReference(interaction, "input", input);
            SetReference(interaction, "cameraController", cameraController);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = WithSceneFirst(EditorBuildSettings.scenes, ScenePath);
        }

        // Unlike the rest of the scene, the ships object is also added to a scene that
        // already exists, so a map built before ships existed gains them.
        private static void AddShipsToScene(bool sceneHadUnsavedChanges)
        {
            if (!TryOpenMapScene("Ships", out Scene scene))
            {
                return;
            }

            var mapView = FindInScene<WorldMapView>(scene);

            if (mapView == null)
            {
                Debug.LogWarning($"{ScenePath} has no WorldMapView; ships not added.");
                return;
            }

            bool changed = false;
            var shipsView = FindInScene<ShipsView>(scene);

            if (shipsView == null)
            {
                // Loaded only now: opening a scene unloads unreferenced assets.
                var shipDefinition = AssetDatabase.LoadAssetAtPath<ShipDefinition>(ShipDefinitionPath);
                var shipPrefab = AssetDatabase.LoadAssetAtPath<ShipView>(ShipPrefabPath);

                if (shipDefinition == null || shipPrefab == null)
                {
                    throw new FileNotFoundException("A generated ship asset could not be loaded.");
                }

                GameObject shipsObject = CreateInGroup(scene, WorldGroup, "Ships");

                shipsView = shipsObject.AddComponent<ShipsView>();
                SetReference(shipsView, "mapView", mapView);
                SetReference(shipsView, "shipPrefab", shipPrefab);
                SetReference(shipsView, "playerShipDefinition", shipDefinition);
                changed = true;
            }

            // Like the ships object, the route of the selected ship is added to a scene
            // built before it existed.
            if (IsReferenceEmpty(shipsView, "routeView"))
            {
                var routeView = FindInScene<ShipRouteView>(scene);

                if (routeView == null)
                {
                    var routePrefab = AssetDatabase.LoadAssetAtPath<ShipRouteView>(ShipRoutePrefabPath);

                    if (routePrefab == null)
                    {
                        throw new FileNotFoundException("The ship route prefab could not be loaded.", ShipRoutePrefabPath);
                    }

                    routeView = (ShipRouteView)PrefabUtility.InstantiatePrefab(routePrefab, shipsView.transform);
                }

                SetReference(shipsView, "routeView", routeView);
                changed = true;
            }

            // The player ship takes its starting crew from the player start, also in a
            // scene built before ships had a crew.
            if (IsReferenceEmpty(shipsView, "playerStart"))
            {
                var playerStart = AssetDatabase.LoadAssetAtPath<PlayerStartDefinition>(PlayerStartPath);

                if (playerStart == null)
                {
                    throw new FileNotFoundException("The player start could not be loaded.", PlayerStartPath);
                }

                SetReference(shipsView, "playerStart", playerStart);
                changed = true;
            }

            var interaction = FindInScene<WorldMapInteraction>(scene);

            if (interaction != null && IsReferenceEmpty(interaction, "shipsView"))
            {
                SetReference(interaction, "shipsView", shipsView);
                changed = true;
            }

            // The city panel lists the ships in port.
            var panelController = FindInScene<CityInfoPanelController>(scene);

            if (panelController != null && IsReferenceEmpty(panelController, "shipsView"))
            {
                SetReference(panelController, "shipsView", shipsView);
                changed = true;
            }

            if (changed)
            {
                SaveSceneChanges(scene, sceneHadUnsavedChanges, "Ships, their route or their starting crew");
            }
        }

        // Like the ships, the panel of the selected ship is added to a scene built before
        // it existed.
        private static void AddShipPanelToScene(bool sceneHadUnsavedChanges)
        {
            if (!TryOpenMapScene("The ship panel", out Scene scene))
            {
                return;
            }

            var shipsView = FindInScene<ShipsView>(scene);

            if (shipsView == null)
            {
                Debug.LogWarning($"{ScenePath} has no ShipsView; ship panel not added.");
                return;
            }

            var panel = FindInScene<ShipInfoPanelController>(scene);

            if (panel == null)
            {
                var panelTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ShipPanelTemplatePath);

                if (panelTemplate == null)
                {
                    throw new FileNotFoundException("The ship panel UXML is missing.", ShipPanelTemplatePath);
                }

                GameObject panelObject = CreateInGroup(scene, UiGroup, "Ship Panel");

                var document = panelObject.AddComponent<UIDocument>();
                document.panelSettings = FindPanelSettings(scene);
                document.visualTreeAsset = panelTemplate;

                panel = panelObject.AddComponent<ShipInfoPanelController>();
            }

            // Also for a scene whose ships were deleted and made again.
            if (IsReferenceEmpty(panel, "shipsView"))
            {
                SetReference(panel, "shipsView", shipsView);
                SaveSceneChanges(scene, sceneHadUnsavedChanges, "The ship panel");
            }
        }

        // Like the ships, the world clock and the time HUD are added to a scene built
        // before they existed.
        private static void AddTimeToScene(bool sceneHadUnsavedChanges)
        {
            if (!TryOpenMapScene("The world clock", out Scene scene))
            {
                return;
            }

            bool changed = false;
            var worldClock = FindInScene<WorldClock>(scene);

            if (worldClock == null)
            {
                // Loaded only now: opening a scene unloads unreferenced assets.
                var calendar = AssetDatabase.LoadAssetAtPath<CalendarDefinition>(CalendarPath);

                if (calendar == null)
                {
                    throw new FileNotFoundException("The calendar could not be loaded.", CalendarPath);
                }

                GameObject clockObject = CreateInGroup(scene, SimulationGroup, "World Clock");

                worldClock = clockObject.AddComponent<WorldClock>();
                SetReference(worldClock, "calendar", calendar);
                changed = true;
            }

            if (FindInScene<TimeHudController>(scene) == null)
            {
                var hudTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TimeHudTemplatePath);

                if (hudTemplate == null)
                {
                    throw new FileNotFoundException("The time HUD UXML is missing.", TimeHudTemplatePath);
                }

                GameObject hudObject = CreateInGroup(scene, UiGroup, "Time HUD");

                var document = hudObject.AddComponent<UIDocument>();
                document.panelSettings = FindPanelSettings(scene);
                document.visualTreeAsset = hudTemplate;

                SetReference(hudObject.AddComponent<TimeHudController>(), "worldClock", worldClock);
                changed = true;
            }

            // Ships sail in the world's time; the map and its camera follow the fast forward.
            // The HUD is in the list for a scene whose clock was deleted and made again.
            foreach (Component follower in new Component[]
            {
                FindInScene<TimeHudController>(scene),
                FindInScene<ShipsView>(scene),
                FindInScene<WorldMapInteraction>(scene),
                FindInScene<WorldMapCameraController>(scene),
            })
            {
                if (follower != null && IsReferenceEmpty(follower, "worldClock"))
                {
                    SetReference(follower, "worldClock", worldClock);
                    changed = true;
                }
            }

            if (changed)
            {
                SaveSceneChanges(scene, sceneHadUnsavedChanges, "The world clock or the time HUD");
            }
        }

        // Like the world clock, the player's treasury and its HUD are added to a scene built
        // before they existed.
        private static void AddTreasuryToScene(bool sceneHadUnsavedChanges)
        {
            if (!TryOpenMapScene("The player's treasury", out Scene scene))
            {
                return;
            }

            bool changed = false;
            var playerTreasury = FindInScene<PlayerTreasury>(scene);

            if (playerTreasury == null)
            {
                // Loaded only now: opening a scene unloads unreferenced assets.
                var playerStart = AssetDatabase.LoadAssetAtPath<PlayerStartDefinition>(PlayerStartPath);

                if (playerStart == null)
                {
                    throw new FileNotFoundException("The player start could not be loaded.", PlayerStartPath);
                }

                GameObject treasuryObject = CreateInGroup(scene, SimulationGroup, "Player Treasury");

                playerTreasury = treasuryObject.AddComponent<PlayerTreasury>();
                SetReference(playerTreasury, "playerStart", playerStart);
                changed = true;
            }

            var hud = FindInScene<TreasuryHudController>(scene);

            if (hud == null)
            {
                var hudTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TreasuryHudTemplatePath);

                if (hudTemplate == null)
                {
                    throw new FileNotFoundException("The treasury HUD UXML is missing.", TreasuryHudTemplatePath);
                }

                GameObject hudObject = CreateInGroup(scene, UiGroup, "Treasury HUD");

                var document = hudObject.AddComponent<UIDocument>();
                document.panelSettings = FindPanelSettings(scene);
                document.visualTreeAsset = hudTemplate;

                hud = hudObject.AddComponent<TreasuryHudController>();
            }

            // Also for a scene whose treasury was deleted and made again.
            if (IsReferenceEmpty(hud, "playerTreasury"))
            {
                SetReference(hud, "playerTreasury", playerTreasury);
                changed = true;
            }

            if (changed)
            {
                SaveSceneChanges(scene, sceneHadUnsavedChanges, "The player's treasury or its HUD");
            }
        }

        // Like the treasury, the player's notifications, their HUD and what posts the arrivals of
        // the ships are added to a scene built before they existed.
        private static void AddNotificationsToScene(bool sceneHadUnsavedChanges)
        {
            if (!TryOpenMapScene("The player's notifications", out Scene scene))
            {
                return;
            }

            bool changed = false;
            var playerNotifications = FindInScene<PlayerNotifications>(scene);

            if (playerNotifications == null)
            {
                GameObject notificationsObject = CreateInGroup(scene, SimulationGroup, "Player Notifications");

                playerNotifications = notificationsObject.AddComponent<PlayerNotifications>();
                changed = true;
            }

            var hud = FindInScene<NotificationsHudController>(scene);

            if (hud == null)
            {
                var hudTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(NotificationsHudTemplatePath);

                if (hudTemplate == null)
                {
                    throw new FileNotFoundException("The notifications HUD UXML is missing.", NotificationsHudTemplatePath);
                }

                GameObject hudObject = CreateInGroup(scene, UiGroup, "Notifications HUD");

                var document = hudObject.AddComponent<UIDocument>();
                document.panelSettings = FindPanelSettings(scene);
                document.visualTreeAsset = hudTemplate;

                hud = hudObject.AddComponent<NotificationsHudController>();
            }

            // Also for a scene whose notifications were deleted and made again.
            if (IsReferenceEmpty(hud, "playerNotifications"))
            {
                SetReference(hud, "playerNotifications", playerNotifications);
                changed = true;
            }

            var shipsView = FindInScene<ShipsView>(scene);
            var arrivalNotifier = FindInScene<ShipArrivalNotifier>(scene);

            // Without ships, nothing arrives.
            if (arrivalNotifier == null && shipsView != null)
            {
                GameObject notifierObject = CreateInGroup(scene, SimulationGroup, "Ship Arrival Notifier");

                arrivalNotifier = notifierObject.AddComponent<ShipArrivalNotifier>();
            }

            if (arrivalNotifier != null)
            {
                if (shipsView != null && IsReferenceEmpty(arrivalNotifier, "shipsView"))
                {
                    SetReference(arrivalNotifier, "shipsView", shipsView);
                    changed = true;
                }

                if (IsReferenceEmpty(arrivalNotifier, "playerNotifications"))
                {
                    SetReference(arrivalNotifier, "playerNotifications", playerNotifications);
                    changed = true;
                }
            }

            if (changed)
            {
                SaveSceneChanges(scene, sceneHadUnsavedChanges, "The player's notifications, their HUD or the ship arrival notifier");
            }
        }

        // Like the notifications, what makes the player pay the weekly expenses is added to a
        // scene built before it existed.
        private static void AddExpensesToScene(bool sceneHadUnsavedChanges)
        {
            if (!TryOpenMapScene("The player's expenses", out Scene scene))
            {
                return;
            }

            var worldClock = FindInScene<WorldClock>(scene);
            var playerTreasury = FindInScene<PlayerTreasury>(scene);
            var shipsView = FindInScene<ShipsView>(scene);

            // Weeks pass on the clock, the treasury pays and the ships carry the sailors.
            if (worldClock == null || playerTreasury == null || shipsView == null)
            {
                Debug.LogWarning(
                    $"{ScenePath} has no WorldClock, PlayerTreasury or ShipsView; player expenses not added.");
                return;
            }

            // Loaded only now: opening a scene unloads unreferenced assets.
            var expenses = AssetDatabase.LoadAssetAtPath<ExpensesDefinition>(ExpensesPath);

            if (expenses == null)
            {
                throw new FileNotFoundException("The expenses could not be loaded.", ExpensesPath);
            }

            bool changed = false;
            var playerExpenses = FindInScene<PlayerExpenses>(scene);

            if (playerExpenses == null)
            {
                GameObject expensesObject = CreateInGroup(scene, SimulationGroup, "Player Expenses");

                playerExpenses = expensesObject.AddComponent<PlayerExpenses>();
                changed = true;
            }

            // Also for a scene in which one of them was deleted and made again.
            changed |= FillReference(playerExpenses, "expenses", expenses);
            changed |= FillReference(playerExpenses, "worldClock", worldClock);
            changed |= FillReference(playerExpenses, "playerTreasury", playerTreasury);
            changed |= FillReference(playerExpenses, "shipsView", shipsView);
            changed |= FillReference(playerExpenses, "playerNotifications", FindInScene<PlayerNotifications>(scene));

            if (changed)
            {
                SaveSceneChanges(scene, sceneHadUnsavedChanges, "The player's expenses");
            }
        }

        // Like the expenses, the world's economy is added to a scene built before it existed, and
        // the panels that show it are told where it is.
        private static void AddEconomyToScene(bool sceneHadUnsavedChanges)
        {
            if (!TryOpenMapScene("The world's economy", out Scene scene))
            {
                return;
            }

            var mapView = FindInScene<WorldMapView>(scene);
            var worldClock = FindInScene<WorldClock>(scene);

            // Cities are the map's, and they produce and consume as the days pass.
            if (mapView == null || worldClock == null)
            {
                Debug.LogWarning($"{ScenePath} has no WorldMapView or WorldClock; world economy not added.");
                return;
            }

            // Loaded only now: opening a scene unloads unreferenced assets.
            var economy = AssetDatabase.LoadAssetAtPath<EconomyDefinition>(EconomyPath);

            if (economy == null)
            {
                throw new FileNotFoundException("The economy could not be loaded.", EconomyPath);
            }

            var buildings = AssetDatabase.LoadAssetAtPath<BuildingsDefinition>(BuildingsPath);

            if (buildings == null)
            {
                throw new FileNotFoundException("The buildings could not be loaded.", BuildingsPath);
            }

            bool changed = false;
            var worldEconomy = FindInScene<WorldEconomy>(scene);

            if (worldEconomy == null)
            {
                GameObject economyObject = CreateInGroup(scene, SimulationGroup, "World Economy");

                worldEconomy = economyObject.AddComponent<WorldEconomy>();
                changed = true;
            }

            // Also for a scene in which one of them was deleted and made again.
            changed |= FillReference(worldEconomy, "economy", economy);
            changed |= FillReference(worldEconomy, "buildings", buildings);
            changed |= FillReference(worldEconomy, "mapView", mapView);
            changed |= FillReference(worldEconomy, "worldClock", worldClock);

            // Ships take the room of a unit of each good from the economy.
            var shipsView = FindInScene<ShipsView>(scene);

            if (shipsView != null)
            {
                changed |= FillReference(shipsView, "economy", economy);
            }

            // The city panel shows the market and trades with the player's gold.
            var cityPanel = FindInScene<CityInfoPanelController>(scene);

            if (cityPanel != null)
            {
                changed |= FillReference(cityPanel, "worldEconomy", worldEconomy);
                changed |= FillReference(cityPanel, "playerTreasury", FindInScene<PlayerTreasury>(scene));
            }

            // The ship panel names the goods of the hold.
            var shipPanel = FindInScene<ShipInfoPanelController>(scene);

            if (shipPanel != null)
            {
                changed |= FillReference(shipPanel, "economy", economy);
            }

            if (changed)
            {
                SaveSceneChanges(scene, sceneHadUnsavedChanges, "The world's economy");
            }
        }

        // Like the economy they trade in, the AI merchants are added to a scene built before
        // they existed.
        private static void AddAiMerchantsToScene(bool sceneHadUnsavedChanges)
        {
            if (!TryOpenMapScene("The AI merchants", out Scene scene))
            {
                return;
            }

            var shipsView = FindInScene<ShipsView>(scene);
            var worldEconomy = FindInScene<WorldEconomy>(scene);
            var mapView = FindInScene<WorldMapView>(scene);
            var worldClock = FindInScene<WorldClock>(scene);

            // Their ships are the map's, they trade in its markets and stay in port as the days pass.
            if (shipsView == null || worldEconomy == null || mapView == null || worldClock == null)
            {
                Debug.LogWarning(
                    $"{ScenePath} has no ShipsView, WorldEconomy, WorldMapView or WorldClock; AI merchants not added.");
                return;
            }

            // Loaded only now: opening a scene unloads unreferenced assets.
            var definition = AssetDatabase.LoadAssetAtPath<AiMerchantsDefinition>(AiMerchantsPath);

            if (definition == null)
            {
                throw new FileNotFoundException("The AI merchants could not be loaded.", AiMerchantsPath);
            }

            bool changed = false;
            var aiMerchants = FindInScene<AiMerchants>(scene);

            if (aiMerchants == null)
            {
                GameObject merchantsObject = CreateInGroup(scene, SimulationGroup, "AI Merchants");

                aiMerchants = merchantsObject.AddComponent<AiMerchants>();
                changed = true;
            }

            // Also for a scene in which one of them was deleted and made again.
            changed |= FillReference(aiMerchants, "definition", definition);
            changed |= FillReference(aiMerchants, "shipsView", shipsView);
            changed |= FillReference(aiMerchants, "worldEconomy", worldEconomy);
            changed |= FillReference(aiMerchants, "mapView", mapView);
            changed |= FillReference(aiMerchants, "worldClock", worldClock);

            if (changed)
            {
                SaveSceneChanges(scene, sceneHadUnsavedChanges, "The AI merchants");
            }
        }

        // The one component through which the player builds, added to a scene built before it existed.
        private static void AddPlayerBuilderToScene(bool sceneHadUnsavedChanges)
        {
            if (!TryOpenMapScene("The player builder", out Scene scene))
            {
                return;
            }

            var worldEconomy = FindInScene<WorldEconomy>(scene);
            var playerTreasury = FindInScene<PlayerTreasury>(scene);
            var playerNotifications = FindInScene<PlayerNotifications>(scene);
            var shipsView = FindInScene<ShipsView>(scene);

            // It builds in the markets with the player's gold; the notifications and the ships
            // (whose hold pays first) are optional.
            if (worldEconomy == null || playerTreasury == null)
            {
                Debug.LogWarning(
                    $"{ScenePath} has no WorldEconomy or PlayerTreasury; player builder not added.");
                return;
            }

            bool changed = false;
            var builder = FindInScene<PlayerBuilder>(scene);

            if (builder == null)
            {
                GameObject builderObject = CreateInGroup(scene, SimulationGroup, "Player Builder");

                builder = builderObject.AddComponent<PlayerBuilder>();
                changed = true;
            }

            // Also for a scene in which one of them was deleted and made again.
            changed |= FillReference(builder, "worldEconomy", worldEconomy);
            changed |= FillReference(builder, "playerTreasury", playerTreasury);
            changed |= FillReference(builder, "playerNotifications", playerNotifications);
            changed |= FillReference(builder, "shipsView", shipsView);

            if (changed)
            {
                SaveSceneChanges(scene, sceneHadUnsavedChanges, "The player builder");
            }
        }

        // After every component it gives access to, so that it finds them all, also in a scene
        // built before it existed.
        private static void AddSimulationServicesToScene(bool sceneHadUnsavedChanges)
        {
            if (!TryOpenMapScene("The simulation services", out Scene scene))
            {
                return;
            }

            var worldClock = FindInScene<WorldClock>(scene);
            var playerTreasury = FindInScene<PlayerTreasury>(scene);
            var playerNotifications = FindInScene<PlayerNotifications>(scene);
            var worldEconomy = FindInScene<WorldEconomy>(scene);
            var shipsView = FindInScene<ShipsView>(scene);
            var aiMerchants = FindInScene<AiMerchants>(scene);
            var playerBuilder = FindInScene<PlayerBuilder>(scene);

            // Added all the same: the services the scene has are reachable, the others filled in
            // by a later run.
            if (worldClock == null || playerTreasury == null || playerNotifications == null
                || worldEconomy == null || shipsView == null || aiMerchants == null || playerBuilder == null)
            {
                Debug.LogWarning(
                    $"{ScenePath} lacks a WorldClock, PlayerTreasury, PlayerNotifications, WorldEconomy, "
                    + "ShipsView, AiMerchants or PlayerBuilder; the simulation services miss it.");
            }

            bool changed = false;
            var services = FindInScene<SimulationServices>(scene);

            if (services == null)
            {
                GameObject servicesObject = CreateInGroup(scene, SimulationGroup, "Simulation Services");

                services = servicesObject.AddComponent<SimulationServices>();
                changed = true;
            }

            // Also for a scene in which one of them was deleted and made again.
            changed |= FillReference(services, "worldClock", worldClock);
            changed |= FillReference(services, "playerTreasury", playerTreasury);
            changed |= FillReference(services, "playerNotifications", playerNotifications);
            changed |= FillReference(services, "worldEconomy", worldEconomy);
            changed |= FillReference(services, "shipsView", shipsView);
            changed |= FillReference(services, "aiMerchants", aiMerchants);
            changed |= FillReference(services, "playerBuilder", playerBuilder);

            if (changed)
            {
                SaveSceneChanges(scene, sceneHadUnsavedChanges, "The simulation services");
            }
        }

        // Like the economy and the ships it shows, the debug panel is added to a scene built
        // before it existed.
        private static void AddDebugToolsToScene(bool sceneHadUnsavedChanges)
        {
            if (!TryOpenMapScene("The debug panel", out Scene scene))
            {
                return;
            }

            var worldEconomy = FindInScene<WorldEconomy>(scene);
            var mapView = FindInScene<WorldMapView>(scene);

            // The panel lists the cities of the map with their markets. Its ships tab is optional.
            if (worldEconomy == null || mapView == null)
            {
                Debug.LogWarning($"{ScenePath} has no WorldEconomy or WorldMapView; debug panel not added.");
                return;
            }

            bool changed = false;
            var panel = FindInScene<DebugPanelController>(scene);

            if (panel == null)
            {
                var panelTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(DebugPanelTemplatePath);

                if (panelTemplate == null)
                {
                    throw new FileNotFoundException("The debug panel UXML is missing.", DebugPanelTemplatePath);
                }

                GameObject panelObject = CreateInGroup(scene, DebugGroup, "Debug Panel");

                var document = panelObject.AddComponent<UIDocument>();
                document.panelSettings = FindPanelSettings(scene);
                document.visualTreeAsset = panelTemplate;

                // Over the panels of the game, the city's in particular, which it overlaps.
                document.sortingOrder = DebugSortingOrder;

                panel = panelObject.AddComponent<DebugPanelController>();
                changed = true;
            }

            // Also for a scene in which one of them was deleted and made again.
            changed |= FillReference(panel, "worldEconomy", worldEconomy);
            changed |= FillReference(panel, "mapView", mapView);

            // The ships tab lists the ships, with the profit of the merchants that trade on their own.
            changed |= FillReference(panel, "shipsView", FindInScene<ShipsView>(scene));
            changed |= FillReference(panel, "aiMerchants", FindInScene<AiMerchants>(scene));

            // Its button that runs the world much faster than the player's fast forward.
            changed |= FillReference(panel, "worldClock", FindInScene<WorldClock>(scene));

            // What the panel shows of the past: recorded from the start, panel open or not.
            var recorder = FindInScene<SimulationRecorder>(scene);

            if (recorder == null)
            {
                GameObject recorderObject = CreateInGroup(scene, DebugGroup, "Simulation Recorder");
                recorder = recorderObject.AddComponent<SimulationRecorder>();
                changed = true;
            }

            changed |= FillReference(recorder, "worldEconomy", worldEconomy);
            changed |= FillReference(recorder, "mapView", mapView);
            changed |= FillReference(recorder, "worldClock", FindInScene<WorldClock>(scene));
            changed |= FillReference(recorder, "aiMerchants", FindInScene<AiMerchants>(scene));
            changed |= FillReference(recorder, "cityPanel", FindInScene<CityInfoPanelController>(scene));
            changed |= FillReference(recorder, "playerBuilder", FindInScene<PlayerBuilder>(scene));
            changed |= FillReference(panel, "recorder", recorder);

            // Its box that makes the player's buildings free.
            changed |= FillReference(panel, "playerBuilder", FindInScene<PlayerBuilder>(scene));

            if (changed)
            {
                SaveSceneChanges(scene, sceneHadUnsavedChanges, "The debug panel");
            }
        }

        // The only way into the city view and out of it: it switches the map off and on around it.
        private static void AddCityViewLoaderToScene(bool sceneHadUnsavedChanges)
        {
            if (!TryOpenMapScene("The city view loader", out Scene scene))
            {
                return;
            }

            var cameraController = FindInScene<WorldMapCameraController>(scene);
            var input = FindInScene<WorldMapInput>(scene);
            var interaction = FindInScene<WorldMapInteraction>(scene);
            var mapView = FindInScene<WorldMapView>(scene);
            var cityPanel = FindInScene<CityInfoPanelController>(scene);

            // The ships are optional.
            if (cameraController == null || input == null || interaction == null || mapView == null || cityPanel == null)
            {
                Debug.LogWarning(
                    $"{ScenePath} lacks the map camera, input, interaction, view or city panel; city view loader not added.");
                return;
            }

            bool changed = false;
            var loader = FindInScene<CityViewLoader>(scene);

            if (loader == null)
            {
                loader = CreateInGroup(scene, WorldGroup, "City View Loader").AddComponent<CityViewLoader>();
                changed = true;
            }

            // Also for a scene in which one of them was deleted and made again.
            // The map view checks the cities' layouts with the catalogue.
            changed |= FillReference(mapView, "buildings", AssetDatabase.LoadAssetAtPath<BuildingsDefinition>(BuildingsPath));
            changed |= FillReference(loader, "mapCamera", cameraController.gameObject);
            changed |= FillReference(loader, "mapInput", input);
            changed |= FillReference(loader, "mapInteraction", interaction);
            changed |= FillReference(loader, "mapView", mapView);
            changed |= FillReference(loader, "shipsView", FindInScene<ShipsView>(scene));
            changed |= FillReference(loader, "cityPanel", cityPanel);
            changed |= FillReference(loader, "worldClock", FindInScene<WorldClock>(scene));
            changed |= FillReference(interaction, "cityViewLoader", loader);

            if (changed)
            {
                SaveSceneChanges(scene, sceneHadUnsavedChanges, "The city view loader");
            }
        }

        // The HUDs and the ship panel share the city panel's panel, so that the map sees the pointer over
        // their buttons as it does over the panel.
        private static PanelSettings FindPanelSettings(Scene scene)
        {
            var cityPanel = FindInScene<CityInfoPanelController>(scene);
            PanelSettings panelSettings = cityPanel != null
                ? cityPanel.GetComponent<UIDocument>().panelSettings
                : null;

            if (panelSettings == null)
            {
                panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            }

            if (panelSettings == null)
            {
                throw new FileNotFoundException("The panel settings could not be loaded.", PanelSettingsPath);
            }

            return panelSettings;
        }

        /// <returns>False when the scene does not exist or could not be opened.</returns>
        internal static bool TryOpenMapScene(string what, out Scene scene)
        {
            scene = default;

            // The scene is missing when its creation was cancelled.
            if (!File.Exists(ScenePath))
            {
                return false;
            }

            scene = SceneManager.GetSceneByPath(ScenePath);

            if (scene.isLoaded)
            {
                return true;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log($"{what} not added to the world map scene: the open scene has unsaved changes.");
                return false;
            }

            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return true;
        }

        internal static void SaveSceneChanges(Scene scene, bool sceneHadUnsavedChanges, string what)
        {
            EditorSceneManager.MarkSceneDirty(scene);

            // Saving would also write the user's own pending edits; leave that to them.
            if (sceneHadUnsavedChanges)
            {
                Debug.Log($"{what} added to {ScenePath}. The scene had unsaved changes: save it to keep them.");
                return;
            }

            EditorSceneManager.SaveScene(scene);
        }

        internal static GameObject CreateInGroup(Scene scene, string group, string name)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(SceneGroup(scene, group), false);
            return gameObject;
        }

        // A group missing from an existing scene is added after the others.
        private static Transform SceneGroup(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root.transform;
                }
            }

            var group = new GameObject(name);
            SceneManager.MoveGameObjectToScene(group, scene);
            return group.transform;
        }

        internal static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);

                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        internal static bool IsReferenceEmpty(Object target, string propertyName)
        {
            SerializedProperty property = new SerializedObject(target).FindProperty(propertyName);

            if (property == null)
            {
                throw new System.MissingFieldException(target.GetType().Name, propertyName);
            }

            return property.objectReferenceValue == null;
        }

        /// <returns>True when the reference was empty and is now set.</returns>
        internal static bool FillReference(Object target, string propertyName, Object value)
        {
            if (value == null || !IsReferenceEmpty(target, propertyName))
            {
                return false;
            }

            SetReference(target, propertyName, value);
            return true;
        }

        /// <summary>
        /// Returns the build scene list with <paramref name="scenePath"/> first. A list that
        /// already contains it is returned unchanged; other scenes are always kept.
        /// </summary>
        public static EditorBuildSettingsScene[] WithSceneFirst(EditorBuildSettingsScene[] scenes, string scenePath)
        {
            foreach (EditorBuildSettingsScene existing in scenes)
            {
                if (existing.path == scenePath)
                {
                    return scenes;
                }
            }

            var result = new EditorBuildSettingsScene[scenes.Length + 1];
            result[0] = new EditorBuildSettingsScene(scenePath, true);
            scenes.CopyTo(result, 1);
            return result;
        }

        /// <summary>
        /// Returns the build scene list with <paramref name="scenePath"/> right after <paramref name="afterPath"/>,
        /// or last when that one is not listed. A list that already contains it is returned unchanged; other scenes
        /// are always kept, in their order.
        /// </summary>
        public static EditorBuildSettingsScene[] WithSceneAfter(
            EditorBuildSettingsScene[] scenes, string scenePath, string afterPath)
        {
            int insertAt = scenes.Length;

            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == scenePath)
                {
                    return scenes;
                }

                if (scenes[i].path == afterPath)
                {
                    insertAt = i + 1;
                }
            }

            var result = new List<EditorBuildSettingsScene>(scenes);
            result.Insert(insertAt, new EditorBuildSettingsScene(scenePath, true));
            return result.ToArray();
        }

        internal static void SetReference(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);

            if (property == null)
            {
                throw new System.MissingFieldException(target.GetType().Name, propertyName);
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
