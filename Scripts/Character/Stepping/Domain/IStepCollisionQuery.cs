using UnityEngine;

namespace PhysicsCharacterController
{
    public interface IStepCollisionQuery
    {
        bool TryCast(Vector3 offsetMeters, Vector3 direction, float distanceMeters, out float hitDistanceMeters, out Vector3 normal);
        bool CanOccupy(Vector3 offsetMeters);
    }
}
