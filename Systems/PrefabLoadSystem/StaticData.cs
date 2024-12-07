using Game.UI;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Platter.Systems {
    public partial class PrefabLoadSystem : UISystemBase {

        public static int4 blockSizes = new int4(2, 2, 6, 6);
        public static float m_CellSize = 8f;
        public static readonly string m_PrefabNamePrefix = "Parcel"; // temp until we iterate
        public static readonly Dictionary<string, string[]> m_PrefabNames = new Dictionary<string, string[]>() {
            { "subLanePrefab", new string[2] { "NetLaneGeometryPrefab", "EU Car Bay Line" } },
            { "roadPrefab", new string[2] { "RoadPrefab", "Alley" } },
            { "uiPrefab", new string[2] { "ZonePrefab", "EU Residential Mixed" } },
        };

        private struct CustomPrefabData {
            public int lotWidth;
            public int lotDepth;

            public CustomPrefabData(int w, int d) {
                lotWidth = w;
                lotDepth = d;
            }
        }
    }
}
