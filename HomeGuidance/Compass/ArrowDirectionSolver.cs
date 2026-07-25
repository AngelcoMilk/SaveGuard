using UnityEngine;

namespace HomeGuidance.Compass;

public static class ArrowDirectionSolver
{
    /// <summary>
    /// Returns signed angle in degrees: positive = right, negative = left.
    /// Returns null if direction is too small.
    /// </summary>
    public static float? ComputeSignedAngle(Transform cameraTransform, Vector3 playerPos, Vector3 targetPoint)
    {
        var cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
        var targetDirection = Vector3.ProjectOnPlane(targetPoint - cameraTransform.position, Vector3.up);

        if (cameraForward.sqrMagnitude < 0.0001f || targetDirection.sqrMagnitude < 0.0001f)
            return null;

        return Vector3.SignedAngle(cameraForward.normalized, targetDirection.normalized, Vector3.up);
    }

    /// <summary>
    /// Horizontal distance from player to target. Return true if within near threshold.
    /// </summary>
    public static bool IsNearTarget(Vector3 playerPos, Vector3 targetPoint, float nearThreshold)
    {
        var flatDelta = new Vector3(targetPoint.x - playerPos.x, 0f, targetPoint.z - playerPos.z);
        return flatDelta.magnitude < nearThreshold;
    }
}
