using UnityEngine;

namespace AreaSurvivors
{
    public static class PickupAttractionMotion
    {
        public const float MinimumSpeedLeadOverPlayer = 2f;

        public static float ResolveSpeed(float configuredMinimumSpeed, float playerMoveSpeed)
        {
            float safePlayerMoveSpeed = Mathf.Max(0f, playerMoveSpeed);
            return Mathf.Max(
                Mathf.Max(0f, configuredMinimumSpeed),
                safePlayerMoveSpeed + MinimumSpeedLeadOverPlayer);
        }

        public static float EstimateWorstCaseTravelSeconds(
            Vector3 currentPosition,
            Vector3 targetPosition,
            float attractionSpeed,
            float playerMoveSpeed)
        {
            float catchUpSpeed = Mathf.Max(
                0.01f,
                Mathf.Max(0f, attractionSpeed) - Mathf.Max(0f, playerMoveSpeed));
            return Vector2.Distance(currentPosition, targetPosition) / catchUpSpeed;
        }

        public static Vector3 MoveTowardsTarget(
            Vector3 currentPosition,
            Vector3 targetPosition,
            float moveSpeed,
            float deltaTime)
        {
            return Vector3.MoveTowards(
                currentPosition,
                targetPosition,
                Mathf.Max(0f, moveSpeed) * Mathf.Max(0f, deltaTime));
        }
    }
}
