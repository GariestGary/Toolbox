using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace VolumeBox.Toolbox
{
    public class AudioAlbumController
    {
        public string AlbumName => _Album.albumName;
        public PlayState CurrentPlayState { get; private set; }
        public PlayType CurrentPlayType { get; private set; }
        public float CurrentPlayTime => _Album.source.time;
        public AudioClip CurrentPlayingClip { get; private set; }

        public event Action StartedPlaying;
        public event Action StoppedPlaying;
        public event Action Paused;
        public event Action EndedPlaying;

        private readonly AudioAlbum _Album;
        private float _CurrentClipDuration;
        private CancellationTokenSource _PlaybackTokenSource = new();

        public AudioAlbumController(AudioAlbum album)
        {
            _Album = album;
            CurrentPlayState = PlayState.STOPPED;
            CurrentPlayType = PlayType.NONE;
        }

        public AudioClipInfo GetClip(string clipId)
        {
            return _Album.clips.FirstOrDefault(c => c.id == clipId);
        }
        
        private AudioClipInfo GetClip(List<AudioClipInfo> list, string id)
        {
            var clips = list.Where(x => x.id == id).ToArray();

            return clips.Length switch
            {
                0 => null,
                1 => clips[0],
                _ => clips[UnityEngine.Random.Range(0, clips.Length)]
            };
        }

        private void ResetPlaybackTokenSource()
        {
            _PlaybackTokenSource?.Cancel();
            _PlaybackTokenSource?.Dispose();
            _PlaybackTokenSource = new CancellationTokenSource();
        }

        public void Play(string clipId, float volume = 1, float pitch = 1, bool loop = false, PlayType playType = PlayType.ONE_SHOT)
        {
            var clipInfo = GetClip(_Album.clips, clipId);

            if (clipInfo == null)
            {
                Debug.LogWarning($"Clip with ID '{clipId}' not found");
                return;
            }

            Play(clipInfo.clip, volume, pitch, loop, playType);
        }
        
        public void Play(AudioClip clip, float volume = 1, float pitch = 1, bool loop = false, PlayType playType = PlayType.ONE_SHOT)
        {
            if (clip == null)
            {
                Debug.LogWarning($"Cannot play a null clip from album '{AlbumName}'");
                return;
            }

            AudioPlayer.Play(_Album.source, clip, volume, pitch, loop, playType);
            CurrentPlayState = PlayState.PLAYING;
            _CurrentClipDuration = clip.length;
            CurrentPlayType = playType;
            CurrentPlayingClip = clip;
            StartedPlaying?.Invoke();
            ResetPlaybackTokenSource();
            HandlePlayback(_PlaybackTokenSource.Token).Forget();
        }

        public void Stop()
        {
            if (CurrentPlayState == PlayState.STOPPED)
            {
                return;
            }
            
            _Album.source.Stop();
            CurrentPlayState = PlayState.STOPPED;
            ResetPlaybackTokenSource();
            StoppedPlaying?.Invoke();
        }

        public void Pause()
        {
            if (CurrentPlayState == PlayState.PAUSED || CurrentPlayState == PlayState.STOPPED)
            {
                return;
            }
            
            _Album.source.Pause();
            CurrentPlayState = PlayState.PAUSED;
            Paused?.Invoke();
        }

        private async UniTask HandlePlayback(CancellationToken token = default)
        {
            if (CurrentPlayingClip == null)
            {
                return;
            }
            
            while (_Album.source.time < _CurrentClipDuration)
            {
                if (token.IsCancellationRequested ||CurrentPlayingClip == null || CurrentPlayState == PlayState.STOPPED)
                {
                    break;
                }

                await UniTask.Yield(token);
            }
            
            HandleClipEnd();
        }

        private void HandleClipEnd()
        {
            if (CurrentPlayingClip != null && CurrentPlayState == PlayState.PLAYING)
            {
                EndedPlaying?.Invoke();
            }

            CurrentPlayingClip = null;
            CurrentPlayState = PlayState.STOPPED;
            CurrentPlayType = PlayType.NONE;
        }

        public void AddClip(AudioClipInfo audioClipInfo)
        {
            _Album.clips.Add(audioClipInfo);
        }

        public void PlayFormatted(string clip, float volume = 1, float pitch = 1, bool loop = false, PlayType playType = PlayType.ONE_SHOT)
        {
            var clipInfo = AudioPlayer.TryParseClipId(clip);

            if (clipInfo == null)
            {
                return;
            }
            
            Play(clipInfo.Value.clip, volume, pitch, loop, playType);
        }
    }

    public enum PlayState
    {
        STOPPED,
        PLAYING,
        PAUSED,
    }
}
