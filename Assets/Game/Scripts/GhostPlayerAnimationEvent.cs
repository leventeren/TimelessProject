using Game.Player.Scripts;
using UnityEngine;

namespace Game.Player.Animation.Scripts
{
    public class GhostPlayerAnimationEvent : MonoBehaviour
    {
        [SerializeField] private GhostPlayer ghostPlayerController;
        
        public void OnJumpAnimationComplete()
        {
            ghostPlayerController.OnJumpEnd();
        }
    }
}