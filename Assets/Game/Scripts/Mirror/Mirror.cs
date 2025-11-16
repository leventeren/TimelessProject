using System;
using Game.Player.Scripts;
using UnityEngine;

namespace Game.MirrorSystem.Scripts
{
    public class Mirror : MonoBehaviour
    {
        [SerializeField] private GameColors mirrorColor;
        [SerializeField] private bool isTrigger;
        public bool IsTriggered => isTrigger;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("GhostPlayer") && other.TryGetComponent(out GhostPlayer ghostPlayer))
            {
                if (ghostPlayer.ColorComponent.GhostColor == mirrorColor)
                {
                    isTrigger = true;
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("GhostPlayer") && other.TryGetComponent(out GhostPlayer ghostPlayer))
            {
                if (ghostPlayer.ColorComponent.GhostColor == mirrorColor)
                {
                    isTrigger = false;
                }
            }
        }
    }
}