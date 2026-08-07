using Cysharp.Threading.Tasks;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VolumeBox.Toolbox;

namespace VolumeBox.Toolbox.Tests
{
    [PrebuildSetup(typeof(TestPrebuild))]
    internal class TravelerTests : ToolboxTestBase
    {
        public static string compare = "null";

        private const string TestOneScene = "Test1";
        private const string TestTwoScene = "Test2";
        
        [UnityTest, PrebuildSetup(typeof(TestPrebuild))]
        public IEnumerator SceneManagementTest()
        {
            yield return Toolbox.Traveler.LoadScene(TestOneScene).ToCoroutine();
            
            Assert.AreEqual(true, Toolbox.Traveler.IsSceneOpened(TestOneScene));
            Assert.AreEqual(true, SceneManager.GetSceneByName(TestOneScene).isLoaded);


            yield return Toolbox.Traveler.UnloadScene(TestOneScene).ToCoroutine();

            Assert.AreEqual(false, SceneManager.GetSceneByName(TestOneScene).isLoaded);
            Assert.AreEqual(false, Toolbox.Traveler.IsSceneOpened(TestOneScene));

            var list = new List<UniTask>
            {
                Toolbox.Traveler.LoadScene(TestOneScene),
                Toolbox.Traveler.LoadScene(TestTwoScene),
                Toolbox.Traveler.UnloadScene(TestOneScene),
                Toolbox.Traveler.UnloadScene(TestTwoScene)
            };

            yield return UniTask.WhenAll(list).ToCoroutine();

            Assert.AreEqual(true, SceneManager.GetSceneByName(TestOneScene).isLoaded);
            Assert.AreEqual(true, Toolbox.Traveler.IsSceneOpened(TestOneScene));
            Assert.AreEqual(true, SceneManager.GetSceneByName(TestTwoScene).isLoaded);
            Assert.AreEqual(true, Toolbox.Traveler.IsSceneOpened(TestTwoScene));

            yield return null;
        }

        [UnityTest, PrebuildSetup(typeof(TestPrebuild))]
        public IEnumerator SceneSetupUnloadTest()
        {
            var args = ScriptableObject.CreateInstance<TestSceneArgs>();
            args.TestString = "loaded";
            yield return Toolbox.Traveler.LoadScene(TestOneScene, args).ToCoroutine();
            Assert.AreEqual("loaded", compare);
            yield return Toolbox.Traveler.UnloadScene(TestOneScene).ToCoroutine();
            Assert.AreEqual("unloaded", compare);
            
            yield return null;
        }
    }
}
