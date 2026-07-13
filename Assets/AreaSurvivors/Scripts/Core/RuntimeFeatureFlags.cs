using UnityEngine;

namespace AreaSurvivors
{
    public static class RuntimeFeatureFlags
    {
        public static bool ShowTestFeatures => Application.isEditor || Debug.isDebugBuild;

        // Keep the complete opening story setup available while excluding it from the normal launch flow.
        public static bool PlayOpeningStoryOnApplicationLaunch => false;
    }
}
