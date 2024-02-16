using System.Collections;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System;
using UnityEngine;


public class Mapmaker
{
    //private static readonly Color32 ChartMapColor = new Color32(203, 155,  87, byte.MaxValue);
    //private static readonly Color32 OceanColor    = new Color32( 20, 100, 255, byte.MaxValue);
    private static readonly Color32 FogColor      = new Color32(203, 155,  87, byte.MaxValue);

    private readonly int       _mapSize;
    private int                _mapPixelCount;

    private Color32   _abyssColor = new Color32(  0,   0,   0, byte.MaxValue);
    //private Color32   _oceanColor = new Color32( 20, 100, 255, byte.MaxValue);
    private Color32   _oceanColor  = new Color32( 10, 136, 193, byte.MaxValue);

    //private Color32[] _scenery;       // -> World
    //private Color32[] _contours;
    //private Color32[] _fog;
    //private Color32[] _fogTexture;    //
    //private Color32[] _abyss;
    //private Color32[] _abyssTexture;  //

    private readonly Color32[] _biomes;
    private readonly Color32[] _heights;
    private readonly Color32[] _forest;
    private readonly Color32[] _explored;

    private Color32[] _fogTexture;
    private Color32[] _waterTexture;

    private readonly int       _contourInterval;
    private readonly int       _isobathInterval;

    //public Color32[] World;        // entire world map
    public Color32[] WorldMap;     // entire world map with contours
    public Color32[] ExploredMap;  // explored map with contours
    //public Color32[] WorldMask;

    //private static string _trace;
    private static readonly StringBuilder _trace = new StringBuilder("");


    public Mapmaker(int mapSize, Color32[] biomes, Color32[] heights, Color32[] forest, Color32[] explored, int contourInterval, int isobathInterval)
    {
        _mapSize         = mapSize;
        _biomes          = biomes;
        _heights         = heights;
        _forest          = forest;
        _explored        = explored;
        _contourInterval = contourInterval;
        _isobathInterval = isobathInterval;

        _mapPixelCount   = _mapSize * _mapSize;

        _trace.Clear();
        _trace.Append($"Mapmaker(): _mapSize = {_mapSize}, _mapPixelCount = {_mapPixelCount}, _contourInterval = {_contourInterval}\n");
    }


    //~Mapmaker()
    //{
    //    _biomes   = null;
    //    _heights  = null;
    //    _forest   = null;
    //    _explored = null;
    //}

    public Color32 AbyssColor
    {
        get => _abyssColor;
        set
        {
            if (_abyssColor.rgba != value.rgba)
            {
                _abyssColor = value;
                // TODO: invalidate
            }
        }
    }


    public Color32 OceanColor
    {
        get => _oceanColor;
        set => _oceanColor.rgba = value.rgba;
    }


    public Color32[] FogTexture
    {
        set => _fogTexture = value;
    }

    public Color32[] WaterTexture
    {
        set => _waterTexture = value;
    }


    public static string Trace()
    {
        return _trace.ToString();
    }

    public void RenderTopographicalMap()
    {
        Color32[] mask           = _biomes;      // TODO: use selector to render only explored map area
        Color32   maskClearColor = _abyssColor;

        _trace.Append($"-> RenderTopographicalMap()\n");

        // _trace.Append($"--   FillWithColor()\n");
        // Color32[] fogMask = FillWithColor(null, _biomes, _abyssColor, _explored, Color.clear);

        if (WorldMap == null)
        {
            _trace.Append($"--   ReplaceColor()\n");
            Color32[] canvas = ReplaceColor(null, _biomes, _abyssColor, Color.white);

            _trace.Append($"--   RenderWater()\n");
            canvas = RenderWater(canvas, _heights, mask, maskClearColor, _mapSize, 4, 8, 1.0f);

            _trace.Append($"--   DarkenLinear()\n");
            canvas = DarkenLinear(canvas, canvas, 20, mask, maskClearColor);

            _trace.Append($"--   DarkenRelative()\n");
            canvas = DarkenRelative(canvas, canvas, 0.85f, _forest, Color.clear);

            if (_contourInterval > 0 || _isobathInterval > 0)
            {
                _trace.Append("--   RenderContours()\n");
                canvas = RenderContours(canvas, _heights, _contourInterval, _isobathInterval, 128, mask, maskClearColor, _mapSize);
            }

            _trace.Append("--   WorldMap = canvas\n");
            WorldMap = canvas;
        }

        if (_fogTexture == null)
        {
            _trace.Append("--   ExploredMap = RenderFog()\n");
            ExploredMap = RenderFog(null, WorldMap, _explored, mask, maskClearColor, _mapSize, 128, 16);
            // TODO: ExploredMap = RenderPerlinNoise(null, WorldMap, FogColor, fogMask, _abyssColor, 128, 16);
        }
        else
        {
            _trace.Append("--   ExploredMap = RenderFogTexture()\n");
            ExploredMap = RenderFogTexture(null, WorldMap, _explored, mask, maskClearColor);
            // ExploredMap = RenderTexture(null, WorldMap, _fogTexture, fogMask, _abyssColor);
        }

        _trace.Append($"<- RenderTopographicalMap()\n");
    }


    public void RenderTopographicalMapLegacy()
    {
        Color32[] mask           = _biomes;     // TODO: use selector
        Color32   maskClearColor = _abyssColor;

        _trace.Append($"-> RenderTopographicalMapLegacy()\n");

        if (WorldMap == null)
        {
            _trace.Append($"--   ReplaceColor()\n");
            Color32[] canvas = ReplaceColor(null, _biomes, _abyssColor, Color.white);

            _trace.Append($"--   RenderWater()\n");
            canvas = RenderWater(canvas, _heights, mask, maskClearColor, _mapSize, 4, 4, 0.5f);

            _trace.Append($"--   DarkenLinear()\n");
            canvas = DarkenLinear(canvas, canvas, 20, mask, maskClearColor);

            _trace.Append($"--   DarkenRelative()\n");
            canvas = DarkenRelative(canvas, canvas, 0.85f, _forest, Color.clear);

            if (_contourInterval > 0)
            {
                _trace.Append("--   RenderContoursLegacy()\n");
                Color32[] contours = RenderContoursLegacy(null, _heights, _contourInterval, 128, mask, maskClearColor);

                _trace.Append("--   Blend()\n");
                canvas = Blend(canvas, contours, null);
            }

            _trace.Append("--   WorldMap = canvas\n");
            WorldMap = canvas;
        }

        _trace.Append("--   ExploredMap = RenderFog()\n");
        ExploredMap = RenderFog(null, WorldMap, _explored, mask, maskClearColor, _mapSize, 128, 16);

        _trace.Append($"<- RenderTopographicalMapLegacy()\n");
    }


    public void RenderInkyMap()
    {
        Color32[] mask           = _biomes;      // TODO: use selector to render only explored map area
        Color32   maskClearColor = _abyssColor;

        _trace.Append($"-> RenderTopographicalMap()\n");

        // _trace.Append($"--   FillWithColor()\n");
        // Color32[] fogMask = FillWithColor(null, _biomes, _abyssColor, _explored, Color.clear);

        if (WorldMap == null)
        {
            _trace.Append($"--   RenderWater()\n");
            Color32[] water = RenderWater(null, _heights, mask, maskClearColor, _mapSize, 0, 0, 1.0f);

            _trace.Append($"--   ReplaceColor(abyss -> white)\n");
            Color32[] canvas = ReplaceColor(null, _biomes, _abyssColor, Color.white);

            //_trace.Append($"--   RenderWater()\n");
            //canvas = RenderWater(canvas, _heights, mask, maskClearColor, _mapSize, 4, 8, 1.0f);

            _trace.Append("--   Blend(water)\n");
            canvas = Blend(canvas, water, mask, maskClearColor);

            if (_waterTexture != null)
            {
                _trace.Append("--   RenderTexture(waterTexture)\n");
                canvas = RenderTexture(canvas, canvas, _waterTexture, water, Color.clear);
            }

            Color32 deepColor = new Color32(
                (byte)(_oceanColor.r < 196 ? _oceanColor.r + 60 : 255),
                (byte)(_oceanColor.g < 196 ? _oceanColor.g + 60 : 255),
                (byte)(_oceanColor.b < 196 ? _oceanColor.b + 60 : 255),
                255
            );

            _trace.Append("--   RenderHeightLayer(-8, -100)\n");
            canvas = RenderHeightLayer(canvas, _heights, -8, -100, deepColor, (h) => (8 - h)*8, mask, maskClearColor, _mapSize, 0, 0, 1.0f);

            _trace.Append($"--   DarkenLinear()\n");
            canvas = DarkenLinear(canvas, canvas, 20, mask, maskClearColor);

            _trace.Append($"--   DarkenRelative(forest)\n");
            canvas = DarkenRelative(canvas, canvas, 0.85f, _forest, Color.clear);

            if (_contourInterval > 0 || _isobathInterval > 0)
            {
                _trace.Append("--   RenderContours()\n");
                canvas = RenderContours(canvas, _heights, _contourInterval, _isobathInterval, 128, mask, maskClearColor, _mapSize);
            }

            _trace.Append("--   WorldMap = canvas\n");
            WorldMap = canvas;
        }

        if (_fogTexture == null)
        {
            _trace.Append("--   ExploredMap = RenderFog()\n");
            ExploredMap = RenderFog(null, WorldMap, _explored, mask, maskClearColor, _mapSize, 128, 16);
            // TODO: ExploredMap = RenderPerlinNoise(null, WorldMap, FogColor, fogMask, _abyssColor, 128, 16);
        }
        else
        {
            _trace.Append("--   ExploredMap = RenderFogTexture()\n");
            ExploredMap = RenderFogTexture(null, WorldMap, _explored, mask, maskClearColor);
            // ExploredMap = RenderTexture(null, WorldMap, _fogTexture, fogMask, _abyssColor);
        }

        _trace.Append($"<- RenderTopographicalMap()\n");
    }


    public static IEnumerable RunAsCoroutine(Action action)
    {
        _trace.Append("-> RunAsCoroutine()\n");
        var thread = new Thread(() => action());

        _trace.Append("--   thread.Start()\n");
        thread.Start();
        _trace.Append("--   while (thread.IsAlive)\n");
        while (thread.IsAlive)
        {
            yield return null;
        }

        _trace.Append("<- RunAsCoroutine()\n");
        yield return null;
    }


    private Color32[] RenderContoursLegacy(Color32[] canvas, Color32[] heights, int interval, byte alpha, Color32[] mask, Color32 maskClearColor)
    {
        Color32[] input  = new Color32[heights.Length];

        canvas = canvas ?? new Color32[heights.Length];  // Color32[] output = new Color32[heights.Length];

        // ! -> input = Array.ConvertAll(start, x => x.b > 0 ? 0 : Math.Min(x.r + graduations, 255));
        // Shift height values up by graduation so that coast is outlined with a contour line
        for (int i = 0; i < _mapPixelCount; ++i)
        {
            input[i].rgba = heights[i].rgba <= 0 ? 0 : heights[i].rgba + interval;
        }

        for (int y = 1; y < _mapSize - 1; y++)
        {
            int yCoord = y * _mapSize;
            for (int x = 1; x < _mapSize - 1; x++)
            {
                int testCoord = yCoord + x;                         // Flattened 2D coords of pixel under test
                int heightRef = input[yCoord + x].rgba / interval;  // Which graduation does the height under test fall under?
                canvas[testCoord] = Color.clear;                 // Default color is clear

                if (mask != null && mask[testCoord].rgba == maskClearColor.rgba)
                    continue;

                for (int i = -1; i < 2; i++)
                {
                    int iCoord = i * _mapSize;
                    for (int j = -1; j < 2; j++)
                    {
                        if (!((i == 0) && (j == 0)))      //Don't check self
                        {
                            int scanCoord = testCoord + iCoord + j;  //Flattened 2D coords of adjacent pixel to be checked
                            int testHeight = input[scanCoord].rgba / interval;

                            if (testHeight < heightRef)  //Is scanned adjacent coordinate in a lower graduation? //If so, this pixel is black
                            {
                                byte alpha2 = alpha;
                                if ((heightRef % 5) - 1 != 0) alpha2 /= 2;  //Keep full alpha for every 5th graduation line. Half alpha for the rest

                                if ((i != 0) && (j != 0) && (canvas[testCoord].a != alpha2))       //Detected at diagonal
                                    canvas[testCoord] = new Color32(0, 0, 0, (byte)(alpha2 / 2));   //Gets half alpha for a smoother effect
                                else                                                    //Detected at orthogonal
                                {
                                    canvas[testCoord] = new Color32(0, 0, 0, (byte)(alpha2));   //Gets full alpha
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        return canvas;
    }


    private Color32[] RenderContours(Color32[] canvas, Color32[] heights, int interval, int isobathInterval, byte alpha, Color32[] mask, Color32 maskClearColor, int size)
    {
        bool noBlending  = canvas == null;
        byte halfAlpha   = (byte)(alpha / 2);
        byte quoterAlpha = (byte)(alpha / 4);

        canvas = canvas ?? new Color32[heights.Length];

        Color32   blackColor = Color.black;
        Color32[] maskProxy  = mask ?? new Color32[2];
        int       maskInc    = mask == null ? 0: 1;
        int       maskSize   = mask == null ? 0: size;

        if (mask == null)
        {
            maskClearColor = Color.clear;
            maskProxy[1]   = Color.white;
        }

        int[] contourHeights = isobathInterval > 0
            ? interval > 0
                ? Array.ConvertAll(heights, x => x.rgba <= 0 ? x.rgba / isobathInterval : (x.rgba + interval) / interval)
                : Array.ConvertAll(heights, x => x.rgba <= 0 ? x.rgba / isobathInterval : 0)
            : Array.ConvertAll(heights, x => x.rgba <= 0 ? 0                        : (x.rgba + interval) / interval);

        unsafe
        {
            fixed (Color32 *maskBegin = maskProxy)
            {
                fixed (Color32 *canvasBegin = canvas)
                {
                    fixed (int *heightsBegin = contourHeights)
                    {
                        // x + +
                        // + o +
                        // + + +

                        int     *height     = heightsBegin;
                        int     *height1    = height       + size;
                        int     *height2    = height1      + size;
                        int     *heightsEnd = heightsBegin + contourHeights.Length - 2*size - 2;
                        Color32 *pixel      = canvasBegin  + size + 1;
                        Color32 *m          = maskBegin    + maskSize + 1;
                        int      x          = 0;
                        int      xEnd       = size - 2;

                        while (height < heightsEnd)
                        {
                            if (m->rgba != maskClearColor.rgba)
                            {
                                int hNW = *(height);
                                int hN  = *(height  + 1);
                                int hNE = *(height  + 2);
                                int hW  = *(height1);
                                int h   = *(height1 + 1);
                                int hE  = *(height1 + 2);
                                int hSW = *(height2);
                                int hS  = *(height2 + 1);
                                int hSE = *(height2 + 2);

                                if (hN < h || hW < h || hE < h || hS < h)
                                {
                                    // detected at orthogonal cell => set full alpha
                                    byte a = (h % 5) == 1 ? alpha : halfAlpha;  // Keep full alpha for every 5th graduation line. Half alpha for the rest

                                    if (noBlending)
                                        pixel->a = a;
                                    else
                                    {
                                        blackColor.a = a;
                                        BlendColor(ref *pixel, *pixel, blackColor);
                                    }
                                }
                                else if (hNW < h || hNE < h || hSW < h || hSE < h)
                                {
                                    // detected at diagonal cell => set half alpha for a smoother effect if not full alpha already
                                    byte a = (h % 5) == 1 ? halfAlpha : quoterAlpha;  // Keep full alpha for every 5th graduation line. Half alpha for the rest

                                    if (noBlending)
                                        pixel->a = a;
                                    else
                                    {
                                        blackColor.a = a;
                                        BlendColor(ref *pixel, *pixel, blackColor);
                                    }
                                }
                            }

                            ++height;
                            ++height1;
                            ++height2;
                            ++pixel;
                            m += maskInc;

                            if (++x < xEnd)
                                continue;

                            height  += 2;
                            height1 += 2;
                            height2 += 2;
                            pixel   += 2;
                            m += maskInc + maskInc;

                            x = 0;
                        }  // while (height < heightsEnd)
                    }  // fixed
                }  // fixed
            }  // fixed
        }  // unsafe

        return canvas;
    }


    private Color32[] RenderWater(Color32[] canvas, Color32[] heights, Color32[] mask, Color32 maskClearColor, int size, int tightness, int noiseAmplitude, float noiseOffset = 0)
    {
        bool    noBlending = canvas == null;
        Color32 c          = new Color32();

        canvas = canvas ?? new Color32[heights.Length];

        if (noiseOffset > 1)
            noiseOffset = 1;
        else if (noiseOffset < 0)
            noiseOffset = 0;

        for (int i = 0; i < heights.Length; ++i)
        {
            if (mask != null && mask[i].rgba == maskClearColor.rgba)
                continue;

            if (heights[i].rgba > 0)
            {
                // above sea level
                if (noBlending)
                    canvas[i] = Color.clear;
            }
            else
            {
                int a = -heights[i].rgba * 16 + 128;

                ref Color32 pixel = ref canvas[i];

                if (tightness > 0 && noiseAmplitude > 0)
                {
                    float noise = Mathf.PerlinNoise((float)(i / size) / tightness, (float)(i % size) / tightness) - noiseOffset;
                    byte  delta = (byte)(noiseAmplitude * noise);
                    c.r = (byte)(_oceanColor.r + delta);
                    c.g = (byte)(_oceanColor.g + delta);
                    c.b = (byte)(_oceanColor.b + delta);
                }
                else
                {
                    c.rgba = _oceanColor.rgba;
                }

                c.a = (byte)(a > 255 ? 255 : a);

                if (noBlending)
                {
                    pixel.rgba = c.rgba;
                }
                else
                {
                    BlendColor(ref pixel, pixel, c);
                }
            }
        }

        return canvas;
    }


    private Color32[] RenderHeightLayer(Color32[] canvas, Color32[] heights, int upperBound, int lowerBound, Color32 color, Func<int, int> alpha, Color32[] mask, Color32 maskClearColor, int size, int tightness, int noiseAmplitude, float noiseOffset = 0)
    {
        bool    noBlending = canvas == null;
        Color32 c          = new Color32();

        canvas = canvas ?? new Color32[heights.Length];

        if (noiseOffset > 1)
            noiseOffset = 1;
        else if (noiseOffset < 0)
            noiseOffset = 0;

        for (int i = 0; i < heights.Length; ++i)
        {
            if (mask != null && mask[i].rgba == maskClearColor.rgba)
                continue;

            int h = heights[i].rgba;

            if (h > upperBound || h < lowerBound)
            {
                // out of layer
                if (noBlending)
                    canvas[i] = Color.clear;
            }
            else
            {
                int a = alpha(h);

                ref Color32 pixel = ref canvas[i];

                if (tightness > 0 && noiseAmplitude > 0)
                {
                    // ReSharper disable once PossibleLossOfFraction
                    float noise = Mathf.PerlinNoise((float)(i / size) / tightness, (float)(i % size) / tightness) - noiseOffset;
                    byte  delta = (byte)(noiseAmplitude * noise);
                    c.r = (byte)(color.r + delta);
                    c.g = (byte)(color.g + delta);
                    c.b = (byte)(color.b + delta);
                }
                else
                {
                    c.rgba = color.rgba;
                }

                c.a = (byte)(a > 255 ? 255 : (a < 0 ? 0 : a));

                if (noBlending)
                {
                    pixel.rgba = c.rgba;
                }
                else
                {
                    BlendColor(ref pixel, pixel, c);
                }
            }
        }

        return canvas;
    }


    private Color32[] RenderFog(Color32[] canvas, Color32[] layer, Color32[] explored, Color32[] mask, Color32 maskClearColor, int size, int tightness, int damping)
    {
        canvas = canvas ?? new Color32[explored.Length];

        for (int i = 0; i < explored.Length; ++i)
        {
            if (mask != null && mask[i].rgba == maskClearColor.rgba)
            {
                canvas[i] = layer[i];
                continue;
            }

            if (explored[i].a > 0)
            {
                canvas[i] = layer[i];
                continue;
            }

            ref Color32 pixel = ref canvas[i];

            if (tightness > 0 && damping > 0)
            {
                // ReSharper disable once PossibleLossOfFraction
                float noise = Mathf.PerlinNoise((float)(i / size) / tightness, (float)(i % size) / tightness) - 0.5f;
                byte  delta = (byte)(255 * noise / damping);
                pixel.r = (byte)(FogColor.r + delta);
                pixel.g = (byte)(FogColor.g + delta);
                pixel.b = (byte)(FogColor.b + delta);
                pixel.a = (byte)(255);
            }
            else
            {
                pixel.rgba = FogColor.rgba;
            }
        }

        return canvas;
    }


    private Color32[] RenderFogTexture(Color32[] canvas, Color32[] layer, Color32[] explored, Color32[] mask, Color32 maskClearColor)
    {
        canvas = canvas ?? new Color32[explored.Length];

        if (_fogTexture == null)
            return canvas;

        int textureSize = (int)Math.Sqrt(_fogTexture.Length);

        for (int i = 0; i < explored.Length; ++i)
        {
            if (mask != null && mask[i].rgba == maskClearColor.rgba)
            {
                canvas[i] = layer[i];
                continue;
            }

            if (explored[i].a > 0)
            {
                canvas[i] = layer[i];
                continue;
            }

            ref Color32 pixel = ref canvas[i];

            int x  = i / _mapSize;
            int y  = i % _mapSize;

            int tx = x % textureSize;
            int ty = y % textureSize;

            pixel.rgba = _fogTexture[tx * textureSize + ty].rgba;
        }

        return canvas;
    }


    private Color32[] RenderTexture(Color32[] canvas, Color32[] layer, Color32[] texture, Color32[] mask, Color32 maskClearColor)
    {
        canvas = canvas ?? new Color32[layer.Length];

        if (texture == null)
            return canvas;

        int textureSize = (int)Math.Sqrt(texture.Length);  // assert(texture.Length == textureSize * textureSize)

        for (int i = 0; i < layer.Length; ++i)
        {
            if (mask != null && mask[i].rgba == maskClearColor.rgba)
            {
                canvas[i] = layer[i];
                continue;
            }

            ref Color32 pixel = ref canvas[i];

            int x  = i / _mapSize;
            int y  = i % _mapSize;

            int tx = x % textureSize;
            int ty = y % textureSize;

            //pixel.rgba = texture[tx * textureSize + ty].rgba;
            BlendColor(ref pixel, pixel, texture[tx * textureSize + ty]);
        }

        return canvas;
    }


    private Color32[] DarkenLinear(Color32[] canvas, Color32[] layer, byte d, Color32[] mask, Color32 maskClearColor)
    {
        canvas = canvas ?? new Color32[layer.Length];

        for (int i = 0; i < layer.Length; ++i)
        {
            if (mask != null && mask[i].rgba == maskClearColor.rgba)
            {
                canvas[i] = layer[i];
                continue;
            }

            ref Color32 pixel = ref canvas[i];
            Color32     c     = layer[i];

            pixel.rgba = 0;
            pixel.a    = c.a;

            int value = c.r - d;
            if (value > 0)
                pixel.r = (byte)value;

            value = c.g - d;
            if (value > 0)
                pixel.g = (byte)value;

            value = c.b - d;
            if (value > 0)
                pixel.b = (byte)value;
        }

        return canvas;
    }


    private Color32[] DarkenRelative(Color32[] canvas, Color32[] layer, float dimFactor, Color32[] mask, Color32 maskClearColor)
    {
        canvas = canvas ?? new Color32[layer.Length];

        for (int i = 0; i < layer.Length; ++i)
        {
            if (mask != null && mask[i].rgba == maskClearColor.rgba)
            {
                canvas[i] = layer[i];
                continue;
            }

            ref Color32 target = ref canvas[i];
            ref Color32 source = ref layer[i];

            target.r = (byte)(source.r * dimFactor);
            target.g = (byte)(source.g * dimFactor);
            target.b = (byte)(source.b * dimFactor);
            target.a = source.a;
        }

        return canvas;
    }


    private Color32[] ReplaceColor(Color32[] canvas, Color32[] layer, Color32 from, Color32 to)
    {
        canvas = canvas ?? new Color32[layer.Length];

        for (int i = 0; i < layer.Length; ++i)
        {
            Color32 ip = layer[i];

            if (ip.rgba == from.rgba)
                canvas[i] = to;
            else
                canvas[i] = ip;
        }

        return canvas;
    }


    private Color32[] FillWithColor(Color32[] canvas, Color32[] layer, Color32 color, Color32[] mask = null, Color32 maskClearColor = new Color32())
    {
        canvas = canvas ?? new Color32[layer.Length];

        for (int i = 0; i < layer.Length; ++i)
        {
            if (mask != null && mask[i].rgba == maskClearColor.rgba)
            {
                canvas[i] = layer[i];
                continue;
            }

            canvas[i] = color;
        }

        return canvas;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte BlendColorChannel(byte c1, byte alpha1, byte c2, byte alpha2, byte alpha)
    {
        //return alpha1 == 0 || alpha2 == 255 ? c2 : (byte)(c2 + (c1 - c2) * (255 - alpha2) * alpha1 / 65025);
        return alpha1 == 0 || alpha2 == 255
            ? (byte)(c2 * alpha2 / alpha)
            : (byte)(c2 * alpha2 / alpha + c1 * alpha1 * (255 - alpha2) / 255 / alpha);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte BlendAlphaChannel(byte alpha1, byte alpha2)
    {
        return alpha1 == 255 || alpha2 == 0
            ?  alpha1
            : (
                alpha2 == 255
                    ? (byte)255
                    : (byte)(alpha1 + ((255 - alpha1) * alpha2) / 255)
            );
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BlendColor(ref Color32 c, Color32 c1, Color32 c2)
    {
        c.a = BlendAlphaChannel(c1.a, c2.a);

        c.r = BlendColorChannel(c1.r, c1.a, c2.r, c2.a, c.a);
        c.g = BlendColorChannel(c1.g, c1.a, c2.g, c2.a, c.a);
        c.b = BlendColorChannel(c1.b, c1.a, c2.b, c2.a, c.a);
    }


    private Color32[] Blend(Color32[] canvas, Color32[] layer, Color32[] mask = null, Color32 maskClearColor = new Color32())
    {
        //Color32 c = new Color32();
        canvas = canvas ?? new Color32[layer.Length];

        for (int i = 0; i < canvas.Length; ++i)
        {
            if (mask != null && mask[i].rgba == maskClearColor.rgba)
                continue;

            Color32 c1 = canvas[i];
            Color32 c2 = layer[i];

            //float a1 = ((Color)canvas[i]).a;
            //float a2 = ((Color)array2[i]).a;

            //c.a = BlendAlphaChannel(c1.a, c2.a);
            //c.r = BlendColorChannel(c1.r, c1.a, c2.r, c2.a, c.a);
            //c.g = BlendColorChannel(c1.g, c1.a, c2.g, c2.a, c.a);
            //c.b = BlendColorChannel(c1.b, c1.a, c2.b, c2.a, c.a);

            BlendColor(ref canvas[i], c1, c2);
        }

        return canvas;
    }

    // refined copy of IEnumerator OverlayTexture(Color32[] array1, Color32[] array2, bool allMap = false)
    private Color32[] BlendByLerp(Color32[] canvas, Color32[] layer, bool[] mask)
    {
        canvas = canvas ?? new Color32[layer.Length];

        for (int i = 0; i < layer.Length; ++i)
        {
            if (mask != null && !mask[i])
                continue;

            Color c1 = canvas[i];
            Color c2 = layer[i];

            Color c  = Color.Lerp(((Color)c1), ((Color)c2), c2.a);

            //c.a = a2 + a1;
            //if (c.a > 1)
            //    c.a = 1;
            c.a = c1.a + (255 - c1.a) * c2.a / 255;

            canvas[i] = c;
        }

        return canvas;
    }
}
