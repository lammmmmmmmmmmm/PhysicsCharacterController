using UnityEngine;

namespace PhysicsCharacterController
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class WaterVolume : MonoBehaviour
    {
        [Header("Volume")]
        [Tooltip("Horizontal trigger whose upper face defines the water surface.")]
        [SerializeField] private BoxCollider _volumeCollider;

        public float SurfaceHeightMeters => _volumeCollider.bounds.max.y;
        public BoxCollider VolumeCollider => _volumeCollider;

        #region Unity Lifecycle

#if UNITY_EDITOR
        private void Reset()
        {
            _volumeCollider = GetComponent<BoxCollider>();
            _volumeCollider.isTrigger = true;
        }

        private void OnValidate()
        {
            if (!_volumeCollider.isTrigger)
            {
                Debug.LogError($"Water volume '{name}' requires its BoxCollider to be a trigger.", this);
            }

            Vector3 rotationDegrees = transform.eulerAngles;
            if (!Mathf.Approximately(rotationDegrees.x, 0f) || !Mathf.Approximately(rotationDegrees.z, 0f))
            {
                Debug.LogError($"Water volume '{name}' must remain horizontal; X and Z rotation must be zero.", this);
            }
        }
#endif

        #endregion

    }
}
