using UnityEngine;
using UnityEngine.AddressableAssets;

namespace PhysicsCharacterController.Tests.PlayMode
{
    [CreateAssetMenu(fileName = "SwimmingTestAssetConfigSO", menuName = "Physics Character Controller/Character Testing/Swimming Test Asset Config")]
    public sealed class SwimmingTestAssetConfigSO : ScriptableObject
    {
        [Header("Production Prefabs")]
        [Tooltip("Complete production player prefab used by swimming integration tests.")]
        [SerializeField] private AssetReferenceGameObject _playerPrefab;
        [Tooltip("Production swimming pool prefab containing water and collision fixtures.")]
        [SerializeField] private AssetReferenceGameObject _swimmingPoolPrefab;

        public AssetReferenceGameObject PlayerPrefab => _playerPrefab;
        public AssetReferenceGameObject SwimmingPoolPrefab => _swimmingPoolPrefab;
    }
}
