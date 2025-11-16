using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Game.Player.Recorder.Scripts.Jobs
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
    public struct BinarySearchJob : IJob
    {
        public NativeArray<PlayerFrame> buffer;
        public int maxFrames;
        public int head;
        public int count;
        public float targetTime;
        
        public NativeArray<int> resultIndex;

        public void Execute()
        {
            if (count == 0)
            {
                resultIndex[0] = -1;
                return;
            }
            
            if (count == 1)
            {
                resultIndex[0] = 0;
                return;
            }
            
            var left = 0;
            var right = count - 1;
            
            while (left < right)
            {
                var mid = (left + right) / 2;
                var actualIndex = (head - count + mid + maxFrames) % maxFrames;
                
                if (buffer[actualIndex].time < targetTime)
                    left = mid + 1;
                else
                    right = mid;
            }
            
            resultIndex[0] = left;
        }
    }
}