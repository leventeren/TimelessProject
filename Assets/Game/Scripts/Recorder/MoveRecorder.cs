using Game.Player.Recorder.Scripts.Jobs;
using Game.Player.Scripts;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Game.Player.Recorder.Scripts
{
    public class MoveRecorder : MonoBehaviour
    {
        [SerializeField] private PlayerController target;
        [SerializeField] private int maxFrames = 600;
        
        [Header("Performance Settings")]
        [SerializeField] private bool useJobsSystem = false;
        [SerializeField, ShowIf("useJobsSystem")] 
        private bool useBurstCompiler = true;

        private PlayerFrame[] buffer;
        private int head = 0;
        private int count = 0;
        
        private float recordingStartTime = 0f;
        private float relativeTime = 0f;
        private int frameCounter = 0;
        
        private NativeArray<PlayerFrame> nativeBuffer;
        private bool isNativeBufferAllocated = false;

        private void Awake()
        {
            buffer = new PlayerFrame[maxFrames];
            
            if (target == null)
                Debug.LogError($"[MoveRecorder] Target PlayerController is not assigned on {gameObject.name}!");
            
            if (useJobsSystem)
            {
                AllocateNativeBuffer();
            }
        }

        private void OnDestroy()
        {
            DisposeNativeBuffer();
        }

        private void OnDisable()
        {
            DisposeNativeBuffer();
        }
        
        private void Start()
        {
            recordingStartTime = Time.time;
            relativeTime = 0f;
            frameCounter = 0;
        }
        
        private void FixedUpdate()
        {
            if (target == null || target.animator == null)
                return;
            
            relativeTime = frameCounter * Time.fixedDeltaTime;
            frameCounter++;

            RecordFrame();
        }

        private void RecordFrame()
        {
            var frame = new PlayerFrame
            {
                position = target.transform.position,
                //rotation = target.transform.rotation,
                normalizedAnimTime = target.animator.GetCurrentAnimatorStateInfo(0).normalizedTime,
                moveDirection = target.GetMoveDirection(),
                isJumping = target.IsJumping,
                time = relativeTime
            };
            
            buffer[head] = frame;
            
            if (useJobsSystem && isNativeBufferAllocated)
            {
                nativeBuffer[head] = frame;
            }
            
            head = (head + 1) % maxFrames;
            
            if (count < maxFrames)
                count++;
        }

        public bool GetFrame(int index, out PlayerFrame frame)
        {
            if (index < 0 || index >= count)
            {
                frame = default;
                return false;
            }

            var actualIndex = (head - count + index + maxFrames) % maxFrames;
            frame = buffer[actualIndex];
            return true;
        }

        public int FrameCount => count;

        public float GetFirstFrameTime()
        {
            if (count == 0)
                return 0f;
            
            GetFrame(0, out var frame);
            return frame.time;
        }

        public float GetLastFrameTime()
        {
            if (count == 0)
                return 0f;
            
            GetFrame(count - 1, out var frame);
            return frame.time;
        }
        
        public int FindFrameIndexByTime(float t)
        {
            if (count == 0)
                return -1;
            
            if (count == 1)
                return 0;
            
            if (useJobsSystem && isNativeBufferAllocated)
            {
                return FindFrameIndexByTimeJob(t);
            }
            
            return FindFrameIndexByTimeClassic(t);
        }
        
        private int FindFrameIndexByTimeClassic(float t)
        {
            var left = 0;
            var right = count - 1;
            
            while (left < right)
            {
                var mid = (left + right) / 2;
                
                GetFrame(mid, out var frame);
                
                if (frame.time < t)
                    left = mid + 1;
                else
                    right = mid;
            }
            
            return left;
        }
        
        private int FindFrameIndexByTimeJob(float t)
        {
            var resultIndex = new NativeArray<int>(1, Allocator.TempJob);
            
            var job = new BinarySearchJob
            {
                buffer = nativeBuffer,
                maxFrames = maxFrames,
                head = head,
                count = count,
                targetTime = t,
                resultIndex = resultIndex
            };

            var handle = job.Schedule();
            handle.Complete();
            
            var result = resultIndex[0];
            resultIndex.Dispose();
            
            return result;
        }
        
        public void Clear()
        {
            head = 0;
            count = 0;
            relativeTime = 0f;
            frameCounter = 0;
            recordingStartTime = Time.time;
        }
        
        public float GetRecordingDuration()
        {
            return GetLastFrameTime() - GetFirstFrameTime();
        }
        
        public string GetDebugInfo()
        {
            if (count == 0)
                return "Buffer: Empty";
            
            var duration = GetLastFrameTime() - GetFirstFrameTime();
            var fillPercentage = (count / (float)maxFrames) * 100f;
            
            var mode = useJobsSystem ? (useBurstCompiler ? "Jobs+Burst" : "Jobs") : "Classic";
            
            return $"Buffer: {count}/{maxFrames} frames ({fillPercentage:F1}%) | Duration: {duration:F2}s | Head: {head} | Mode: {mode}";
        }
        
        private void AllocateNativeBuffer()
        {
            if (isNativeBufferAllocated)
                return;
                
            nativeBuffer = new NativeArray<PlayerFrame>(maxFrames, Allocator.Persistent);
            
            for (var i = 0; i < count; i++)
            {
                if (GetFrame(i, out var frame))
                {
                    var actualIndex = (head - count + i + maxFrames) % maxFrames;
                    nativeBuffer[actualIndex] = frame;
                }
            }
            
            isNativeBufferAllocated = true;
            Debug.Log("[MoveRecorder] Native buffer allocated for Jobs System");
        }

        private void DisposeNativeBuffer()
        {
            if (isNativeBufferAllocated && nativeBuffer.IsCreated)
            {
                nativeBuffer.Dispose();
                isNativeBufferAllocated = false;
                Debug.Log("[MoveRecorder] Native buffer disposed");
            }
        }

        private void SetUseJobsSystem(bool enable)
        {
            if (enable == useJobsSystem)
                return;
            
            useJobsSystem = enable;
            
            if (enable)
            {
                AllocateNativeBuffer();
            }
            else
            {
                DisposeNativeBuffer();
            }
        }
        
        public NativeArray<PlayerFrame> GetNativeBuffer()
        {
            return nativeBuffer;
        }

        public bool IsUsingJobs => useJobsSystem && isNativeBufferAllocated;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (buffer == null || count == 0)
                return;

            Gizmos.color = Color.cyan;
            
            for (var i = 0; i < count - 1; i++)
            {
                if (GetFrame(i, out var currentFrame) && GetFrame(i + 1, out var nextFrame))
                {
                    Gizmos.DrawLine(currentFrame.position, nextFrame.position);
                }
            }
            
            if (GetFrame(0, out var firstFrame))
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(firstFrame.position, 0.3f);
            }
            
            if (GetFrame(count - 1, out var lastFrame))
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(lastFrame.position, 0.3f);
            }
        }
        
        [Button("Toggle Jobs System"), PropertySpace(10)]
        private void ToggleJobsSystem()
        {
            SetUseJobsSystem(!useJobsSystem);
        }
        
        [Button("Clear Recording"), PropertySpace(5)]
        private void ClearRecording()
        {
            Clear();
            Debug.Log("[MoveRecorder] Recording cleared");
        }
#endif
        
    }
}