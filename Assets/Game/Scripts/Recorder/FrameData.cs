using UnityEngine;

namespace Game.Player.Recorder.Scripts
{
    [System.Serializable]
    public struct PlayerFrame
    {
        public Vector3 position;
        public Quaternion rotation;
        public float normalizedAnimTime;
        
        public Vector2 moveDirection;
        public bool isJumping;
        public float time; // Time.time
    }
}