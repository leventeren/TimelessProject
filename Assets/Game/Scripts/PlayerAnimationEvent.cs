using Game.Player.Scripts;
using UnityEngine;

namespace Game.Player.Animation.Scripts
{
    public class PlayerAnimationEvent : MonoBehaviour
    {
        [SerializeField] private PlayerController playerController;
        
        public void OnJumpAnimationComplete()
        {
            playerController.OnJumpEnd();
        }
    }
}

