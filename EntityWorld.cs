using System.Threading.Tasks;
using Unity.Entities;
using Unity.Scenes;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Partity
{
    public class EntityWorld : MonoBehaviour
    {
#if UNITY_EDITOR
        [SerializeField] private SceneAsset _SceneAsset;
#endif

        [SerializeField, HideInInspector] private Unity.Entities.Hash128 _SceneGUID;
        [SerializeField, HideInInspector] private string _SceneName;

        private World _value;
        private Entity _sceneEntity;
        private bool _sceneReady;
        private bool _sceneFailed;
        private TaskCompletionSource<World> _ready;

        /// <summary>
        /// The only way to access the world. <see cref="Task.IsCompleted"/> means the scene loaded successfully and
        /// the world is ready to use and to sweep. Sweeping is the consumer's job (Stop) and is scoped to
        /// <see cref="SceneTag"/> — scene-originated entities only — so the prefab sources, the scene meta chain and
        /// every system-created infrastructure entity survive it.
        /// </summary>
        public Task<World> Value
        {
            get
            {
                if (_sceneReady)
                    return Task.FromResult(_value);
                if (_ready == null)
                    _ready = new TaskCompletionSource<World>(TaskCreationOptions.RunContinuationsAsynchronously);
                return _ready.Task;
            }
        }

        // World creation and scene loading happen in Start — after every SubScene.OnEnable. AddSceneEntities
        // broadcasts scene meta entities to all worlds that exist at that moment, so creating the world any earlier
        // (Awake) would make ordinary subscenes load into this world too.
        private void Start()
        {
            _value = new World(_SceneName, WorldFlags.Game);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(_value, DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default));
            _sceneEntity = SceneSystem.LoadSceneAsync(_value.Unmanaged, _SceneGUID);
            if (_sceneEntity == Entity.Null)
            {
                // LoadSceneAsync has already logged the invalid GUID. Mark failed so Update stops polling, and
                // leave the wait incomplete — consumers never proceed, matching a failed load.
                _sceneFailed = true;
            }
        }

        // The world is driven manually (World.Update) instead of AppendWorldToCurrentPlayerLoop, so this component's
        // enabled flag is the world's heartbeat: disabling it freezes everything, scene streaming included. Never
        // also append the world to the player loop — it would tick twice per frame and every deltaTime would double.
        private void Update()
        {
            _value.Update();

            if (_ready == null || _sceneReady || _sceneFailed)
                return;

            var state = SceneSystem.GetSceneStreamingState(_value.Unmanaged, _sceneEntity);
            switch (state)
            {
                case SceneSystem.SceneStreamingState.LoadedSuccessfully:
                    _sceneReady = true;
                    _ready.TrySetResult(_value);
                    break;
                case SceneSystem.SceneStreamingState.FailedLoadingSceneHeader:
                case SceneSystem.SceneStreamingState.LoadedWithSectionErrors:
                    _sceneFailed = true;
                    Debug.LogError($"EntityWorld: scene {_SceneName} failed to load ({state}).");
                    break;
            }
        }

        private void OnDestroy()
        {
            // The wait is intentionally left incomplete: a world dying before its scene loaded has nothing to hand
            // out, and its waiters die with the same scene. No cancellation exception for callers to handle.
            if (_value == null || !_value.IsCreated)
                return;

            _value.Dispose();
            _value = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_SceneAsset == null)
                return;

            _SceneGUID = AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(_SceneAsset));
            _SceneName = _SceneAsset.name;
        }
#endif
    }
}
