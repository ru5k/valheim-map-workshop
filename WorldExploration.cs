using System;
using System.Collections;
using System.Threading;
using UnityEngine;

namespace NomapPrinter
{
    public class WorldExploration
    {
        private readonly int  _mapScale;
        private readonly bool _showSharedMap;

        private int       _textureSize;
        private Color32[] _explored;

        public WorldExploration(int mapScale, bool showSharedMap, Color32[] explored = null)
        {
            _mapScale      = mapScale;
            _showSharedMap = showSharedMap;
            _explored      = explored;
        }

        public Color32[] Explored
        {
            get => _explored;
            private set {}
        }

        private bool IsExplored(int x, int y)
        {
            Color explorationPos = Minimap.instance.m_fogTexture.GetPixel(x / _mapScale, y / _mapScale);
            return explorationPos.r == 0f || _showSharedMap && explorationPos.g == 0f;
        }

        private IEnumerator InternalFetch()
        {
            _textureSize = _mapScale * Minimap.instance.m_textureSize;

            int arraySize = _textureSize * _textureSize;

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
                            continue;

                        ref Color32 pixel = ref _explored[pos];

                        if (pixel.r > 0 || pixel.g > 0)
                        {
                            pixel.r = 0;
                            pixel.g = byte.MaxValue;
                        }
                        else
                        {
                            pixel.r = byte.MaxValue;
                            pixel.g = 0;
                        }

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
                                pixel = ref _explored[dit + dj];
                                if (pixel.b > 0 || pixel.a > 0)
                                {
                                    pixel.b = 0;
                                    pixel.a = byte.MaxValue;
                                }
                                else
                                {
                                    pixel.b = byte.MaxValue;
                                    pixel.a = 0;
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