using Game.Player.Recorder.Scripts;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Game.Player.Scripts.Jobs
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
    public struct FrameInterpolationJob : IJob
    {
        [Sirenix.OdinInspector.ReadOnly] public PlayerFrame prevFrame;
        [Sirenix.OdinInspector.ReadOnly] public PlayerFrame currFrame;
        [Sirenix.OdinInspector.ReadOnly] public float targetTime;

        [WriteOnly] public NativeArray<float> result;

        public void Execute()
        {
            var blend = 0f;

            var timeDiff = currFrame.time - prevFrame.time;
            if (timeDiff > 0.0001f)
            {
                blend = (targetTime - prevFrame.time) / timeDiff;
                blend = blend < 0f ? 0f : (blend > 1f ? 1f : blend);
            }

            result[0] = blend;
        }
    }
}