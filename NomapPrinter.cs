using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using ServerSync;

namespace NomapPrinter
{
    [BepInPlugin(pluginID, pluginName, pluginVersion)]
    public class ValheimMod : BaseUnityPlugin
    {
        const string pluginID = "shudnal.NomapPrinter";
        const string pluginName = "Nomap Printer";
        const string pluginVersion = "1.0.3";

        private readonly Harmony harmony = new Harmony(pluginID);

        internal static readonly ConfigSync configSync = new ConfigSync(pluginID) { DisplayName = pluginName, CurrentVersion = pluginVersion, MinimumRequiredVersion = pluginVersion };

        private static ConfigEntry<bool> modEnabled;
        private static ConfigEntry<bool> configLocked;
        private static ConfigEntry<bool> doubleTheSize;
        private static ConfigEntry<float> showInRadius; 

        private static ConfigEntry<bool> loggingEnabled;

        private static ConfigEntry<bool> saveMapToFile;
        private static ConfigEntry<string> filePath;

        private static ConfigEntry<MapType> mapType;
        private static ConfigEntry<float> mapDefaultScale;
        private static ConfigEntry<float> mapMinimumScale;
        private static ConfigEntry<float> mapMaximumScale;

        private static ConfigEntry<bool> showPins;
        private static ConfigEntry<bool> showExploredPins;
        private static ConfigEntry<bool> showMyPins;
        private static ConfigEntry<bool> showNonCheckedPins;
        private static ConfigEntry<bool> showMerchantPins;

        private static ConfigEntry<bool> showEveryPin;
        private static ConfigEntry<bool> showPinStart;
        private static ConfigEntry<bool> showPinTrader;
        private static ConfigEntry<bool> showPinHildir;
        private static ConfigEntry<bool> showPinHildirQuest;
        private static ConfigEntry<bool> showPinBoss;
        private static ConfigEntry<bool> showPinFire;
        private static ConfigEntry<bool> showPinHouse;
        private static ConfigEntry<bool> showPinHammer;
        private static ConfigEntry<bool> showPinPin;
        private static ConfigEntry<bool> showPinPortal;
        private static ConfigEntry<bool> showPinBed;
        private static ConfigEntry<bool> showPinDeath;

        private static ConfigEntry<string> messageStart;
        private static ConfigEntry<string> messageSaving;
        private static ConfigEntry<string> messageReady;
        private static ConfigEntry<string> messageSavedTo;
        private static ConfigEntry<string> messageNotReady;

        public static World game_world;
        public static float abyss_depth = -100f;

        static int m_textureSize;
        static int m_waterLevel;

        private static Color32[] m_mapTexture;
        private static Color32[] m_forestTexture;
        private static Color32[] m_heightmap;
        private static Color32[] m_fogmap;
        private static int[] heightBytes;

        NomapPrinter maker;
        private static ValheimMod instance;

        private static string path;

        private static readonly Dictionary<string, Color32[]> pinIcons = new Dictionary<string, Color32[]>();

        public static GameObject parentObject;
        public static GameObject mapContent;
        public static RectTransform content;
        private static float mapCurrentScale = 0.7f;

        public static Texture2D mapTexture = new Texture2D(4069, 4096);

        private static bool mapWindowInitialized = false;
        private static bool mapTextureIsReady = false;

        private bool _displayingWindow = false;

        private PropertyInfo _curLockState;
        private PropertyInfo _curVisible;
        private int _previousCursorLockState;
        private bool _previousCursorVisible;

        private static string saveFieldKey;

        public event EventHandler<ValueChangedEventArgs<bool>> DisplayingWindowChanged;

        public enum MapType
        {
            BirdsEye,
            Topographical,
            Chart,
            OldChart
        }

        void Awake()
        {
            harmony.PatchAll();
            instance = this;
            maker = new NomapPrinter();

            ConfigInit();
            _ = configSync.AddLockingConfigEntry(configLocked);
        }

        void OnDestroy()
        {
            instance = null;
            harmony?.UnpatchSelf();
        }

        private void OnGUI()
        {
            if (DisplayingWindow)
            {
                // enable cursor if map is displaying
                SetUnlockCursor(0, true);

                if (Event.current.isMouse)
                {
                    if (Event.current.type == EventType.MouseDown)
                    {
                        if (Input.GetMouseButtonDown(1)) // Right click to reset zoom
                        {
                            ZoomMap(0);
                        }
                        else if (Input.GetMouseButtonDown(2)) // Middle click to reset position
                        {
                            CenterMap();
                        }
                    }

                    // Block every other mouse clicks
                    Input.ResetInputAxes();
                }
            }
        }

        private void Start()
        {
            var tCursor = typeof(Cursor);
            _curLockState = tCursor.GetProperty("lockState", BindingFlags.Static | BindingFlags.Public);
            _curVisible = tCursor.GetProperty("visible", BindingFlags.Static | BindingFlags.Public);
        }

        private void Update()
        {

            if (!modEnabled.Value)
                return;

            if (!mapWindowInitialized)
                return;

            if (!Game.m_noMap)
                return;

            if (CanOperateMap)
            {
                if (ZInput.GetButtonUp("Map") || ZInput.GetButtonUp("JoyMap"))
                {
                    if (!mapTextureIsReady)
                    {
                        ShowMessage(messageNotReady.Value);
                    }
                    else
                    {
                        DisplayingWindow = !DisplayingWindow;
                    }
                }
                else if (DisplayingWindow && ZInput.GetKeyDown(KeyCode.Escape))
                {
                    DisplayingWindow = false;
                }
            }

            if (DisplayingWindow)
            {
                // enable scroll to change map scale
                float scrollIncrement = ZInput.GetAxis("Mouse ScrollWheel");
                if (scrollIncrement != 0)
                {
                    ZoomMap(scrollIncrement);
                }

                // disable everything else not disabled at OnGUI call
                Input.ResetInputAxes();
            }
        }

        private static void ZoomMap(float increment)
        {
            mapCurrentScale = (increment == 0f) ? mapDefaultScale.Value : mapCurrentScale + increment / 2;

            float minScale = Math.Max(mapMinimumScale.Value, 0.1f) * (doubleTheSize.Value ? 1 : 2);
            float maxScale = Math.Min(mapMaximumScale.Value, 2f) * (doubleTheSize.Value ? 1 : 2);

            if (mapCurrentScale >= maxScale)
            {
                mapCurrentScale = maxScale;
            }
            else if (mapCurrentScale <= minScale)
            {
                mapCurrentScale = minScale;
            }
            content.localScale = new Vector2(mapCurrentScale, mapCurrentScale);
        }

        private static void CenterMap()
        {
            content.localPosition = Vector2.zero;
        }

        private static void ResetViewerContentSize()
        {
            if (content.sizeDelta.x != mapTexture.width)
            {
                content.sizeDelta = new Vector2(mapTexture.width, mapTexture.height);
                mapContent.GetComponent<Image>().sprite = Sprite.Create(mapTexture, new Rect(0, 0, mapTexture.width, mapTexture.height), Vector2.zero);

                ZoomMap(0);
                CenterMap();
            }
        }

        public static void Log(object message)
        {
            if (loggingEnabled.Value)
                instance.Logger.LogInfo(message);
        }

        public bool CanOperateMap
        {
            get
            {
                Player localPlayer = Player.m_localPlayer;

                return !(localPlayer == null || localPlayer.IsDead() || localPlayer.InCutscene() || localPlayer.IsTeleporting()) &&
                        (Chat.instance == null || !Chat.instance.HasFocus()) &&
                        !Console.IsVisible() && !Menu.IsVisible() && TextViewer.instance != null &&
                        !TextViewer.instance.IsVisible() && !TextInput.IsVisible() && !Minimap.IsOpen();

            }
        }

        public bool DisplayingWindow
        {
            get => _displayingWindow;
            set
            {
                if (_displayingWindow == value) return;

                if (value && showInRadius.Value > 0)
                {
                    List<Piece> pieces = new List<Piece>(); ;
                    Piece.GetAllPiecesInRadius(Player.m_localPlayer.transform.position, showInRadius.Value, pieces);
                    foreach (Piece piece in pieces)
                    {
                        value = piece.TryGetComponent<MapTable>(out MapTable table);
                        
                        if (value)
                        {
                            break;
                        }
                            
                    }
                }

                _displayingWindow = value;

                if (_displayingWindow)
                {
                    if (_curLockState != null)
                    {
                        _previousCursorLockState = (int)_curLockState.GetValue(null, null);
                        _previousCursorVisible = (bool)_curVisible.GetValue(null, null);
                    }
                }
                else
                {
                    if (!_previousCursorVisible || _previousCursorLockState != 0) // 0 = CursorLockMode.None
                        SetUnlockCursor(_previousCursorLockState, _previousCursorVisible);
                }

                parentObject.SetActive(_displayingWindow);

                DisplayingWindowChanged?.Invoke(this, new ValueChangedEventArgs<bool>(value));
            }
        }

        private void ConfigInit()
        {
            config("General", "NexusID", 2505, "Nexus mod ID for updates", false);

            modEnabled = config("General", "Enabled", true, "Print map on table interaction");
            configLocked = config("General", "Lock Configuration", defaultValue: true, "Configuration is locked and can be changed by server admins only.");

            loggingEnabled = config("Logging", "Enabled", false, "Enable logging. [Not Synced with Server]", false);

            saveMapToFile = config("Map", "Save to file", false, "Save map to file. [Not Synced with Server]", false);
            filePath = config("Map", "Save to file path", "", "File path used to save generated map. [Not Synced with Server]", false);
            showInRadius = config("Map", "Show map when the table near", defaultValue: 0f, "Distance to nearest map table for map to be shown if set");
            mapDefaultScale = config("Map", "Map zoom default scale", 0.7f, "Default scale of opened map, more is closer, less is farther. [Not Synced with Server]", false);
            mapMinimumScale = config("Map", "Map zoom minimum scale", 0.25f, "Minimum scale of opened map, more is closer, less is farther. [Not Synced with Server]", false);
            mapMaximumScale = config("Map", "Map zoom maximum scale", 1.0f, "Maximum scale of opened map, more is closer, less is farther. [Not Synced with Server]", false);

            mapType = config("Map", "Map type", MapType.Chart, "Type of showed map. [Not Synced with Server]", false);
            doubleTheSize = config("Map", "Map size 8k", false, "Doubles the map resolution. [Not Synced with Server]", false);

            showPins = config("Pins", "Show map pins", true, "Show pins on drawed map");
            showExploredPins = config("Pins", "Show only explored pins", true, "Only show pins on explored part of the map");
            showMerchantPins = config("Pins", "Show merchants pins always", true, "Show merchant pins even in unexplored part of the map");
            showMyPins = config("Pins", "Show only my pins", true, "Only show your pins on the map");
            showNonCheckedPins = config("Pins", "Show only unchecked pins", true, "Only show pins that doesn't checked (have no red cross)");

            showEveryPin = config("Pins list", "Show all pins", true, "Show all pins");
            showPinStart = config("Pins list", "Show Start pins", true, "Show Start pin on drawed map");
            showPinTrader = config("Pins list", "Show Haldor pins", true, "Show Haldor pin on drawed map");
            showPinHildir = config("Pins list", "Show Hildir pins", true, "Show Hildir pin on drawed map");
            showPinHildirQuest = config("Pins list", "Show Hildir quest pins", true, "Show Hildir quest pins on drawed map");
            showPinBoss = config("Pins list", "Show Boss pins", true, "Show Boss pins on drawed map");
            showPinFire = config("Pins list", "Show Fire pins", true, "Show Fire pins on drawed map");
            showPinHouse = config("Pins list", "Show House pins", true, "Show House pins on drawed map");
            showPinHammer = config("Pins list", "Show Hammer pins", true, "Show Hammer pins on drawed map");
            showPinPin = config("Pins list", "Show Pin pins", true, "Show Pin pins on drawed map");
            showPinPortal = config("Pins list", "Show Portal pins", true, "Show Portal pins on drawed map");
            showPinBed = config("Pins list", "Show Bed pins", false, "Show Bed pins on drawed map");
            showPinDeath = config("Pins list", "Show Death pins", false, "Show Death pins on drawed map");

            messageStart = config("Messages", "Drawing begin", "Remembering travels...", "Center message when drawing is started. [Not Synced with Server]", false);
            messageSaving = config("Messages", "Drawing end", "Drawing map...", "Center message when saving file is started. [Not Synced with Server]", false);
            messageReady = config("Messages", "Saved", "Map is ready", "Center message when file is saved. [Not Synced with Server]", false);
            messageSavedTo = config("Messages", "Saved to", "Map saved to", "Top left message with file name. [Not Synced with Server]", false);
            messageNotReady = config("Messages", "Not ready", "Map is not drawn yet", "Top left message on trying to open a not ready map. [Not Synced with Server]", false);

            path = filePath.Value == "" ? Path.Combine(Utils.GetSaveDataPath(FileHelpers.FileSource.Local), "screenshots") : filePath.Value;
            Log("Saves going to " + path);

            NomapPrinter.InitIconSize();
        }

        private void ConfigUpdate()
        {
            Config.Reload();
            ConfigInit();

            m_waterLevel = (int)ZoneSystem.instance.m_waterLevel;
            m_textureSize = Minimap.instance.m_textureSize;
        }

        ConfigEntry<T> config<T>(string group, string name, T defaultValue, ConfigDescription description, bool synchronizedSetting = true)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, defaultValue, description);

            SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        ConfigEntry<T> config<T>(string group, string name, T defaultValue, string description, bool synchronizedSetting = true) => config(group, name, defaultValue, new ConfigDescription(description), synchronizedSetting);

        private static void ShowMessage(string text, MessageHud.MessageType type = MessageHud.MessageType.Center)
        {
            // if someone doesn't want a message and cleared the value
            if (text.IsNullOrWhiteSpace())
                return;

            MessageHud.instance.ShowMessage(type, text, 1);
        }

        private void SetUnlockCursor(int lockState, bool cursorVisible)
        {
            if (_curLockState != null)
            {
                _curLockState.SetValue(null, lockState, null);
                _curVisible.SetValue(null, cursorVisible, null);
            }
        }

        private static void SaveValue(Player player, string key, string value)
        {
            if (player.m_customData.ContainsKey(key))
                player.m_customData[key] = value;
            else
                player.m_customData.Add(key, value);
        }

        private static void DeleteValue(Player player, string key)
        {
            player.m_customData.Remove(key);
        }

        private static bool LoadValue(Player player, string key, out string value)
        {
            if (player.m_customData.TryGetValue(key, out value))
                return true;

            return false;
        }

        private static bool LoadMapFromPlayer(Player player)
        {
            if (player == null)
                return false;

            if (!LoadValue(player, saveFieldKey, out string texBase64))
                return false;

            try
            {
                mapTexture.LoadImage(Convert.FromBase64String(texBase64));
                mapTexture.Apply();
            }
            catch (Exception ex)
            {
                Log($"Loading map error. Invalid printed map texture: {ex}");
                DeleteMapFromPlayer(player);
                return false;
            }

            return true;
        }

        private static void DeleteMapFromPlayer(Player player)
        {
            if (player == null)
                return;

            DeleteValue(player, saveFieldKey);
        }

        private static void SaveMapToPlayer(Player player)
        {
            if (player == null)
                return;

            if (mapTextureIsReady)
            {
                try
                {
                    SaveValue(player, saveFieldKey, Convert.ToBase64String(mapTexture.EncodeToPNG()));
                }
                catch (Exception ex)
                {
                    Log($"Saving map error. Invalid printed map texture: {ex}");
                    DeleteMapFromPlayer(player);
                }
            }
            else
            {
                DeleteMapFromPlayer(player);
            }
        }

        public static void AddIngameView(Transform parentTransform)
        {

            // Parent object to set visibility
            parentObject = new GameObject("NomapPrinter_Parent", typeof(RectTransform));
            parentObject.transform.SetParent(parentTransform, false);
            parentObject.layer = LayerMask.NameToLayer("UI");

            // Parent rect with size of fullscreen
            RectTransform pRectTransform = parentObject.GetComponent<RectTransform>();
            pRectTransform.anchoredPosition = Vector2.zero;
            pRectTransform.anchorMin = Vector2.zero;
            pRectTransform.anchorMax = Vector2.one;
            pRectTransform.sizeDelta = Vector2.zero;

            // ScrollView object to operate the map
            GameObject mapScrollView = new GameObject("NomapPrinter_ScrollView", typeof(RectTransform));
            mapScrollView.layer = LayerMask.NameToLayer("UI");
            mapScrollView.transform.SetParent(parentObject.transform, false);

            // ScrollView rect with margin from screen edge
            RectTransform rtScrollView = mapScrollView.GetComponent<RectTransform>();
            rtScrollView.anchorMin = Vector2.zero;
            rtScrollView.anchorMax = Vector2.one;
            rtScrollView.sizeDelta = new Vector2(-300f, -200f);

            // Background image to make the borders visible
            mapScrollView.AddComponent<Image>().color = new Color(0, 0, 0, 0.4f);

            // ScrollRect component with inner scrolling logic
            ScrollRect svScrollRect = mapScrollView.AddComponent<ScrollRect>();

            // Viewport object of ScrollRect logic
            GameObject mapViewPort = new GameObject("NomapPrinter_ViewPort", typeof(RectTransform));
            mapViewPort.layer = LayerMask.NameToLayer("UI");
            mapViewPort.transform.SetParent(mapScrollView.transform, false);

            // Auto applied mask
            mapViewPort.AddComponent<RectMask2D>();
            mapViewPort.AddComponent<Image>().color = new Color(0, 0, 0, 0);

            // Viewport rect is on 6 pixels less then Scrollview to make borders
            RectTransform viewport = mapViewPort.GetComponent<RectTransform>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.sizeDelta = new Vector2(-12f, -12f);
            viewport.anchoredPosition = Vector2.zero;

            // Content object to contain the Map image
            mapContent = new GameObject("NomapPrinter_Map", typeof(RectTransform));
            mapContent.layer = LayerMask.NameToLayer("UI");

            // Map rect is full size. It must be child of Viewport
            content = mapContent.GetComponent<RectTransform>();
            content.SetParent(mapViewPort.transform);
            content.sizeDelta = new Vector2(mapTexture.width, mapTexture.height);
            content.anchoredPosition = Vector2.zero;

            ZoomMap(0);

            // Map image component
            Image mapImage = mapContent.AddComponent<Image>();
            mapImage.sprite = Sprite.Create(mapTexture, new Rect(0, 0, mapTexture.width, mapTexture.height), Vector2.zero);
            mapImage.preserveAspect = true;

            // Scroll rect settings
            svScrollRect.content = content;
            svScrollRect.viewport = viewport;
            svScrollRect.horizontal = true;
            svScrollRect.vertical = true;
            svScrollRect.inertia = false;
            svScrollRect.movementType = ScrollRect.MovementType.Clamped;

            mapWindowInitialized = true;

            parentObject.SetActive(false);

            Log("Ingame drawed map added to hud");
        }

        [HarmonyPatch(typeof(Player), nameof(Player.Save))]
        public static class Player_Save_Patch
        {
            public static bool Prefix(Player __instance)
            {
                SaveMapToPlayer(__instance);
                return true;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.Load))]
        public static class Player_Load_Patch
        {
            public static void Postfix(Player __instance)
            {
                if (LoadMapFromPlayer(__instance))
                {
                    ResetViewerContentSize();
                    mapTextureIsReady = true;
                }
            }
        }

        [HarmonyPatch(typeof(Hud), nameof(Hud.Awake))]
        class Hud_Awake_Patch
        {
            static void Postfix(Hud __instance)
            {
                if (!modEnabled.Value)
                    return;

                if (GameObject.Find("NomapPrinter_Parent"))
                    return;

                AddIngameView(__instance.m_rootObject.transform); // Parent object is parent of HUD itself
            }
        }

        [HarmonyPatch(typeof(WorldGenerator), nameof(WorldGenerator.Initialize))]
        public static class WorldGenerator_Initialize_Patch
        {
            public static void Postfix(World world)
            {
                game_world = world;

                saveFieldKey = $"NomapPrinter_MapTexture_{world.m_name}";
            }
        }

        [HarmonyPatch(typeof(MapTable), nameof(MapTable.OnRead))]
        [HarmonyPriority(Priority.Last)]
        public static class MapTable_OnRead_Patch
        {
            static void Postfix()
            {
                if (!modEnabled.Value) 
                    return;

                if (!Game.m_noMap)
                    return;

                instance.ConfigUpdate();

                if (!instance.maker.working) 
                    instance.StartCoroutine(instance.maker.Go());
            }
        }

        private class NomapPrinter : MonoBehaviour
        {
            public bool working = false;
            private static Texture2D iconSpriteTexture;   // Current sprite texture is not readable. Saving a cached copy the first time the variable is accessed 
            private static readonly Dictionary<Vector3, string> pinsToPrint = new Dictionary<Vector3, string>();    // key - map position, value - icon name
            private static int iconSize = 16;  // 16 for normal size, 32 for double size
            private static bool saveToFile = false;

            public IEnumerator Go()
            {
                working = true;

                ShowMessage(messageStart.Value);

                yield return PrepareTerrainData();

                ShowMessage(messageSaving.Value);

                MapImageGeneration.Initialize(m_mapTexture, m_forestTexture, m_heightmap, m_fogmap, m_textureSize);

                MapImageGeneration imageGen = new MapImageGeneration();

                GetPinsToPrint();

                saveToFile = saveMapToFile.Value;

                switch (mapType.Value)
                {
                    case MapType.BirdsEye:
                        yield return imageGen.GenerateSatelliteImage();
                        break;
                    case MapType.Topographical:
                        yield return imageGen.GenerateTopographicalMap(8);
                        break;
                    case MapType.Chart:
                        yield return imageGen.GenerateChartMap(8);
                        break;
                    case MapType.OldChart:
                        yield return imageGen.GenerateOldMap(8);
                        break;
                    default:
                        goto case MapType.Chart;
                }

                SaveMapTexture(path, mapType.Value, imageGen.output);

                Log("Finished Map Draw");
                ShowMessage(messageReady.Value);

                MapImageGeneration.DeInitialize();

                m_mapTexture = null;
                m_forestTexture = null;
                m_heightmap = null;
                m_fogmap = null;

                working = false;
            }

            private IEnumerator PrepareTerrainData()
            {
                float m_pixelSize = Minimap.instance.m_pixelSize;
                int num = m_textureSize / 2;
                float num2 = m_pixelSize / 2f;
                Color32[] array = new Color32[m_textureSize * m_textureSize];
                Color32[] array2 = new Color32[m_textureSize * m_textureSize];
                Color32[] array3 = new Color32[m_textureSize * m_textureSize];
                Color32[] array4 = new Color32[m_textureSize * m_textureSize];
                heightBytes = new int[m_textureSize * m_textureSize];

                Texture2D fogTexture = (Texture2D)Minimap.instance.m_mapImageLarge.material.GetTexture("_FogTex");

                var internalThread = new Thread(() =>
                {
                    for (int i = 0; i < m_textureSize; i++)
                    {
                        for (int j = 0; j < m_textureSize; j++)
                        {
                            float wx = (float)(j - num) * m_pixelSize + num2;
                            float wy = (float)(i - num) * m_pixelSize + num2;
                            Heightmap.Biome biome = WorldGenerator.instance.GetBiome(wx, wy);
                            float biomeHeight = WorldGenerator.instance.GetBiomeHeight(biome, wx, wy, out Color _);
                            array2[i * m_textureSize + j] = GetMaskColor(wx, wy, biomeHeight, biome);

                            if (biomeHeight > abyss_depth)          //Are we on map?
                            {
                                //Height Arrays
                                float height = biomeHeight - m_waterLevel;  //Set zero point to sea level.
                                heightBytes[i * m_textureSize + j] = (int)height;
                                if (height > 0) array3[i * m_textureSize + j] = new Color(height / 512, 0f, 0f);
                                else array3[i * m_textureSize + j] = new Color(0f, 0f, height / (-256));

                                //Biome data
                                array[i * m_textureSize + j] = GetPixelColor(biome, biomeHeight);

                                //Exploration Data
                                if (fogTexture.GetPixel(j, i).r != 0f && fogTexture.GetPixel(j, i).g != 0f)       //Draw fogmap
                                    array4[i * m_textureSize + j] = Color.gray;
                                else
                                    array4[i * m_textureSize + j] = Color.clear;

                            }
                            else
                            {   //Do this when off map
                                array[i * m_textureSize + j] = Color.black;
                            }
                        }
                    }
                });

                internalThread.Start();
                while (internalThread.IsAlive == true)
                {
                    yield return null;
                }

                //save values
                m_mapTexture = array;
                m_forestTexture = array2;
                m_heightmap = array3;
                m_fogmap = array4;

            }

            private static void SaveMapTexture(string path, MapType mapType, Color32[] map)
            {
                string filename = Path.Combine(path, $"{mapType} map of {game_world.m_name}.png");

                // File size increase is not so much, better overall details, required for viewable icons 16x16, required for ingame map
                // If someone will be asking for smaller resolution just make this line optional via config
                DoubleMapSize(ref map, out int mapSize);

                // If someone want even bigger, most smooth icons.
                if (doubleTheSize.Value)
                {
                    DoubleMapSize(ref map, out mapSize);
                }

                if (showPins.Value)
                {
                    AddPinsOnMap(map, mapType, mapSize);
                }

                mapTexture.Resize(mapSize, mapSize);
                mapTexture.SetPixels32(map);
                mapTexture.Apply();

                ResetViewerContentSize();

                mapTextureIsReady = true;

                if (saveToFile)
                {
                    Log($"Writing {filename}");
                    File.WriteAllBytes(filename, ImageConversion.EncodeToPNG(mapTexture));
                    ShowMessage($"{messageSavedTo.Value} {path}", MessageHud.MessageType.TopLeft);
                }
            }

            private static void DoubleMapSize(ref Color32[] map, out int mapSize)
            {
                int currentMapSize = (int)Math.Sqrt(map.Length);
                mapSize = currentMapSize * 2;

                Color32[] doublemap = new Color32[mapSize * mapSize];

                for (int row = 0; row < currentMapSize; row++)
                {
                    for (int col = 0; col < currentMapSize; col++)
                    {
                        Color32 pix = map[row * currentMapSize + col];
                        doublemap[row * 2 * mapSize + col * 2] = pix;
                        doublemap[row * 2 * mapSize + col * 2 + 1] = pix;
                        doublemap[(row * 2 + 1) * mapSize + col * 2] = pix;
                        doublemap[(row * 2 + 1) * mapSize + col * 2 + 1] = pix;
                    }
                }

                map = doublemap;
            }

            private static Color GetPixelColor(Heightmap.Biome biome, float height)
            {
                if (height < abyss_depth)
                    return Color.black;

                switch (biome)
                {
                    case Heightmap.Biome.Meadows:
                        return Minimap.instance.m_meadowsColor;
                    case Heightmap.Biome.AshLands:
                        return Minimap.instance.m_ashlandsColor;
                    case Heightmap.Biome.BlackForest:
                        return Minimap.instance.m_blackforestColor;
                    case Heightmap.Biome.DeepNorth:
                        return Minimap.instance.m_deepnorthColor;
                    case Heightmap.Biome.Plains:
                        return Minimap.instance.m_heathColor;
                    case Heightmap.Biome.Swamp:
                        return Minimap.instance.m_swampColor;
                    case Heightmap.Biome.Mountain:
                        return Minimap.instance.m_mountainColor;
                    case Heightmap.Biome.Mistlands:
                        return Minimap.instance.m_mistlandsColor;
                    case Heightmap.Biome.Ocean:
                        return Color.blue;
                    default:
                        return Color.black;
                }
            }

            private static Color GetMaskColor(float wx, float wy, float height, Heightmap.Biome biome)
            {
                Color noForest = new Color(0f, 0f, 0f, 0f);
                Color forest = new Color(1f, 0f, 0f, 0f);

                if (height < m_waterLevel)
                {
                    return noForest;
                }

                switch (biome)
                {
                    case Heightmap.Biome.Meadows:
                        return !WorldGenerator.InForest(new Vector3(wx, 0.0f, wy)) ? noForest : forest;
                    case Heightmap.Biome.Plains:
                        return !(WorldGenerator.GetForestFactor(new Vector3(wx, 0f, wy)) < 0.8f) ? noForest : forest;
                    case Heightmap.Biome.BlackForest:
                    case Heightmap.Biome.Mistlands:
                        return forest;
                    default:
                        return noForest;
                }
            }

            private static void AddPinsOnMap(Color32[] map, MapType mapType, int mapSize)
            {
                foreach (KeyValuePair<Vector3, string> pin in pinsToPrint)
                {
                    // get position in relative float instead of vector
                    Minimap.instance.WorldToMapPoint(pin.Key, out float mx, out float my);

                    // filter icons outside of map circle
                    if (mx >= 1 || my >= 1 || mx <= 0 || my <= 0)
                    {
                        continue;
                    }

                    Color32[] iconPixels = pinIcons[pin.Value];
                    if (iconPixels != null)
                    {
                        // get icon position in array
                        int iconmx = Math.Max((int)(mx * mapSize) - (iconSize / 2), 0);
                        int iconmy = Math.Max((int)(my * mapSize) - (iconSize / 2), 0);

                        // overlay icon pixels to map array with lerp
                        for (int row = 0; row < iconSize; row++)
                        {
                            for (int col = 0; col < iconSize; col++)
                            {
                                int pos = (iconmy + row) * mapSize + iconmx + col;

                                Color32 iconPix = iconPixels[row * iconSize + col];
                                if (mapType == MapType.Chart || mapType == MapType.OldChart)
                                {
                                    // add yellow tint of chart maps, one iteration is enough for OldChart
                                    iconPix = Color32.Lerp(iconPix, MapImageGeneration.yellowMap, 0.33f * iconPix.a / 255f);
                                }

                                map[pos] = Color32.Lerp(map[pos], iconPix, iconPix.a / 255f); // alpha is relative for more immersive effect
                                map[pos].a = byte.MaxValue;  // make opaque again
                            }
                        }
                    }
                }
            }

            private static void GetPinsToPrint()
            {
                pinsToPrint.Clear();

                if (!showPins.Value)
                    return;

                if (Minimap.instance == null)
                    return;

                foreach (Minimap.PinData pin in Minimap.instance.m_pins)
                {
                    if (!showEveryPin.Value)
                    {
                        if (showNonCheckedPins.Value && pin.m_checked)
                            continue;

                        if (showMyPins.Value && pin.m_ownerID != 0L)
                            continue;

                        if (showExploredPins.Value && !Minimap.instance.IsExplored(pin.m_pos))
                            if (!IsMerchantPin(pin.m_icon.name) || !showMerchantPins.Value)
                                continue;
                    }

                    if (IsShowablePinIcon(pin))
                        pinsToPrint.Add(pin.m_pos, pin.m_icon.name);
                }
            }

            private static bool IsIconConfiguredShowable(string pinIcon)
            {
                if (showEveryPin.Value)
                    return true;

                switch (pinIcon)
                {
                    case "mapicon_boss_colored":
                        return showPinBoss.Value;
                    case "mapicon_fire":
                        return showPinFire.Value;
                    case "mapicon_hammer":
                        return showPinHammer.Value;
                    case "mapicon_hildir":
                        return showPinHildir.Value;
                    case "mapicon_hildir1":
                        return showPinHildirQuest.Value;
                    case "mapicon_hildir2":
                        return showPinHildirQuest.Value;
                    case "mapicon_hildir3":
                        return showPinHildirQuest.Value;
                    case "mapicon_house":
                        return showPinHouse.Value;
                    case "mapicon_pin":
                        return showPinPin.Value;
                    case "mapicon_portal":
                        return showPinPortal.Value;
                    case "mapicon_start":
                        return showPinStart.Value;
                    case "mapicon_trader":
                        return showPinTrader.Value;
                    case "mapicon_bed":
                        return showPinBed.Value;
                    case "mapicon_death":
                        return showPinDeath.Value;
                }

                return false;
            }

            private static bool IsMerchantPin(string pinIcon)
            {
                switch (pinIcon)
                {
                    case "mapicon_hildir":
                        return true;
                    case "mapicon_hildir1":
                        return true;
                    case "mapicon_hildir2":
                        return true;
                    case "mapicon_hildir3":
                        return true;
                    case "mapicon_trader":
                        return true;
                }

                return false;
            }

            private static bool IsShowablePinIcon(Minimap.PinData pin)
            {
                if (pin.m_icon == null)
                    return false;

                bool showIcon = IsIconConfiguredShowable(pin.m_icon.name);

                if (showIcon && !pinIcons.ContainsKey(pin.m_icon.name) && !AddPinIconToCache(pin.m_icon))
                    return false;

                return showIcon;
            }

            private static bool AddPinIconToCache(Sprite icon)
            {
                Color32[] iconPixels = GetIconPixels(icon, iconSize, iconSize);

                if (iconPixels.Length == 1)
                    return false;

                pinIcons.Add(icon.name, iconPixels);
                return true;
            }

            private static Color32[] GetIconPixels(Sprite icon, int targetX, int targetY)
            {
                Texture2D texture2D = GetTextureFromSprite(icon);

                RenderTexture tmp = RenderTexture.GetTemporary(
                                                    targetX,
                                                    targetY,
                                                    24,
                                                    RenderTextureFormat.Default,
                                                    RenderTextureReadWrite.Default);

                Graphics.Blit(texture2D, tmp);
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = tmp;

                Texture2D result = new Texture2D(targetX, targetY);
                result.ReadPixels(new Rect(0, 0, targetX, targetY), 0, 0);
                result.Apply();

                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(tmp);

                Color32[] iconPixels = result.GetPixels32();

                // textures is small, called once per caching icon
                Destroy(texture2D);
                Destroy(result);

                return iconPixels;
            }

            private static Texture2D GetTextureFromSprite(Sprite sprite)
            {

                if (sprite.rect.width != sprite.texture.width)
                {
                    int texWid = (int)sprite.rect.width;
                    int texHei = (int)sprite.rect.height;
                    Texture2D newTex = new Texture2D(texWid, texHei);
                    Color[] defaultPixels = Enumerable.Repeat<Color>(new Color(0, 0, 0, 0), texWid * texHei).ToArray();
                    Color[] pixels = GetIconSpriteTexture(sprite.texture).GetPixels((int)sprite.textureRect.x
                                                                                  , (int)sprite.textureRect.y
                                                                                  , (int)sprite.textureRect.width
                                                                                  , (int)sprite.textureRect.height);

                    newTex.SetPixels(defaultPixels);
                    newTex.SetPixels((int)sprite.textureRectOffset.x, (int)sprite.textureRectOffset.y, (int)sprite.textureRect.width, (int)sprite.textureRect.height, pixels);
                    newTex.Apply();
                    return newTex;
                }
                else
                {
                    // not relevant for now, but for compat means
                    return sprite.texture;
                }
            }

            private static Texture2D GetIconSpriteTexture(Texture2D texture2D)
            {
                if (iconSpriteTexture == null)
                    iconSpriteTexture = GetReadableTexture(texture2D);

                return iconSpriteTexture;
            }

            private static Texture2D GetReadableTexture(Texture2D texture)
            {
                RenderTexture tmp = RenderTexture.GetTemporary(
                                                    texture.width,
                                                    texture.height,
                                                    24,
                                                    RenderTextureFormat.Default,
                                                    RenderTextureReadWrite.Default);

                // Blit the pixels on texture to the RenderTexture
                Graphics.Blit(texture, tmp);

                // Backup the currently set RenderTexture
                RenderTexture previous = RenderTexture.active;

                // Set the current RenderTexture to the temporary one we created
                RenderTexture.active = tmp;

                // Create a new readable Texture2D to copy the pixels to it
                Texture2D textureCopy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, true, false);

                // Copy the pixels from the RenderTexture to the new Texture
                textureCopy.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
                textureCopy.Apply();

                // Reset the active RenderTexture
                RenderTexture.active = previous;

                // Release the temporary RenderTexture
                RenderTexture.ReleaseTemporary(tmp);

                // "textureCopy" now has the same pixels from "texture" and it's readable
                Texture2D newTexture = new Texture2D(texture.width, texture.height);
                newTexture.SetPixels(textureCopy.GetPixels());
                newTexture.Apply();

                return newTexture;
            }

            public static void InitIconSize()
            {
                int newSize = (doubleTheSize.Value) ? 32 : 16;

                if (iconSize == newSize)
                    return;

                pinIcons.Clear(); // Need to rebuild icon cache
                iconSize = newSize;
            }

        }

        public sealed class ValueChangedEventArgs<TValue> : EventArgs
        {
            /// <inheritdoc />
            public ValueChangedEventArgs(TValue newValue)
            {
                NewValue = newValue;
            }
            /// <summary>
            /// Newly assigned value
            /// </summary>
            public TValue NewValue { get; }
        }

    }
}
