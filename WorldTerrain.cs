using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Diagnostics;
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
    public class WorldTerrain
    {
        private readonly int     _mapScale;
        private readonly int     _mapFetchersCount;
        private readonly bool    _mapFetchOnlyExplored;
        private readonly bool    _mapUseWorldRadius;
        private readonly bool    _showSharedMap;

        private readonly float   _abyssBiomeHeight = 100.0f;
        private readonly int[]   _slicesOffset = new int[]   {0, 1, 3, 6, 10, 15, 21, 28};  // [8]
        private readonly float[] _slices       = new float[] {
            100.00f,
             50.00f, 50.00f,
             36.75f, 26.49f, 36.75f,
             29.80f, 20.20f, 20.20f, 29.80f,
             25.41f, 16.71f, 15.77f, 16.71f, 25.41f,
             22.34f, 14.42f, 13.25f, 13.25f, 14.42f, 22.34f,
             20.05f, 12.78f, 11.55f, 11.24f, 11.55f, 12.78f, 20.05f,
             18.26f, 11.54f, 10.32f,  9.88f,  9.88f, 10.32f, 11.54f, 18.26f
        };

        private readonly Color32 _abyssColor       = Color.black;
        private readonly Color32 _meadowsColor     = new Color32(146, 167,  92, 255);
        private readonly Color32 _swampColor       = new Color32(163, 114,  88, 255);
        private readonly Color32 _mountainColor    = new Color32(255, 255, 255, 255);
        private readonly Color32 _blackForestColor = new Color32(107, 116,  63, 255);
        private readonly Color32 _plainsColor      = new Color32(231, 171, 120, 255);
        private readonly Color32 _ashLandsColor    = new Color32(176,  49,  49, 255);
        private readonly Color32 _deepNorthColor   = new Color32(230, 230, 255, 255);  // Blueish color
        private readonly Color32 _oceanColor       = new Color32(100, 110, 140, 255);
        private readonly Color32 _mistLandsColor   = new Color32(153, 102, 153, 255);
        private readonly Color32 _clearColor       = Color.clear;
        private readonly Color32 _noForestColor    = Color.clear;
        private readonly Color32 _forestColor      = Color.red;

        private bool      _isFetched;
        private int       _textureSize;
        private int       _maxAltitude = int.MinValue;
        private int       _minAltitude = int.MaxValue;
        private int       _radius      = 0;

        private Color32[] _biomes;
        private Color32[] _forest;
        private Color32[] _altitudes;
        private Color32[] _explored;

        private sealed class FetcherContext
        {
            public Thread thread;
            public int    maxAltitude = int.MinValue;
            public int    minAltitude = int.MinValue;
            public int    chunkBegin;
            public int    chunkEnd;
        }

        public WorldTerrain(int mapScale, int fetchersCount, bool mapFetchOnlyExplored, bool useWorldRadius, Color32[] explored = null)
        {
            _mapScale             = mapScale;
            _mapFetchersCount     = fetchersCount > 8 ? 8 : (fetchersCount < 1 ? 1 : fetchersCount);
            _mapFetchOnlyExplored = mapFetchOnlyExplored;
            _mapUseWorldRadius    = useWorldRadius;
            //_showSharedMap        = showSharedMap;
            _explored             = explored;
        }

        private static void Log(string message)
        {
            //
        }

        private Color32 GetBiomeColor(Heightmap.Biome biome)
        {
            //public enum Biome
            //{
            //    None        =   0,
            //    Meadows     =   1,
            //    Swamp       =   2,
            //    Mountain    =   4,
            //    BlackForest =   8,
            //    Plains      =  16,  // 0x00000010
            //    AshLands    =  32,  // 0x00000020
            //    DeepNorth   =  64,  // 0x00000040
            //    Ocean       = 256,  // 0x00000100
            //    Mistlands   = 512   // 0x00000200
            //}

            switch (biome)
            {
                case Heightmap.Biome.None:        return _abyssColor;
                case Heightmap.Biome.Meadows:     return _meadowsColor;
                case Heightmap.Biome.Swamp:       return _swampColor;
                case Heightmap.Biome.Mountain:    return _mountainColor;
                case Heightmap.Biome.BlackForest: return _blackForestColor;
                case Heightmap.Biome.Plains:      return _plainsColor;
                case Heightmap.Biome.AshLands:    return _ashLandsColor;
                case Heightmap.Biome.DeepNorth:   return _deepNorthColor;
                case Heightmap.Biome.Ocean:       return _oceanColor;
                case Heightmap.Biome.Mistlands:   return _mistLandsColor;
                default:
                    return _abyssColor;
            }
        }

        private Color32 GetForestColor(float wx, float wy, int altitude, Heightmap.Biome biome)
        {
            if (altitude <= 0)
                return _noForestColor;

            switch (biome)
            {
                case Heightmap.Biome.Meadows:
                    return WorldGenerator.InForest(new Vector3(wx, 0.0f, wy)) ? _forestColor : _noForestColor;
                case Heightmap.Biome.Plains:
                    return WorldGenerator.GetForestFactor(new Vector3(wx, 0f, wy)) < 0.8f ? _forestColor : _noForestColor;
                case Heightmap.Biome.BlackForest:
                    return _forestColor;
                case Heightmap.Biome.None:
                case Heightmap.Biome.Swamp:
                case Heightmap.Biome.Mountain:
                case Heightmap.Biome.AshLands:
                case Heightmap.Biome.DeepNorth:
                case Heightmap.Biome.Ocean:
                case Heightmap.Biome.Mistlands:
                default:
                    return _noForestColor;
            }
        }

        private Heightmap.Biome CastColorAsBiome(Color32 color)
        {
            // TODO: replace this horror with more optimal code - get rid of explicit colors for biomes and unite them with heights and forests
            // rgba::= ((altitude + 1000) << 5) + (forest << 4) + biomeId[0..10]
            if (color.rgba == _abyssColor.rgba      ) return Heightmap.Biome.None;
            if (color.rgba == _meadowsColor.rgba    ) return Heightmap.Biome.Meadows;
            if (color.rgba == _swampColor.rgba      ) return Heightmap.Biome.Swamp;
            if (color.rgba == _mountainColor.rgba   ) return Heightmap.Biome.Mountain;
            if (color.rgba == _blackForestColor.rgba) return Heightmap.Biome.BlackForest;
            if (color.rgba == _plainsColor.rgba     ) return Heightmap.Biome.Plains;
            if (color.rgba == _ashLandsColor.rgba   ) return Heightmap.Biome.AshLands;
            if (color.rgba == _deepNorthColor.rgba  ) return Heightmap.Biome.DeepNorth;
            if (color.rgba == _oceanColor.rgba      ) return Heightmap.Biome.Ocean;
            if (color.rgba == _mistLandsColor.rgba  ) return Heightmap.Biome.Mistlands;

            return Heightmap.Biome.None;
        }

        // TODO: also we have explicit fields (with the precision of Minimap.instance.m_textureSize):
        //   bool[] Minimap.instance.m_explored;
        //   bool[] Minimap.instance.m_exploredOthers;
        private bool IsExplored(int x, int y)
        {
            Color explorationPos = Minimap.instance.m_fogTexture.GetPixel(x / _mapScale, y / _mapScale);
            return explorationPos.r == 0f || _showSharedMap && explorationPos.g == 0f;
        }

        /// out:
        ///   m_mapTexture    + _biomesMap.Colors
        ///   m_forestTexture + _forestMap.Colors
        ///   m_heightmap     + _heightMap.Colors + _altitudes
        ///   m_exploration   + _explored
        ///   m_mapData
        ///
        /// TODO:
        ///   + calculate abyss area without scanning the whole map (e.g. binary lookup for the worlds radius)
        ///   + support multithreading: we can split map for horizontal chunks
        ///   - combine m_exploration and m_mapData in single Color32[] array like it is made for others and self explored areas
        ///   - combine m_mapTexture, m_forestTexture and m_heightmap (we can go deeper and even combine them with m_exploration and m_mapData)
        ///
        private IEnumerator InternalFetch()
        {
            //Texture2D fogTexture      = (Texture2D)Minimap.instance.m_mapImageLarge.material.GetTexture("_FogTex");
            _textureSize    = _mapScale * Minimap.instance.m_textureSize;

            float     pixelSize       = Minimap.instance.m_pixelSize / _mapScale;
            float     halfPixelSize   = pixelSize / 2;
            int       halfTextureSize = _textureSize / 2;
            int       arraySize       = _textureSize * _textureSize;
            float     waterLevel      = ZoneSystem.instance.m_waterLevel;  // = 30f by default
            int       abyssAltitude   = (int)(_abyssBiomeHeight - waterLevel);
            int       radius2         = 0;

            _biomes    = new Color32[arraySize];
            _forest    = new Color32[arraySize];
            _altitudes = new Color32[arraySize];
            _explored  = new Color32[arraySize];

            //bool[]    mapData     = _mapFetchOnlyExplored ? new bool[arraySize] : null;

            Func<int, float, float, Heightmap.Biome> getBiome = (Func<int, float, float, Heightmap.Biome>)((n, wx, wy) =>
                WorldGenerator.instance.GetBiome(wx, wy));

            Func<int, Heightmap.Biome, Color32> getBiomeColor = (Func<int, Heightmap.Biome, Color32>)((n, biome) =>
                GetBiomeColor(biome));

            Func<int, float, float, Heightmap.Biome, int> getAltitude = (n, wx, wy, biome) =>
            {
                float altitude = WorldGenerator.instance.GetBiomeHeight(biome, wx, wy, out Color mask) - waterLevel;
                int result = altitude >= 0 ? (int)altitude + 1 : (int)altitude;
                if (result <= abyssAltitude)
                    return abyssAltitude - 1;

                return result;
            };

            Func<int, float, float, int, Heightmap.Biome, Color32> getForest = (n, wx, wy, altitude,  biome) =>
                GetForestColor(wx, wy, altitude, biome);

            //*/
            if (_mapUseWorldRadius)
            {
                int it = halfTextureSize * _textureSize;
                int j0 = halfTextureSize;
                int j1 = _textureSize - 1;

                int GetMapAltitude(int dj)
                {
                    const float wy = 0;

                    float wx = (dj - halfTextureSize) * pixelSize + halfPixelSize; // = halfPixelSize;
                    int   n  = it + dj;

                    Heightmap.Biome biome = getBiome(n, wx, wy); // WorldGenerator.instance.GetBiome(wx, wy);

                    return getAltitude(n, wx, wy, biome);
                }

                while (j1 - j0 > 1)
                {
                    int j = (j1 + j0) / 2;
                    int h = GetMapAltitude(j);

                    if (h > abyssAltitude)
                    {
                        Log($"[d] radius lookup: j0 = {j0}, j1 = {j1}, j = {j}, h = {h}, j0 -> j");
                        j0 = j;
                    }
                    else
                    {
                        Log($"[d] radius lookup: j0 = {j0}, j1 = {j1}, j = {j}, h = {h}, j1 -> j");
                        j1 = j;
                    }
                }

                _radius = j1 - halfTextureSize + 1;
                radius2 = _radius * _radius;
            }
            //*/

            Action<FetcherContext> getMapChunkData = (FetcherContext context) => //void GetMapChunkData(int iBegin, int iEnd)
            {
                for (int i = context.chunkBegin, ti = context.chunkBegin * _textureSize; i < context.chunkEnd; ++i, ti += _textureSize)
                {
                    int wi = i - halfTextureSize;
                    float wy = wi * pixelSize + halfPixelSize;

                    for (int j = 0, n = ti; j < _textureSize; ++j, ++n)
                    {
                        if (_explored != null && _explored[n].rgba == 0)
                            continue;

                        int   wj = j - halfTextureSize;
                        float wx = wj * pixelSize + halfPixelSize;

                        // All is bad: the 'abyss' does not stand out among biomes - you always need a height
                        Heightmap.Biome biome    = Heightmap.Biome.None; // = getBiome(n, wx, wy);
                        int             altitude = abyssAltitude - 1; // = getAltitude(n, wx, wy, biome);

                        if (radius2 <= 0 || radius2 >= wj * wj + wi * wi)
                        {
                            biome    = getBiome(n, wx, wy);
                            altitude = getAltitude(n, wx, wy, biome);
                        }

                        if (context.maxAltitude < altitude)
                            context.maxAltitude = altitude;

                        if (context.minAltitude > altitude)
                            context.minAltitude = altitude;

                        if (altitude <= abyssAltitude)
                        {
                            biome = Heightmap.Biome.None;
                            // abyss => no forest, no forest => no color
                        }
                        else
                        {
                            _forest[n] = getForest(n, wx, wy, altitude, biome); //GetForestColor(wx, wy, altitude, biome);
                        }

                        _biomes[n] = getBiomeColor(n, biome);

                        _altitudes[n].rgba = altitude;

                        // ! Minimap logic shows that biome height < waterLevel is considered as water area
                        //   => altitude >= 0 should be a land

                        // Fog map and exploration data
                        if (!_mapFetchOnlyExplored && altitude > abyssAltitude)
                        {
                            bool isExplored = IsExplored(j, i);
                            _explored[n] = isExplored ? _forestColor : _clearColor;
                        }
                    } // for (int j = 0, n = ti; j < textureSize; ++j, ++n)
                } // for (int i = 0, ti = 0; i < textureSize; ++i, ti += textureSize)
            };  // void GetMapChunkData(int jBegin, int jEnd)

            FetcherContext[] fetchers = new FetcherContext[_mapFetchersCount];

            if (_radius <= 0)
            {
                int chunkSize  = _textureSize / _mapFetchersCount;

                int chunkBegin = 0;
                int chunkEnd   = chunkSize;

                if (_textureSize - chunkEnd < chunkSize)
                    chunkEnd = _textureSize;

                for (int n = 0; chunkEnd <= _textureSize && n < _mapFetchersCount; ++n)
                {
                    FetcherContext context = new FetcherContext() {chunkBegin = chunkBegin, chunkEnd = chunkEnd};
                    fetchers[n] = context;
                    context.thread = new Thread(() => getMapChunkData(context));
                    context.thread.Start();

                    chunkBegin = chunkEnd;
                    chunkEnd   = chunkBegin + chunkSize;
                    if (chunkEnd < _textureSize && _textureSize - chunkEnd < chunkSize)
                        chunkEnd = _textureSize;
                }
            }
            else
            {
                int offset     = _slicesOffset[_mapFetchersCount - 1];
                int diameter   = _radius + _radius;

                int chunkBegin = 0;
                int chunkEnd   = halfTextureSize - _radius + (int)(diameter * _slices[offset] / 100f);

                for (int n = 0; n < _mapFetchersCount - 1; ++n)
                {
                    FetcherContext context = new FetcherContext() {chunkBegin = chunkBegin, chunkEnd = chunkEnd};
                    fetchers[n] = context;
                    context.thread = new Thread(() => getMapChunkData(context));
                    context.thread.Start();

                    int h = (int)(diameter * _slices[offset + n + 1] / 100f);

                    chunkBegin = chunkEnd;
                    chunkEnd   = chunkBegin + h;
                }

                chunkEnd = _textureSize;

                {
                    FetcherContext context = new FetcherContext() { chunkBegin = chunkBegin, chunkEnd = chunkEnd };
                    fetchers[_mapFetchersCount - 1] = context;
                    context.thread = new Thread(() => getMapChunkData(context));
                    context.thread.Start();
                }
            }

            for (int n = 0; n < _mapFetchersCount; ++n)
            {
                FetcherContext fetcher = fetchers[n];
                while (fetcher.thread.IsAlive)
                {
                    yield return null;
                }

                if (_maxAltitude < fetcher.maxAltitude)
                    _maxAltitude = fetcher.maxAltitude;

                if (_minAltitude > fetcher.minAltitude)
                    _minAltitude = fetcher.minAltitude;
            }

            //m_mapData       = mapData;

            // X::

            //if (_biomesMap.IsEmpty())
            //{
            //    _biomesMap.Colors = biomeColors;
            //}
            //
            //if (_heightMap.IsEmpty())
            //{
            //    _altitudes        = altitudes;
            //}
            //
            //if (_forestMap.IsEmpty())
            //{
            //    _forestMap.Colors = forest;
            //}
            //
            //_explored = explored;
        }

        public IEnumerator Fetch()
        {
            if (!_isFetched)
            {
                yield return InternalFetch();
                _isFetched = true;
            }

            yield return null;
        }
    }
}