using System.Collections;
using System.Threading;
using System;
using UnityEngine;

public class MapImageGeneration
{
    private static Color32[] m_mapTexture;
    private static Color32[] m_forestTexture;
    private static Color32[] m_heightmap;
    private static Color32[] m_fogmap;
    private static int m_textureSize;
    private Color32[] result;
    public Color32[] output;

    public static Color32 yellowMap = new Color32(203, 155, 87, byte.MaxValue);
    
    public static void Initialize(Color32[] biomes, Color32[] forests, Color32[] height, Color32[] exploration, int texture_size)
    {
        m_mapTexture = biomes;
        m_forestTexture = forests;
        m_heightmap = height;
        m_fogmap = exploration;
        m_textureSize = texture_size;
    }
    
    public static void DeInitialize()
    {
        m_mapTexture = null;
        m_forestTexture = null;
        m_heightmap = null;
        m_fogmap = null;
    }
    
    public IEnumerator GenerateOldMap(int graduationHeight)
    {
        output = null;

        yield return GenerateOceanTexture(m_heightmap);
        Color32[] oceanTexture = result;

        yield return ReplaceColour32(m_mapTexture, new Color32(0, 0, 0, 255), yellowMap);    //Replace void with "Map colour"
        Color32[] outtex = result;

        yield return OverlayTexture(outtex, oceanTexture);
        outtex = result;

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

        yield return GenerateContourMap(m_heightmap, graduationHeight, 128);
        Color32[] contours = result;

        yield return OverlayTexture(outtex, contours);
        outtex = result;

        yield return StylizeFog(m_fogmap);
        Color32[] fog = result;

        yield return OverlayTexture(outtex, fog);
        outtex = result;

        output = outtex;
    }
    
    public IEnumerator GenerateChartMap(int graduationHeight)
    {
        output = null;

        yield return GenerateOceanTexture(m_heightmap);
        Color32[] oceanTexture = result;

        yield return ReplaceColour32(m_mapTexture, new Color32(0, 0, 0, 255), yellowMap);    //Replace void with "Map colour"
        Color32[] outtex = result;

        yield return OverlayTexture(outtex, oceanTexture);
        outtex = result;

        yield return GetSolidColour(yellowMap);    //Yellowize map
        Color32[] offYellow = result;
        yield return LerpTextures(outtex, offYellow);
        outtex = result;
        //yield return LerpTextures(outtex, offYellow);
        //outtex = result;

        yield return AddPerlinNoise(outtex, 128, 16);
        outtex = result;

        yield return GenerateContourMap(m_heightmap, graduationHeight, 128);
        Color32[] contours = result;

        yield return OverlayTexture(outtex, contours);
        outtex = result;

        yield return StylizeFog(m_fogmap);
        Color32[] fog = result;

        yield return OverlayTexture(outtex, fog);
        outtex = result;

        output = outtex;
    }
    
    public IEnumerator GenerateSatelliteImage()
    {
        output = null;
        //Color32[] a_heightmap = m_heightmap.GetPixels32();

        yield return GenerateOceanTexture(m_heightmap);
        Color32[] oceanTexture = result;

        yield return AddPerlinNoise(oceanTexture, 4, 64);
        oceanTexture = result;

        yield return OverlayTexture(m_mapTexture, oceanTexture);
        Color32[] outtex = result;

        yield return CreateShadowMap(m_heightmap, 23);
        Color32[] shadowmap = result;

        yield return DarkenTextureLinear(outtex, 20);
        outtex = result;

        yield return GenerateContourMap(m_heightmap, 128, 64);
        Color32[] contours = result;

        yield return OverlayTexture(outtex, contours);
        outtex = result;

        yield return OverlayTexture(outtex, shadowmap);
        outtex = result;

        yield return StylizeFog(m_fogmap);
        Color32[] fog = result;

        yield return OverlayTexture(outtex, fog);
        outtex = result;

        output = outtex;
    }
    
    public IEnumerator GenerateTopographicalMap(int graduationHeight)
    {
        output = null;
        //Color32[] a_heightmap = m_heightmap.GetPixels32();

        yield return GenerateOceanTexture(m_heightmap);
        Color32[] oceanTexture = result;

        yield return AddPerlinNoise(oceanTexture, 4, 64);
        oceanTexture = result;

        yield return OverlayTexture(m_mapTexture, oceanTexture);
        Color32[] outtex = result;

        //yield return CreateShadowMap(m_heightmap, 23);
        //Color32[] shadowmap = result;

        yield return DarkenTextureLinear(outtex, 20);
        outtex = result;

        yield return GenerateContourMap(m_heightmap, graduationHeight, 128);
        Color32[] contours = result;

        yield return OverlayTexture(outtex, contours);
        outtex = result;

        //yield return OverlayTexture(outtex, shadowmap);
        //outtex = result;

        yield return StylizeFog(m_fogmap);
        Color32[] fog = result;

        yield return OverlayTexture(outtex, fog);
        outtex = result;

        output = outtex;
    }

    private IEnumerator OverlayTexture(Color32[] array1, Color32[] array2) //Tex2 on Tex1
    {
        Color32[] output = new Color32[m_textureSize * m_textureSize];
        Color workingColor;

        var internalThread = new Thread(() =>
        {
            for (int i = 0; i < (m_textureSize * m_textureSize); i++)
            {
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
                int a = (array2[i].a - array1[i].a);

                int finalA = (array1[i].a + array2[i].a);
                if (finalA > 255) finalA = 255;

                int div;
                if (array1[i].a > array2[i].a) div = array1[i].a;
                else div = array2[i].a;
                div *= 2;

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
                    int pixel;
                    if (i > 0)
                    {
                        pixel = input[i * m_textureSize + j].r - input[(i - 1) * m_textureSize + j].r;
                    }
                    else pixel = 0;

                    pixel *= 8;
                    byte abs = (byte)Math.Abs(pixel);
                    byte pix;
                    if (pixel >= 0) pix = 255;
                    else pix = 0;
                    output[i * m_textureSize + j] = new Color32(pix, pix, pix, abs);

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
                    if (shaded[i * m_textureSize + j] == false)
                    {
                        output[i * m_textureSize + j] = new Color32(255, 255, 255, 0);
                        int q = 1;
                        while ((i + q) < m_textureSize)
                        {
                            if (input[i * m_textureSize + j].r > (input[(i + q) * m_textureSize + j].r + (q * 2)))    //2/1 sun angle (the +q part at the end)
                            {
                                shaded[(i + q) * m_textureSize + j] = true;
                            }
                            else break;
                            q++;
                        }
                    }
                    else output[i * m_textureSize + j] = new Color32(0, 0, 0, intensity);
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

    private IEnumerator GenerateOceanTexture(Color32[] input)
    {
        Color32[] output = new Color32[input.Length];
        //Color32 m_oceanColor = new Color32(20, 100, 255, 255);

        var internalThread = new Thread(() =>
        {
            int divider = m_textureSize / 256;

            for (int i = 0; i < input.Length; i++)
            {
                if (input[i].b > 0)    //Below sea level
                {
                    int correction1 = ((i / m_textureSize) / divider) - 128;  // correction1 goes from -128 to 127
                    int correction2 = ((i % m_textureSize) / divider) - 128;  // correction2 goes from -128 to 127
                    int correction3 = ((correction1 * correction1) / 128) + ((correction2 * correction2) / 512);

                    //if (correction < 0) m_oceanColor = new Color32((byte)(30+correction3), (byte)(240-correction3), 255, 255);
                    //else m_oceanColor = new Color32(30, (byte)(240-correction3), (byte)(255-(correction3/2)), 255);

                    int alpha = (input[i].b * 16) + 128;

                    ref Color32 pixel = ref output[i];

                    if (correction1 < 0)
                    {
                        //m_oceanColor = new Color32((byte)(10 + correction3), (byte)(136 - (correction3 / 4)), (byte)(193), (byte)(alpha > 255 ? 255 : alpha));        // South  83, 116, 196
                        pixel.r = (byte)( 10 + correction3);
                        pixel.g = (byte)(136 - (correction3 / 4));
                        pixel.b = (byte)(193);
                    }
                    else
                    {
                        //m_oceanColor = new Color32((byte)(10 + (correction3 / 2)), (byte)(136), (byte)(193 - (correction3 / 2)), (byte)(alpha > 255 ? 255 : alpha));  // North
                        pixel.r = (byte)( 10 + (correction3 / 2));
                        pixel.g = (byte)(136);
                        pixel.b = (byte)(193 - (correction3 / 2));
                    }

                    pixel.a = (byte)(alpha > 255 ? 255 : alpha);

                    //output[i]. = m_oceanColor;
                }
                else 
                    output[i] = Color.clear;
            }
        });

        internalThread.Start();
        while (internalThread.IsAlive == true)
        {
            yield return null;
        }

        result = output;
        //result = returnTex;
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

    private IEnumerator StylizeFog(Color32[] input)
    {

        yield return GetPerlin(128, 16);
        Color32[] noise = result;
        Color32[] output = new Color32[m_textureSize * m_textureSize];

        var internalThread = new Thread(() =>
        {
            for (int i = 0; i < m_textureSize * m_textureSize; i++)
            {
                if (input[i].a > 0)
                {
                    output[i] = new Color32((byte)(203 + (noise[i].r - 128)), (byte)(155 + (noise[i].g - 128)), (byte)(87 + (noise[i].b - 128)), 255);
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
                int newR = (start[i].r + graduations);
                if (newR > 255) newR = 255;
                if (start[i].b > 0) newR = 0;
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
                    Color start = input[x * m_textureSize + y];
                    float sample = Mathf.PerlinNoise(((float)x) / tightness, ((float)y) / tightness);
                    sample = ((sample - 0.5f) / damping);
                    array[x * m_textureSize + y] = new Color(start.r + sample, start.g + sample, start.b + sample, start.a);
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

    private IEnumerator ReplaceColour32(Color32[] input, Color32 from, Color32 to)
    {
        Color32[] output = new Color32[input.Length];

        var internalThread = new Thread(() =>
        {
            for (int i = 0; i < input.Length; i++)
            {
                if ((input[i].r == from.r) && (input[i].g == from.g) && (input[i].b == from.b)) output[i] = to;
                else output[i] = input[i];
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
