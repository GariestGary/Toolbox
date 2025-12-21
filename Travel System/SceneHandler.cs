using Cysharp.Threading.Tasks;
using UnityEngine;

namespace VolumeBox.Toolbox
{
    public abstract class SceneHandler<TArgs> : SceneHandlerBase where TArgs : SceneArgs
    {
        protected TArgs Args;

        internal sealed override async UniTask OnLoadCallbackAsync(SceneArgs args)
        {
            Args = args as TArgs;

            if (Args != null)
            {
                if (args is not TArgs)
                {
                    Debug.Log($"Current loaded {gameObject.scene.name} scene expected {typeof(TArgs)} args, but provided with {args.GetType()}");
                }
            }
            
            await SetupSceneAsync(Args);
        }

        internal override async UniTask OnUnloadCallbackAsync()
        {
            await UnloadSceneAsync();
        }

        protected virtual UniTask UnloadSceneAsync()
        {
            return UniTask.CompletedTask;
        }

        protected virtual UniTask SetupSceneAsync(TArgs args)
        {
            return UniTask.CompletedTask;
        }
    }

    public class SceneHandlerBase : MonoCached
    {
        internal virtual UniTask OnLoadCallbackAsync(SceneArgs args)
        {
            return UniTask.CompletedTask;
        }

        internal virtual UniTask OnUnloadCallbackAsync()
        {
            return UniTask.CompletedTask;
        }
    }
}