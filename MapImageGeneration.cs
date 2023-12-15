using System.Collections;
using System.Threading;
using System;
using UnityEngine;

public class MapImageGeneration
{
    private static Color32[] m_mapTexture;
    private static Color[] m_forestTexture;
    private static Color32[] m_heightmap;
    public  static Color32[] s_worldMask;
    private static bool[] m_exploration;
    private static bool[] m_mapData;
    private static int m_textureSize;
    private static int mapSizeFactor;

    private static Color32[] space;
    private static int spaceRes;

    private Color32[] result;
    public Color32[] output;

    public Color32[] mapWithoutFog = null;

    public static readonly Color32 yellowMap = new Color32(203, 155, 87, byte.MaxValue);
    private static readonly Color32 s_oceanColor = new Color32(20, 100, 255, byte.MaxValue);

    public Color32 abyssColor = new Color32(0, 0, 0, byte.MaxValue);  // TODO: set this value on init by mapmaker

    public static void Initialize(Color32[] biomes, Color[] forests, Color32[] height, bool[] exploration, int texture_size, bool[] mapData)
    {
        m_mapTexture = biomes;
        m_forestTexture = forests;
        m_heightmap = height;
        m_exploration = exploration;
        m_textureSize = texture_size;
        m_mapData = mapData;

        mapSizeFactor = m_textureSize / Minimap.instance.m_textureSize;

        // ? why we do it here - not inside MapGeneration::PrepareTerrainData() ?
        if (spaceRes == 0)
        {
            Texture spaceTex = Minimap.instance.m_mapImageLarge.material.GetTexture("_SpaceTex");

            spaceRes = spaceTex.width;

            RenderTexture tmp = RenderTexture.GetTemporary(spaceRes, spaceRes, 24);

            Graphics.Blit(spaceTex, tmp);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tmp;

            Texture2D tex = new Texture2D(spaceRes, spaceRes, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, spaceRes, spaceRes), 0, 0);
            tex.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tmp);

            space = tex.GetPixels32();

            UnityEngine.Object.Destroy(tex);
        }
    }
    
    public static void DeInitialize()
    {
        m_mapTexture = null;
        m_forestTexture = null;
        m_heightmap = null;
        m_exploration = null;
        m_mapData = null;
        s_worldMask = null;
    }

    // ? same same but different =) ::= GenerateChartMap(), but more yellowish and with slightly different brightness for forest. So, may be 'config' it?
    public IEnumerator GenerateOldMap(int graduationHeight)
    {
        output = null;

        Color32[] outtex;

        if (mapWithoutFog != null)
        {
            outtex = mapWithoutFog;
        }
        else
        {
            yield return GenerateOceanTexture(m_heightmap, m_mapTexture, 0.25f);
            Color32[] oceanTexture = result;

            yield return ReplaceColor(m_mapTexture, abyssColor, yellowMap);    //Replace void with "Map colour"
            outtex = result;

            yield return OverlayTexture(outtex, oceanTexture);
            outtex = result;

            // ? next 9 lines of code: -> LerpTextureWithColor(outtex, m_YellowColor, 3 /* times */) + check if alpha is 255 (max)
            yield return GetSolidColour(yellowMap);    //Yellowize map
            Color32[] offYellow = result;

            yield return LerpTextures(outtex, offYellow);
            outtex = result;
            yield return LerpTextures(outtex, offYellow);
            outtex = result;
            yield return LerpTextures(outtex, offYellow);
            outtex = result;

            yield return AddPerlinNoise(outtex, 128, 16);
            outtex = result;

            yield return ApplyForestMaskTexture(outtex, m_forestTexture, 0.95f);
            outtex = result;

            yield return GenerateContourMap(m_heightmap, graduationHeight, 128);
            Color32[] contours = result;

            yield return OverlayTexture(outtex, contours);
            outtex = result;

            mapWithoutFog = result;
        }

        yield return StylizeFog(m_exploration);
        Color32[] fog = result;

        yield return OverlayTexture(outtex, fog, true);
        outtex = result;

        output = outtex;
    }
    
    public IEnumerator GenerateChartMap(int graduationHeight)
    {
        output = null;

        Color32[] outtex;

        if (mapWithoutFog != null)
        {
            outtex = mapWithoutFog;
        }
        else
        {
            yield return GenerateOceanTexture(m_heightmap, m_mapTexture, 0.15f);
            Color32[] oceanTexture = result;

            yield return ReplaceColor(m_mapTexture, abyssColor, yellowMap);    //Replace void with "Map colour"
            outtex = result;

            yield return OverlayTexture(outtex, oceanTexture);
            outtex = result;

            yield return GetSolidColour(yellowMap);    //Yellowize map
            Color32[] offYellow = result;

            yield return LerpTextures(outtex, offYellow);
            outtex = result;

            yield return AddPerlinNoise(outtex, 128, 16);
            outtex = result;

            yield return ApplyForestMaskTexture(outtex, m_forestTexture);
            outtex = result;

            yield return GenerateContourMap(m_heightmap, graduationHeight, 128);
            Color32[] contours = result;

            yield return OverlayTexture(outtex, contours);
            outtex = result;

            mapWithoutFog = result;
        }

        yield return StylizeFog(m_exploration);
        Color32[] fog = result;

        yield return OverlayTexture(outtex, fog, true);
        outtex = result;

        output = outtex;
    }
    
    public IEnumerator GenerateSatelliteImage()
    {
        output = null;

        Color32[] outtex;

        if (mapWithoutFog != null)
        {
            outtex = mapWithoutFog;
        }
        else
        {
            yield return GenerateOceanTexture(m_heightmap, m_mapTexture);
            Color32[] oceanTexture = result;

            yield return AddPerlinNoise(oceanTexture, 4, 64);
            oceanTexture = result;

            yield return ReplaceColorWithSpace(m_mapTexture, abyssColor);    //Replace void with Space texture
            outtex = result;

            yield return OverlayTexture(outtex, oceanTexture);
            outtex = result;

            yield return CreateShadowMap(m_heightmap, 23);
            Color32[] shadowmap = result;

            yield return DarkenTextureLinear(outtex, 20);
            outtex = result;

            yield return ApplyForestMaskTexture(outtex, m_forestTexture);
            outtex = result;

            yield return GenerateContourMap(m_heightmap, 128, 64);
            Color32[] contours = result;

            yield return OverlayTexture(outtex, contours);
            outtex = result;

            yield return OverlayTexture(outtex, shadowmap);
            outtex = result;

            mapWithoutFog = result;
        }

        yield return StylizeFog(m_exploration);
        Color32[] fog = result;

        yield return OverlayTexture(outtex, fog, true);
        outtex = result;

        output = outtex;
    }
    
    public IEnumerator GenerateTopographicalMap(int graduationHeight)
    {
        output = null;

        Color32[] outtex;

        if (mapWithoutFog != null)
        {
            outtex = mapWithoutFog;
        }
        else
        {
            yield return GenerateOceanTexture(m_heightmap);
            Color32[] oceanTexture = result;

            yield return AddPerlinNoise(oceanTexture, 4, 64);
            oceanTexture = result;

            yield return ReplaceColor(m_mapTexture, abyssColor, Color.white);
            outtex = result;

            yield return OverlayTexture(outtex, oceanTexture);
            outtex = result;

            //yield return CreateShadowMap(m_heightmap, 23);
            //Color32[] shadowmap = result;

            yield return DarkenTextureLinear(outtex, 20);
            outtex = result;

            yield return ApplyForestMaskTexture(outtex, m_forestTexture);
            outtex = result;

            yield return GenerateContourMap(m_heightmap, graduationHeight, 128);
            Color32[] contours = result;

            yield return OverlayTexture(outtex, contours);
            outtex = result;

            //yield return OverlayTexture(outtex, shadowmap);
            //outtex = result;

            mapWithoutFog = result;
        }

        yield return StylizeFog(m_exploration);
        Color32[] fog = result;

        yield return OverlayTexture(outtex, fog, true);
        outtex = result;

        output = outtex;
    }

    private IEnumerator OverlayTexture(Color32[] array1, Color32[] array2, bool allMap = false) //Tex2 on Tex1
    {
        Color32[] output = new Color32[m_textureSize * m_textureSize];
        Color workingColor;

        var internalThread = new Thread(() =>
        {
            for (int i = 0; i < (m_textureSize * m_textureSize); i++)
            {
                if (!allMap && !m_mapData[i])
                    continue;

                float a = ((Color)array2[i]).a;
                float b = ((Color)array1[i]).a;
                //array1[i].a = 1;
                workingColor = Color.Lerp(((Color)array1[i]), ((Color)array2[i]), a);
                workingColor.a = a + b;
                if (workingColor.a > 1) workingColor.a = 1;
                output[i] = (Color32)workingColor;
            }
        });

        internalThread.Start();
        while (internalThread.IsAlive == true)
        {
            yield return null;
        }

        result = output;
    }

    private IEnumerator LerpTextures(Color32[] array1, Color32[] array2)
    {
        Color32[] output = new Color32[m_textureSize * m_textureSize];

        var internalThread = new Thread(() =>
        {
            for (int i = 0; i < (m_textureSize * m_textureSize); i++)
            {
                if (!m_mapData[i])
                    continue;

                int a = array2[i].a - array1[i].a;

                int finalA = Math.Min(array1[i].a + array2[i].a, 255);

                int div = ((array1[i].a > array2[i].a) ? array1[i].a : array2[i].a) * 2;

                float lerp = (((float)a) / div) + 0.5f;

                output[i] = Color32.Lerp(array1[i], array2[i], lerp);
                output[i].a = (byte)finalA;
            }
        });

        internalThread.Start();
        while (internalThread.IsAlive == true)
        {
            yield return null;
        }

        result = output;
    }

    private IEnumerator DarkenTextureLinear(Color32[] array, byte d)
    {
        Color32[] output = new Color32[m_textureSize * m_textureSize];

        var internalThread = new Thread(() =>
        {
            for (int i = 0; i < m_textureSize * m_textureSize; i++)
            {
                if (!m_mapData[i])
                    continue;

                int bit;
                bit = (array[i].r - d);
                if (bit < 0) bit = 0;
                output[i].r = (byte)bit;

                bit = (array[i].g - d);
                if (bit < 0) bit = 0;
                output[i].g = (byte)bit;

                bit = (array[i].b - d);
                if (bit < 0) bit = 0;
                output[i].b = (byte)bit;

                output[i].a = array[i].a;
            }
        });

        internalThread.Start();
        while (internalThread.IsAlive == true)
        {
            yield return null;
        }

        result = output;
    }

    private IEnumerator CreateShadowMap(Color32[] heightmap, byte intensity)
    {
        yield return CreateHardShadowMap(heightmap, intensity);
        Color32[] hardshadows = result;

        yield return CreateSoftShadowMap(heightmap);
        Color32[] softshadows = result;
        yield return LerpTextures(softshadows, hardshadows);
    }

    private IEnumerator CreateSoftShadowMap(Color32[] input)
    {
        Color32[] output;

        output = new Color32[input.Length];

        var internalThread = new Thread(() =>
        {
            for (int i = 0; i < m_textureSize; i++)
            {
                for (int j = 0; j < m_textureSize; j++)
                {
                    int pos = i * m_textureSize + j;
                    if (!m_mapData[pos])
                        continue;

                    int pixel;
                    if (i > 0)
                    {
                        pixel = input[pos].r - input[(i - 1) * m_textureSize + j].r;
                    }
                    else pixel = 0;

                    pixel *= 8;
                    byte abs = (byte)Math.Abs(pixel);
                    byte pix;
                    if (pixel >= 0) pix = 255;
                    else pix = 0;
                    output[pos] = new Color32(pix, pix, pix, abs);

                }
            }
        });

        internalThread.Start();
        while (internalThread.IsAlive == true)
        {
            yield return null;
        }

        result = output;
    }

    private IEnumerator CreateHardShadowMap(Color32[] input, byte intensity)
    {
        Color32[] output;

        output = new Color32[input.Length];

        var internalThread = new Thread(() =>
        {
            bool[] shaded = new bool[m_textureSize * m_textureSize];

            for (int i = 0; i < m_textureSize * m_textureSize; i++)
                shaded[i] = false;

            for (int i = 0; i < m_textureSize; i++)
            {
                for (int j = 0; j < m_textureSize; j++)
                {
                    int pos = i * m_textureSize + j;
                    if (!m_mapData[pos])
                        continue;

                    if (shaded[pos] == false)
                    {
                        output[pos] = new Color32(255, 255, 255, 0);
                        int q = 1;
                        while ((i + q) < m_textureSize)
                        {
                            if (input[pos].r > (input[(i + q) * m_textureSize + j].r + (q * 2)))    //2/1 sun angle (the +q part at the end)
                            {
                                shaded[(i + q) * m_textureSize + j] = true;
                            }
                            else break;
                            q++;
                        }
                    }
                    else output[pos] = new Color32(0, 0, 0, intensity);
                }
            }
        });

        internalThread.Start();
        while (internalThread.IsAlive == true)
        {
            yield return null;
        }
        result = output;
    }

    private IEnumerator GenerateOceanTexture(Color32[] input, Color32[] biomeColor, float oceanLerpTarget = 0.1f)
    {
        Color32[] output = new Color32[input.Length];

        var internalThread = new Thread(() =>
        {
            for (int i = 0; i < m_textureSize * m_textureSize; i++)
            {
                if (!m_mapData[i])
                    continue;

                if (input[i].b == 0)
                {
                    output[i] = Color.clear;
                    continue;
                }

                int correction = ((i / m_textureSize) / (8 * mapSizeFactor)) - 128;           //correction goes from -128 to 127
                int correction2 = ((i % m_textureSize) / (8 * mapSizeFactor)) - 128;       //correction2 goes from -128 to 127
                int correction3 = ((correction * correction) / 128) + ((correction2 * correction2) / 512);

                if (correction < 0)
                {
                    // South         83, 116, 196
                    output[i].r = (byte)(10 + correction3);
                    output[i].g = (byte)(136 - (correction3 / 4));
                    output[i].b = 193;
                }
                else
                {
                    // North
                    output[i].r = (byte)(10 + (correction3 / 2));
                    output[i].g = 136;
                    output[i].b = (byte)(193 - (correction3 / 2));
                }
                output[i].a = (byte)Math.Min((input[i].b * 16) + 128, 255);

                if (biomeColor[i] == Color.blue)
                    output[i] = Color32.Lerp(output[i], s_oceanColor, oceanLerpTarget);
            }
        });

        internalThread.Start();
        while (internalThread.IsAlive == true)
        {
            yield return null;
        }

        result = output;
    }

    private IEnumerator GenerateOceanTexture(Color32[] input)
    {
        Color32[] output     = new Color32[input.Length];
        Color32   oceanColor = new Color32(20, 100, 255, 255);

        var internalThread = new Thread(() =>
        {
            int divider = m_textureSize / 256;

            for (int i = 0; i < input.Length; i++)
            {
                if (!m_mapData[i])
                    continue;

                if (input[i].b <= 0)
                {
                    output[i] = Color.clear;  // above sea level
                }
                else
                {
                    int correction1 = ((i / m_textureSize) / divider) - 128;  // correction1 goes from -128 to 127
                    int correction2 = ((i % m_textureSize) / divider) - 128;  // correction2 goes from -128 to 127
                    int correction3 = ((correction1 * correction1) / 128) + ((correction2 * correction2) / 512);

                    int alpha = (int)(input[i].b * 16) + 128;

                    ref Color32 pixel = ref output[i];

                    if (correction1 < 0)
                    {
                        //m_oceanColor = new Color32((byte)(10 + correction3), (byte)(136 - (correction3 / 4)), (byte)(193), (byte)(alpha > 255 ? 255 : alpha));        // South  83, 116, 196
                        pixel.r = (byte)(10 + correction3);
                        pixel.g = (byte)(136 - (correction3 / 4));
                        pixel.b = (byte)(193);
                    }
                    else
                    {
                        //m_oceanColor = new Color32((byte)(10 + (correction3 / 2)), (byte)(136), (byte)(193 - (correction3 / 2)), (byte)(alpha > 255 ? 255 : alpha));  // North
                        pixel.r = (byte)(10 + (correction3 / 2));
                        pixel.g = (byte)(136);
                        pixel.b = (byte)(193 - (correction3 / 2));
                    }

                    pixel.a = (byte)(alpha > 255 ? 255 : alpha);

                    //output[i]. = m_oceanColor;
                }
            }
        });

        internalThread.Start();
        while (internalThread.IsAlive == true)
        {
            yield return null;
        }

        result = output;
    }

    private IEnumerator GetPerlin(int tightness, byte damping)        //Damping reduces amplitude of noise
    {
        Color32[] array = new Color32[m_textureSize * m_textureSize];

        var internalThread = new Thread(() =>
        {
            for (int x = 0; x < m_textureSize; x++)
            {
                for (int y = 0; y < m_textureSize; y++)
                {
                    float sample = Mathf.PerlinNoise(((float)x) / tightness, ((float)y) / tightness);
                    sample = ((sample - 0.5f) / damping) + 0.5f;
                    array[x * m_textureSize + y] = new Color(sample, sample, sample, 0.2f);
                }
            }

        });

        internalThread.Start();
        while (internalThread.IsAlive == true)
        {
            yield return null;
        }
        result = array;
    }

    private IEnumerator StylizeFog(bool[] exploration)
    {
        yield return GetPerlin(128, 16);
        Color32[] noise = result;
        Color32[] fog = new Color32[m_textureSize * m_textureSize];

        var internalThread = new Thread(() =>
        {
            if (s_worldMask == null)
            {
                for (int i = 0; i < m_textureSize * m_textureSize; i++)
                {
                    ref Color32 np = ref noise[i];
                    if (!exploration[i])
                    {
                        fog[i] = new Color32((byte)(203 + (np.r - 128)), (byte)(155 + (np.g - 128)), (byte)(87 + (np.b - 128)), 255);
                    }
                }
            }
            else
            {
                for (int i = 0; i < m_textureSize * m_textureSize; i++)
                {
                    ref Color32 np = ref noise[i];
                    if (!exploration[i] && s_worldMask[i].a > 0)
                    {
                        fog[i] = new Color32((byte)(203 + (np.r - 128)), (byte)(155 + (np.g - 128)), (byte)(87 + (np.b - 128)), 255);
                    }
                }
            }
        });

        internalThread.Start();
        while (internalThread.IsAlive == true)
        {
            yield return null;
        }
        result = fog;
    }

    private IEnumerator GenerateContourMap(Color32[] start, int graduations, byte alpha)
    {
        Color32[] input;
        Color32[] output;

        input = new Color32[start.Length];
        output = new Color32[input.Length];

        var internalThread = new Thread(() =>
        {
            for (int i = 0; i < (m_textureSize * m_textureSize); i++)    //Shift height values up by graduation so that coast is outlined with a contour line
            {
                int newR = (start[i].b > 0) ? 0 : Math.Min(start[i].r + graduations, 255);
                input[i].r = (byte)newR;
            }

            for (int y = 1; y < (m_textureSize - 1); y++)
            {
                int yCoord = y * m_textureSize;
                for (int x = 1; x < (m_textureSize - 1); x++)
                {
                    int testCoord = yCoord + x;    //Flattened 2D coords of pixel under test
                    int heightRef = input[yCoord + x].r / graduations;      //Which graduation does the height under test fall under?
                    output[testCoord] = Color.clear;     //Default color is clear

                    if (!m_mapData[testCoord])
                        continue;

                    for (int i = -1; i < 2; i++)
                    {
                        int iCoord = i * m_textureSize;
                        for (int j = -1; j < 2; j++)
                        {

                            if (!((i == 0) && (j == 0)))      //Don't check self
                            {
                                int scanCoord = testCoord + iCoord + j; //Flattened 2D coords of adjacent pixel to be checked
                                int testHeight = input[scanCoord].r / graduations;

                                if (testHeight < heightRef)  //Is scanned adjacent coordinate in a lower graduation? //If so, this pixel is black
                                {
                                    byte alpha2 = alpha;
                                    if ((heightRef % 5) - 1 != 0) alpha2 /= 2;  //Keep full alpha for every 5th graduation line. Half alpha for the rest 

                                    if ((i != 0) && (j != 0) && (output[testCoord].a != alpha2))       //Detected at diagonal
                                        output[testCoord] = new Color32(0, 0, 0, (byte)(alpha2 / 2));   //Gets half alpha for a smoother effect
                                    else                                                    //Detected at orthogonal
                                    {
                                        output[testCoord] = new Color32(0, 0, 0, (byte)(alpha2));   //Gets full alpha
                                        break;
                                    }

                                }
                            }

                        }
                    }

                }
            }
        });

        internalThread.Start();
        while (internalThread.IsAlive == true)
        {
            yield return null;
        }
        result = output;
    }

    private IEnumerator AddPerlinNoise(Color32[] input, int tightness, byte damping)        //Damping reduces amplitude of noise
    {
        Color32[] array = new Color32[m_textureSize * m_textureSize];

        var internalThread = new Thread(() =>
        {
            for (int x = 0; x < m_textureSize; x++)
            {
                for (int y = 0; y < m_textureSize; y++)
                {
                    int pos = x * m_textureSize + y;

                    if (!m_mapData[pos])
                        continue;

                    Color start = input[pos];
                    float sample = Mathf.PerlinNoise(((float)x) / tightness, ((float)y) / tightness);
                    sample = ((sample - 0.5f) / damping);
                    array[pos] = new Color(start.r + sample, start.g + sample, start.b + sample, start.a);
                }
            }

        });

        internalThread.Start();
        while (internalThread.IsAlive == true)
        {
            yield return null;
        }
        result = array;
    }

    private IEnumerator GetSolidColour(Color32 TexColour)
    {
        Color32[] array = new Color32[m_textureSize * m_textureSize];

        var internalThread = new Thread(() =>
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (!m_mapData[i])
                    continue;

                array[i] = TexColour;
            }
        });

        internalThread.Start();
        while (internalThread.IsAlive == true)
        {
            yield return null;
        }
        result = array;
    }

    private IEnumerator ReplaceColor(Color32[] input, Color32 from, Color32 to)
    {
        Color32[] output = new Color32[input.Length];

        var internalThread = new Thread(() =>
        {
            for (int i = 0; i < input.Length; i++)
            {
                Color32 ip = input[i];

                if (/*!m_mapData[i] ||*/ (ip.r == from.r && ip.g == from.g && ip.b == from.b))
                    output[i] = to;
                else
                    output[i] = ip;
            }
        });

        internalThread.Start();
        while (internalThread.IsAlive == true)
        {
            yield return null;
        }
        result = output;
    }

    private IEnumerator ReplaceColorWithSpace(Color32[] input, Color32 from)
    {
        Color32[] output = new Color32[input.Length];

        var internalThread = new Thread(() =>
        {
            for (int x = 0, d = 0; x < m_textureSize; ++x, d += m_textureSize)
            {
                for (int y = 0; y < m_textureSize; ++y)
                {
                    int pos = d + y;
                    Color32 ip = input[pos];

                    if (/*!m_mapData[pos] ||*/ (ip.r == from.r && ip.g == from.g && ip.b == from.b))
                    {
                        output[pos] = space[x % spaceRes * spaceRes + y % spaceRes];
                    }
                    else
                    {
                        output[pos] = ip;
                    }
                }
            }
        });

        internalThread.Start();
        while (internalThread.IsAlive == true)
        {
            yield return null;
        }
        result = output;
    }

    private IEnumerator ApplyForestMaskTexture(Color32[] array, Color[] forestMask, float forestColorFactor = 0.9f)
    {
        Color32[] output = new Color32[m_textureSize * m_textureSize];

        var internalThread = new Thread(() =>
        {
            for (int i = 0; i < m_textureSize * m_textureSize; i++)
            {
                if (!m_mapData[i])
                    continue;

                if (forestMask[i].r == 0f)
                {
                    output[i] = array[i];
                }
                else
                {
                    float factor = 1f - (1f - forestColorFactor) * forestMask[i].r;

                    // ?? we rewrite output[i].g later with (byte)(array[i].g * factor) 5 lines later => next two lines are useless
                    //if (forestMask[i].g > 0f)
                    //    output[i].g = (byte)(array[i].g + (byte)(forestMask[i].g * forestMask[i].b * 255f));

                    output[i].r = (byte)(array[i].r * factor);
                    output[i].g = (byte)(array[i].g * factor);
                    output[i].b = (byte)(array[i].b * factor);
                    output[i].a = array[i].a;
                }
            }
        });

        internalThread.Start();
        while (internalThread.IsAlive == true)
        {
            yield return null;
        }

        result = output;
    }
}