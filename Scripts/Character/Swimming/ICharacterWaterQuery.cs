using UnityEngine;

namespace PhysicsCharacterController
{
    public interface ICharacterWaterQuery
    {
        bool TryGetSurfaceHeight(Bounds characterBoundsMeters, out float surfaceHeightMeters);
    }
}
