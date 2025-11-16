using Game.MirrorSystem.Scripts;
using UnityEngine;

namespace Game.Player.Scripts
{
    public class GhostPlayerColorComponent : MonoBehaviour
    {
        [SerializeField] private GameColors ghostColor;
        [SerializeField] private Renderer[] renderers;

        public GameColors GhostColor => ghostColor;

        public void SetColor(Color color)
        {
            foreach (var renderer in renderers)
            {
                foreach (var mat in renderer.materials)
                {
                    mat.color = color;
                }
            }
        }
    }
}