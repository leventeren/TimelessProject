using System.Collections;
using Game.Player.Recorder.Scripts;
using Game.Player.Scripts.Jobs;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using ReadOnlyAttribute = Sirenix.OdinInspector.ReadOnlyAttribute;

namespace Game.Player.Scripts
{
    public class GhostPlayer : MonoBehaviour
    {
        [SerializeField] private MoveRecorder recorder;
        [SerializeField] private Animator ghostAnimator;

        [Header("Performance Settings")] [SerializeField]
        private bool useJobsSystem;

        [SerializeField, ShowIf("useJobsSystem")]
        private bool useBurstCompiler = true;

        [Header("Playback Settings")]
        [SerializeField] private float timeScale = 1f;

        [SerializeField] private bool autoPlay = true;
        [SerializeField] private bool loopPlayback;
        [SerializeField] private bool reversePlayback;

        [Header("Timing Settings")]
        [Tooltip("Ghost'un kaç saniye gecikmeli başlayacağı (0.1f = 100ms geride)")]
        [SerializeField] private float playbackDelay;

        [Tooltip("Zaman kayması düzeltme faktörü (0 = kapalı, 0.1 = hafif, 1.0 = agresif)")]
        [SerializeField, Range(0f, 1f)] private float driftCorrectionFactor = 0.1f;

        [Header("Move Settings")]
        [SerializeField] private Vector3 moveOffset = Vector3.zero;

        [SerializeField, ReadOnly] private bool isJumping;

        [Header("Mirror Settings")]
        [OnValueChanged(nameof(OnMirrorModeChanged))]
        [SerializeField] private bool mirrorMode;

        [Header("Component References")]
        [SerializeField] private GhostPlayerColorComponent ghostPlayerColorComponent;

        [Header("Debug")]
        [SerializeField, ReadOnly] private float playbackTime;
        [SerializeField, ReadOnly] private float targetPlaybackTime;
        [SerializeField, ReadOnly] private float timeDrift;

        private static readonly int DirectionX = Animator.StringToHash("directionX");
        private static readonly int DirectionY = Animator.StringToHash("directionY");
        private static readonly int IsMoving = Animator.StringToHash("isMoving");
        private static readonly int IsJumpingHash = Animator.StringToHash("isJumping");
        private static readonly int SpeedMultiplierHash = Animator.StringToHash("speedMultiplier");
        private static readonly int JumpTriggerHash = Animator.StringToHash("jumpTrigger");

        private bool isPlaying = false;
        private int lastFrameIndex = -1;

        private float playbackStartTime = 0f;
        private float expectedPlaybackTime = 0f;
        
        private NativeArray<float> jobResult;
        private bool isJobResultAllocated = false;

        public GhostPlayerColorComponent ColorComponent => ghostPlayerColorComponent;
        
        #region Properties

        public bool IsPlaying => isPlaying;
        public float PlaybackTime => playbackTime;
        public bool IsReversed => reversePlayback;
        public bool IsUsingJobs => useJobsSystem && isJobResultAllocated;
        public float TimeDrift => timeDrift;

        private float PlaybackProgress
        {
            get
            {
                if (recorder == null || recorder.FrameCount == 0)
                    return 0f;

                var duration = recorder.GetLastFrameTime() - recorder.GetFirstFrameTime();
                return duration <= 0 ? 0f : Mathf.Clamp01((playbackTime - recorder.GetFirstFrameTime()) / duration);
            }
        }

        #endregion

        private void Awake()
        {
            UpdateAnimatorSpeed();

            if (useJobsSystem)
            {
                AllocateJobResources();
            }
        }

        private void OnDestroy()
        {
            DisposeJobResources();
        }

        private void OnDisable()
        {
            DisposeJobResources();
        }

        private void UpdateAnimatorSpeed()
        {
            if (ghostAnimator == null)
                return;

            ghostAnimator.speed = Mathf.Abs(timeScale);
        }

        private void Start()
        {
            if (autoPlay)
                StartCoroutine(WaitForFramesAndPlay());
        }

        private IEnumerator WaitForFramesAndPlay()
        {
            if (playbackDelay > 0f)
            {
                yield return new WaitForSeconds(playbackDelay);
            }

            while (recorder == null || recorder.FrameCount < 2)
            {
                yield return null;
            }

            Play();
        }

        private void FixedUpdate()
        {
            if (!isPlaying || recorder == null)
                return;

            ProcessPlayback(Time.fixedDeltaTime);
        }

        private void ProcessPlayback(float delta)
        {
            var direction = reversePlayback ? -1f : 1f;
            
            expectedPlaybackTime += delta * timeScale * direction;
            
            var targetTime = expectedPlaybackTime;
            
            if (driftCorrectionFactor > 0f)
            {
                timeDrift = targetTime - playbackTime;
                var correction = timeDrift * driftCorrectionFactor;
                playbackTime += (delta * timeScale * direction) + correction;
            }
            else
            {
                playbackTime += delta * timeScale * direction;
                timeDrift = 0f;
            }

            targetPlaybackTime = targetTime;

            var lastTime = recorder.GetLastFrameTime();
            var firstTime = recorder.GetFirstFrameTime();

            if (loopPlayback)
            {
                if (reversePlayback && playbackTime < firstTime)
                {
                    playbackTime = lastTime;
                    expectedPlaybackTime = lastTime;
                    lastFrameIndex = -1;
                }
                else if (!reversePlayback && playbackTime > lastTime)
                {
                    playbackTime = firstTime;
                    expectedPlaybackTime = firstTime;
                    lastFrameIndex = -1;
                }
            }
            else
            {
                playbackTime = Mathf.Clamp(playbackTime, firstTime, lastTime);
                expectedPlaybackTime = Mathf.Clamp(expectedPlaybackTime, firstTime, lastTime);

                if ((reversePlayback && playbackTime <= firstTime) ||
                    (!reversePlayback && playbackTime >= lastTime))
                {
                    isPlaying = false;
                    return;
                }
            }

            if (!TryGetInterpolatedFrame(playbackTime, out var interpolatedFrame))
                return;

            ApplyInterpolatedFrame(interpolatedFrame);
        }

        private bool TryGetInterpolatedFrame(float t, out PlayerFrame result)
        {
            result = default;

            if (recorder.FrameCount < 2)
                return false;

            if (useJobsSystem && recorder.IsUsingJobs && isJobResultAllocated)
            {
                return TryGetInterpolatedFrameJob(t, out result);
            }

            return TryGetInterpolatedFrameClassic(t, out result);
        }

        private bool TryGetInterpolatedFrameClassic(float t, out PlayerFrame result)
        {
            result = default;

            var index = recorder.FindFrameIndexByTime(t);

            if (index == lastFrameIndex && index > 0)
            {
                if (!recorder.GetFrame(index, out var curr) || !recorder.GetFrame(index - 1, out var prev))
                    return false;

                var blend = Mathf.InverseLerp(prev.time, curr.time, t);
                result = InterpolateFrames(prev, curr, blend, t);
                return true;
            }

            lastFrameIndex = index;

            if (index <= 0)
            {
                return recorder.GetFrame(0, out result);
            }

            if (index >= recorder.FrameCount)
            {
                return recorder.GetFrame(recorder.FrameCount - 1, out result);
            }

            if (!recorder.GetFrame(index, out var currentFrame))
                return false;

            if (!recorder.GetFrame(index - 1, out var previousFrame))
                return false;

            var blendFactor = Mathf.InverseLerp(previousFrame.time, currentFrame.time, t);
            result = InterpolateFrames(previousFrame, currentFrame, blendFactor, t);

            return true;
        }

        private bool TryGetInterpolatedFrameJob(float t, out PlayerFrame result)
        {
            result = default;

            var nativeBuffer = recorder.GetNativeBuffer();
            if (!nativeBuffer.IsCreated)
                return false;

            var index = recorder.FindFrameIndexByTime(t);
            lastFrameIndex = index;

            if (index <= 0)
            {
                return recorder.GetFrame(0, out result);
            }

            if (index >= recorder.FrameCount)
            {
                return recorder.GetFrame(recorder.FrameCount - 1, out result);
            }

            if (!recorder.GetFrame(index, out var currentFrame))
                return false;

            if (!recorder.GetFrame(index - 1, out var previousFrame))
                return false;

            var interpolationJob = new FrameInterpolationJob
            {
                prevFrame = previousFrame,
                currFrame = currentFrame,
                targetTime = t,
                result = jobResult
            };

            var handle = interpolationJob.Schedule();
            handle.Complete();

            var blendFactor = jobResult[0];
            result = InterpolateFrames(previousFrame, currentFrame, blendFactor, t);

            return true;
        }

        private static PlayerFrame InterpolateFrames(PlayerFrame prev, PlayerFrame curr, float blend, float time)
        {
            return new PlayerFrame
            {
                position = Vector3.Lerp(prev.position, curr.position, blend),
                //rotation = Quaternion.SlerpUnclamped(prev.rotation, curr.rotation, blend),
                normalizedAnimTime = Mathf.Lerp(prev.normalizedAnimTime, curr.normalizedAnimTime, blend),
                moveDirection = Vector2.Lerp(prev.moveDirection, curr.moveDirection, blend),
                isJumping = blend < 0.5f ? prev.isJumping : curr.isJumping,
                time = time
            };
        }

        private void ApplyInterpolatedFrame(PlayerFrame frame)
        {
            if (mirrorMode)
            {
                var newFramePosition = frame.position;
                newFramePosition.z = -newFramePosition.z;
                transform.position = newFramePosition + moveOffset;
            }
            else
            {
                transform.position = frame.position + moveOffset;
            }
            //transform.rotation = frame.rotation;

            if (ghostAnimator == null)
                return;

            var isMoving = frame.moveDirection.magnitude > 0.01f;

            if (mirrorMode)
            {
                ghostAnimator.SetFloat(DirectionX, -frame.moveDirection.x);
                ghostAnimator.SetFloat(DirectionY, frame.moveDirection.y);
            }
            else
            {
                ghostAnimator.SetFloat(DirectionX, frame.moveDirection.x);
                ghostAnimator.SetFloat(DirectionY, frame.moveDirection.y);
            }

            ghostAnimator.SetBool(IsMoving, isMoving);

            var isGhostPlayerJumping = frame.isJumping;
            ghostAnimator.SetBool(IsJumpingHash, isGhostPlayerJumping);

            if (frame.isJumping && !isJumping)
            {
                isJumping = true;
                ghostAnimator.SetTrigger(JumpTriggerHash);
            }
        }

        public void OnJumpEnd()
        {
            isJumping = false;
        }

        private void AllocateJobResources()
        {
            if (isJobResultAllocated)
                return;

            jobResult = new NativeArray<float>(1, Allocator.Persistent);
            isJobResultAllocated = true;
            Debug.Log("[GhostPlayer] Job resources allocated");
        }

        private void DisposeJobResources()
        {
            if (isJobResultAllocated && jobResult.IsCreated)
            {
                jobResult.Dispose();
                isJobResultAllocated = false;
                Debug.Log("[GhostPlayer] Job resources disposed");
            }
        }

        public void SetUseJobsSystem(bool enable)
        {
            if (enable == useJobsSystem)
                return;

            useJobsSystem = enable;

            if (enable)
            {
                AllocateJobResources();
            }
            else
            {
                DisposeJobResources();
            }
        }

        #region Public Control Methods

        [Button]
        public void Play()
        {
            if (recorder == null || recorder.FrameCount == 0)
            {
                Debug.LogWarning("[GhostPlayer] Cannot play: no frames recorded");
                return;
            }

            isPlaying = true;
            playbackStartTime = Time.time;
            
            if (reversePlayback)
            {
                playbackTime = recorder.GetLastFrameTime();
                expectedPlaybackTime = recorder.GetLastFrameTime();
            }
            else
            {
                playbackTime = recorder.GetFirstFrameTime();
                expectedPlaybackTime = recorder.GetFirstFrameTime();
            }

            lastFrameIndex = -1;
            timeDrift = 0f;
        }

        [Button]
        public void Pause()
        {
            isPlaying = false;
            ghostAnimator.speed = 0;
        }

        [Button]
        public void Resume()
        {
            isPlaying = true;
            expectedPlaybackTime = playbackTime;
            lastFrameIndex = -1;
            UpdateAnimatorSpeed();
            ToggleSpeedMultiplier();
        }

        [Button]
        public void Stop()
        {
            isPlaying = false;
            playbackTime = reversePlayback ? recorder.GetLastFrameTime() : recorder.GetFirstFrameTime();
            expectedPlaybackTime = playbackTime;
            lastFrameIndex = -1;
            timeDrift = 0f;

            if (ghostAnimator != null)
            {
                ghostAnimator.SetFloat(DirectionX, 0);
                ghostAnimator.SetFloat(DirectionY, 0);
                ghostAnimator.SetBool(IsMoving, false);
            }

            isJumping = false;
        }

        [Button]
        public void Restart()
        {
            Stop();
            Play();
        }

        [Button]
        public void SeekToTime(float time)
        {
            playbackTime = time;
            expectedPlaybackTime = time;
            lastFrameIndex = -1;
            timeDrift = 0f;
        }

        [Button]
        public void SetTimeScale(float scale)
        {
            timeScale = scale;
            UpdateAnimatorSpeed();
        }

        [Button]
        public void SetReverse(bool reverse)
        {
            reversePlayback = reverse;
            ToggleSpeedMultiplier();
        }

        [Button]
        public void ToggleReverse()
        {
            reversePlayback = !reversePlayback;
            ToggleSpeedMultiplier();
        }

        private void ToggleSpeedMultiplier()
        {
            if (ghostAnimator == null)
                return;

            ghostAnimator.SetFloat(SpeedMultiplierHash, reversePlayback ? -1f : 1f);
        }
        
        public void SetPlaybackDelay(float delay)
        {
            playbackDelay = delay;
        }
        
        public void SetDriftCorrection(float factor)
        {
            driftCorrectionFactor = Mathf.Clamp01(factor);
        }

        #endregion

        
        private void OnMirrorModeChanged()
        {
            transform.rotation = mirrorMode ? Quaternion.Euler(0f, -180f, 0f) : Quaternion.Euler(0f, 0f, 0f);
        }
        
        [Button("Toggle Jobs System"), PropertySpace(10)]
        private void ToggleJobsSystem()
        {
            SetUseJobsSystem(!useJobsSystem);
        }
    }
}