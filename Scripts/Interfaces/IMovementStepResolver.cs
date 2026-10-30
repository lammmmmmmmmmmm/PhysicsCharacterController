using UnityEngine;

namespace PhysicsCharacterController
{
    public interface IMovementStepResolver
    {
        bool TryApplyStep(Vector3 velocityMetersPerSecond, float deltaTimeSeconds);
    }
}
