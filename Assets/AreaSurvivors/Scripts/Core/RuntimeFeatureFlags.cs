using UnityEngine;

namespace AreaSurvivors
{
    public static class RuntimeFeatureFlags
    {
        public static bool ShowTestFeatures => Application.isEditor || Debug.isDebugBuild;
    }
}
