using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VolumeBox.Toolbox.Tests
{
    [PrebuildSetup(typeof(TestPrebuild))]
    internal class AudioPlayerTests
    {
        [Test]
        public void NullClipDoesNotStartPlaybackTest()
        {
            var album = new AudioAlbum
            {
                albumName = "Test album",
                clips = new List<AudioClipInfo>()
            };
            var controller = new AudioAlbumController(album);

            Assert.DoesNotThrow(() => controller.Play((AudioClip)null));
            Assert.AreEqual(PlayState.STOPPED, controller.CurrentPlayState);
            Assert.IsNull(controller.CurrentPlayingClip);
        }
    }
}
