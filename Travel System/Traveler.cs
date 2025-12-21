using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VolumeBox.Toolbox
{
    public class Traveler : MonoBehaviour
    {
        private AsyncOperation _currentUnloadingSceneOperation;
        private AsyncOperation _currentLoadingSceneOperation;
        private List<OpenedScene> _openedScenes = new List<OpenedScene>();

        private Messenger _Msg;
        private Updater _Upd;
        private Action<Component> _traverseAction;
        
        public void Initialize(Messenger msg, Updater upd)
        {
            _Msg = msg;
            _Upd = upd;
            _openedScenes = new List<OpenedScene>();
            _Msg.Subscribe<LoadSceneMessage>(m => LoadScene(m.SceneName, m.Args).Forget(), null, true);
            _Msg.Subscribe<UnloadSceneMessage>(m => UnloadScene(m.SceneName).Forget(), null, true);
            _Msg.Subscribe<UnloadAllScenesMessage>(_ => UnloadAllScenes().Forget(), null, true);
        }

        /// <summary>
        /// Returns SceneHandler of specified type if it exists among loaded scenes
        /// </summary>
        /// <typeparam name="T">Type of SceneHandler which located in necessary scene hierarchy</typeparam>
        /// <returns>Instance of an requested SceneHandler, or null if it doesn't exist</returns>
        public T TryGetSceneHandler<T>() where T: SceneHandlerBase
        {
            OpenedScene scene = null;

            for(int i = 0; i < _openedScenes.Count; i++)
            {
                if (_openedScenes[i].Handler != null && _openedScenes[i].Handler.GetType() == typeof(T))
                {
                    scene = _openedScenes[i];
                    break;
                }
            }

            if(scene == null)
            {
                return null;
            }
            else
            {
                return scene.Handler as T;
            }
        }

        /// <summary>
        /// Returns all SceneHandlers of specified type if they exists among loaded scenes
        /// </summary>
        /// <typeparam name="T">Type of SceneHandlers which located in necessary scene hierarchy</typeparam>
        /// <returns>List of all requested SceneHandlers, or empty list if they doesn't exist</returns>
        public List<T> TryGetAllSceneHandlers<T>() where T: SceneHandlerBase
        {
            List<T> openedScenes = new List<T>();

            for (int i = 0; i < _openedScenes.Count; i++)
            {
                if (_openedScenes[i].Handler != null && _openedScenes[i].Handler.GetType() == typeof(T))
                {
                    openedScenes.Add(_openedScenes[i].Handler as T);
                }
            }

            return openedScenes;
        }

        /// <summary>
        /// Returns true if specified scene is opened in hierarchy
        /// </summary>
        public bool IsSceneOpened(string sceneName)
        {
            return _openedScenes.Any(s => s.SceneDefinition.name == sceneName);
        }

        public async UniTask LoadScene(string sceneName, SceneArgs args = null)
        {
            await LoadScene<SceneHandlerBase>(sceneName, args);
        }

        public void SetTraverseAction(Action<Component> action)
        {
            _traverseAction = action;
        }

        /// <summary>
        /// Loads scene with given name and args.
        /// </summary>
        /// <remarks>
        /// It's recommend to use it with async/await, to prevent errors while loading and unloading scenes at the same time.
        /// </remarks>
        /// <param name="sceneName">scene name other than empty string</param>
        /// <param name="args">custom scene arguments, null by default</param>
        public async UniTask<T> LoadScene<T>(string sceneName, SceneArgs args = null) where T: SceneHandlerBase
        {
            if(!DoesSceneExist(sceneName))
            {
                Debug.LogWarning($"Scene with name {sceneName} you want to load doesn't exist");
                return null;
            }

            _Msg.Send(new SceneLoadingMessage(sceneName));
            await WaitForLoadingOperationsEnd(sceneName);
            var loadingOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            _currentLoadingSceneOperation = loadingOperation;
            await loadingOperation;
            _Msg.Send(new SceneLoadedMessage(sceneName));
            Scene sceneDefinition = SceneManager.GetSceneByName(sceneName);
            await UniTask.DelayFrame(1);
            GameObject[] sceneObjects = sceneDefinition.GetRootGameObjects();

            var allComponents = new List<Component>();

            foreach (var obj in sceneObjects)
            {
                var components = obj.GetComponentsInChildren<Component>(true);
                allComponents.AddRange(components);
            }
            
            var handler = allComponents.Find(x => x is SceneHandlerBase) as SceneHandlerBase;

            if (_traverseAction != null)
            {
                //traverse all components
                foreach (var component in allComponents)
                {
                    _traverseAction.Invoke(component);
                }
            }

            OpenedScene newOpenedScene = new OpenedScene(sceneDefinition, handler, args);

            if (handler != null)
            {
                await handler.OnLoadCallbackAsync(args);
            }

            _openedScenes.Add(newOpenedScene);
            
            _Upd.InitializeMonos(allComponents.Where(x => x is MonoCached).Cast<MonoCached>());
            
            _currentLoadingSceneOperation = null;
            //temp fix for situations when TryGetSceneHandler returns null after receiving SceneOpenedMessage
            await UniTask.DelayFrame(1);
            _Msg.Send(new SceneOpenedMessage(sceneName));
            return handler as T;
        }

        /// <summary>
        /// Unloads scene if it loaded now. It's recommend to use it with async/await, to prevent errors while loading and unloading scenes at the same time.
        /// </summary>
        /// <param name="sceneName">scene name other than empty string</param>
        public async UniTask UnloadScene(string sceneName)
        {
            OpenedScene sceneToUnload = _openedScenes.FirstOrDefault(x => x.SceneDefinition.name == sceneName);

            if (sceneToUnload == null)
            {
                Debug.LogWarning($"Scene with name {sceneName} you want to unload was not loaded before. Skipping");
                return;
            }

            await WaitForLoadingOperationsEnd(sceneName);

            _Msg.Send(new SceneUnloadingMessage(sceneName));

            if (sceneToUnload.Handler != null)
            {
                await sceneToUnload.Handler.OnUnloadCallbackAsync();
            }

            _Upd.RemoveObjectsFromUpdate(sceneToUnload.SceneDefinition.GetRootGameObjects());

            _currentUnloadingSceneOperation = SceneManager.UnloadSceneAsync(sceneName);

            while (_currentLoadingSceneOperation != null && !_currentUnloadingSceneOperation.isDone)
            {
                await UniTask.Yield();
            }

            _currentUnloadingSceneOperation = null;
            await Resources.UnloadUnusedAssets();
            _openedScenes.Remove(sceneToUnload);
            _Msg.Send(new SceneUnloadedMessage(sceneName));
        }

        private async UniTask WaitForLoadingOperationsEnd(string sceneName)
        {
            if(!CanLoadSceneNow(sceneName))
            {
                await UniTask.WaitUntil(() =>
                _currentUnloadingSceneOperation == null || 
                _currentUnloadingSceneOperation.isDone && 
                _currentLoadingSceneOperation == null || 
                _currentLoadingSceneOperation.isDone);

                _currentLoadingSceneOperation = null;
                _currentUnloadingSceneOperation = null;
                
                await UniTask.DelayFrame(1);
            }
        }

        private bool CanLoadSceneNow(string sceneName)
        {
            return _currentUnloadingSceneOperation == null && _currentLoadingSceneOperation == null && !string.IsNullOrEmpty(sceneName);
        }

        private bool DoesSceneExist(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                var lastSlash = scenePath.LastIndexOf("/");
                var sceneName = scenePath.Substring(lastSlash + 1, scenePath.LastIndexOf(".") - lastSlash - 1);

                if (string.Compare(name, sceneName, true) == 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Unloads all opened scenes except 'MAIN'
        /// </summary>
        public async UniTask UnloadAllScenes()
        {
            var unloadings = new List<UniTask>();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var sc = SceneManager.GetSceneAt(i);

                if (sc.name != "MAIN")
                {
                    unloadings.Add(UnloadScene(sc.name));
                }
            }

            await UniTask.WhenAll(unloadings);
        }

        private class OpenedScene
        {
            public Scene SceneDefinition { get; }
            public SceneArgs Args { get; } = null;
            public SceneHandlerBase Handler { get; } = null;

            public OpenedScene(Scene sceneDefinition, SceneHandlerBase sceneHandler, SceneArgs args)
            {
                SceneDefinition = sceneDefinition;
                Args = args;
                Handler = sceneHandler;
            }
        }
    }

    #region Traveler's class messages

    [Serializable]
    public class UnloadSceneMessage: Message
    {
        public string SceneName;
    }

    public class UnloadAllScenesMessage: Message
    {

    }

    [Serializable]
    public class LoadSceneMessage: Message
    {
        public string SceneName;
        public SceneArgs Args;
    }

    [Serializable]
    public abstract class SceneMessage: Message
    {
        public string SceneName => _sceneName;

        private string _sceneName;

        public SceneMessage(string sceneName)
        {
            _sceneName = sceneName;
        }

        public SceneMessage()
        {
            _sceneName = string.Empty;
        }
    }

    [Serializable]
    public class SceneUnloadingMessage : SceneMessage
    {
        public SceneUnloadingMessage(string sceneName) : base(sceneName)
        {
        }

        public SceneUnloadingMessage()
        {

        }
    }

    [Serializable]
    public class SceneUnloadedMessage : SceneMessage
    {
        public SceneUnloadedMessage(string sceneName) : base(sceneName)
        {
        }

        public SceneUnloadedMessage()
        {
            
        }
    }

    [Serializable]
    public class SceneLoadingMessage : SceneMessage
    {
        public SceneLoadingMessage(string sceneName) : base(sceneName)
        {
        }

        public SceneLoadingMessage()
        {

        }
    }

    [Serializable]
    public class SceneLoadedMessage : SceneMessage
    {
        public SceneLoadedMessage(string sceneName) : base(sceneName)
        {
        }

        public SceneLoadedMessage()
        {

        }
    }

    [Serializable]
    public class SceneOpenedMessage : SceneMessage
    {
        public SceneOpenedMessage(string sceneName) : base(sceneName)
        {
        }

        public SceneOpenedMessage()
        {

        }
    }

    #endregion
}
