using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
#if UNITY_2023_2_OR_NEWER
using UnityEngine.Audio;
#endif

namespace VolumeBox.Toolbox
{
    public class AudioAlbumController
    {
        public string AlbumName => _Album.albumName;
        public PlayState CurrentPlayState { get; private set; }
        public PlayType CurrentPlayType { get; private set; }
        public float CurrentPlayTime => _Album.source.time;
        public AudioClip CurrentPlayingClip { get; private set; }
#if UNITY_2023_2_OR_NEWER
        public AudioResource CurrentPlayingResource { get; private set; }
#endif

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

        internal AudioAlbum Album => _Album;

        public AudioClipInfo GetClip(string clipId)
        {
            return _Album.clips?.FirstOrDefault(clip => clip.id == clipId);
        }
        
        private AudioClipInfo GetClip(List<AudioClipInfo> list, string id)
        {
            if (list == null)
            {
                return null;
            }

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

#if UNITY_2023_2_OR_NEWER
        public void Play(AudioClip clip, float volume = 1, float pitch = 1, bool loop = false, PlayType playType = PlayType.ONE_SHOT)
        {
            Play((AudioResource)clip, volume, pitch, loop, playType);
        }

        public void Play(AudioResource resource, float volume = 1, float pitch = 1, bool loop = false, PlayType playType = PlayType.ONE_SHOT)
        {
            if (resource == null)
            {
                Debug.LogWarning($"Cannot play a null audio resource from album '{AlbumName}'");
                return;
            }

            AudioPlayer.Play(_Album.source, resource, volume, pitch, loop, playType);
            CurrentPlayState = PlayState.PLAYING;
            CurrentPlayingResource = resource;
            CurrentPlayingClip = resource as AudioClip;
            _CurrentClipDuration = CurrentPlayingClip != null ? CurrentPlayingClip.length : 0;
            CurrentPlayType = playType;
            StartedPlaying?.Invoke();
            ResetPlaybackTokenSource();
            HandlePlayback(_PlaybackTokenSource.Token).Forget();
        }
#else
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
#endif

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
#if UNITY_2023_2_OR_NEWER
            if (CurrentPlayingResource == null)
#else
            if (CurrentPlayingClip == null)
#endif
            {
                return;
            }

#if UNITY_2023_2_OR_NEWER
            if (CurrentPlayingClip == null)
            {
                await UniTask.Yield(token);

                while (_Album.source.isPlaying)
                {
                    if (token.IsCancellationRequested || CurrentPlayingResource == null || CurrentPlayState == PlayState.STOPPED)
                    {
                        break;
                    }

                    await UniTask.Yield(token);
                }

                HandleClipEnd();
                return;
            }
#endif

            while (_Album.source.time < _CurrentClipDuration)
            {
                if (token.IsCancellationRequested || CurrentPlayingClip == null || CurrentPlayState == PlayState.STOPPED)
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
#if UNITY_2023_2_OR_NEWER
            CurrentPlayingResource = null;
#endif
            CurrentPlayState = PlayState.STOPPED;
            CurrentPlayType = PlayType.NONE;
        }

        public void AddClip(AudioClipInfo audioClipInfo)
        {
            _Album.clips ??= new List<AudioClipInfo>();
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
