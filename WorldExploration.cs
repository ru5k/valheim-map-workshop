using System;
using System.Collections;
using System.Threading;
using UnityEngine;

namespace MapWorkshop
{
    public class WorldExploration
    {
        private readonly int     _mapScale;
        private readonly bool    _showSharedMap;

        private readonly Color32 _exploredNew = new Color32(1, 0, 0, 1);
        private readonly Color32 _exploredOld = new Color32(1, 0, 1, 1);
        private readonly Color32 _extendedNew = new Color32(0, 1, 0, 0);
        private readonly Color32 _extendedOld = new Color32(0, 1, 1, 0);

        private int       _textureSize;
        private Color32[] _explored;

        private Texture2D _fogTexture;

        private int       _cachedX = -1;
        private int       _cachedY = -1;
        private bool      _cachedIsExplored;

        // TODO:
        //   add out int[2 * _textureSize] - left and right bounds for x (::= j)
        //   add out int[2] - top and bottom bounds for y (::= i)
        //   => these can narrow scanned region when fetching heights more than world radius
        //
        //   we can also narrow fetching of exploration data by providing a precalculated world radius (not a big speed boost though)
        //
        //   so, a strategy:
        //     load old heights and old exploration
        //     -> get world radius
        //       -> get exploration region and bounds taking into account the radius
        //         -> fetch data using bounds and 'new pixels' flags to add them to heights
        //           -> save new heights and new exploration
        //
        //   in case of fetching all map heights we just mark all unexplored pixels as extended (unexplored but needed to be fetched)

        public WorldExploration(int mapScale, bool showSharedMap, Color32[] explored = null)
        {
            _mapScale      = mapScale;
            _showSharedMap = showSharedMap;
            _explored      = explored;
            if (_explored != null)
                _textureSize = (int)Math.Sqrt(_explored.Length);
        }

        public Color32[] ExploredColors
        {
            get => _explored;
            private set {}
        }

        public int TextureSize
        {
            get => _textureSize;
            private set {}
        }

        private bool IsExplored(int x, int y)
        {
            x /= _mapScale; 
            y /= _mapScale;

            if (x == _cachedX && y == _cachedY)
                return _cachedIsExplored;

            _cachedX = x;
            _cachedY = y;
            Color pixel = _fogTexture.GetPixel(x, y);
            _cachedIsExplored = pixel.r == 0f || _showSharedMap && pixel.g == 0f;
            return _cachedIsExplored;
        }

        private IEnumerator InternalFetch()
        {
            int textureSize = Minimap.instance.m_textureSize * _mapScale;
            if (textureSize != _textureSize)
            {
                _textureSize = textureSize;
                _explored    = null;
            }

            _fogTexture  = Minimap.instance.m_fogTexture;

            int arraySize = _textureSize * _textureSize;
            
            bool[] isNew = new bool[arraySize];

            if (_explored == null)
                _explored = new Color32[arraySize];

            var thread = new Thread(() =>
            {
                for (int i = 0; i < _textureSize; ++i)
                {
                    int it = i * _textureSize;

                    for (int j = 0; j < _textureSize; ++j)
                    {
                        int pos = it + j;

                        bool isExplored = IsExplored(j, i);
                        if (!isExplored)
                            continue;     // => r = g = b = a = 0

                        ref Color32 pixel    = ref _explored[pos];
                        ref bool    isNewOne = ref isNew[pos];

                        // (.r != 0 || .g != 0) && .b == 0  => to fetch
                        // .a == 1                          => explored

                        if (pixel.r == 0 && pixel.g == 0)
                            pixel.rgba = _exploredNew.rgba;
                        else
                            pixel.rgba = _exploredOld.rgba;

                        isNewOne = true;

                        // Get map data in a small radius
                        int iBegin = i - 1 >= 0 ? i - 1 : 0;
                        int iEnd   = i + 1 < _textureSize ? i + 2 : _textureSize;
                        for (int di = iBegin; di < iEnd; ++di)
                        {
                            int dit    = di * _textureSize;

                            int jBegin = j - 1 >= 0 ? j - 1 : 0;
                            int jEnd   = j + 1 < _textureSize ? j + 2 : _textureSize;

                            for (int dj = jBegin; dj < jEnd; ++dj)
                            {
                                int rpos = it + j;

                                pixel    = ref _explored[rpos];
                                isNewOne = ref isNew[rpos];

                                if (rpos != pos && pixel.r == 0)
                                {
                                    if (pixel.g == 0)
                                    {
                                        pixel.rgba = _extendedNew.rgba;
                                        isNewOne   = true;
                                    }
                                    else
                                    {
                                        if (!isNewOne)
                                        {
                                            pixel.rgba = _extendedOld.rgba;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            });

            thread.Start();
            while (thread.IsAlive)
            {
                yield return null;
            }
        }

        public IEnumerator Fetch()
        {
            yield return InternalFetch();
        }
    }
}