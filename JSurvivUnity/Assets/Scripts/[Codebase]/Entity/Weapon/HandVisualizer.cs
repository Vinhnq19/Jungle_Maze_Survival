using UnityEngine;

namespace _Codebase_.Entity.Weapon
{
    public class HandVisualizer : MonoBehaviour
    {
        [SerializeField]
        private Transform Visual;

        [SerializeField]
        private Vector3 onHandPosition;

        [SerializeField]
        private Vector3 onHandRotation;

        public void OnPickUp()
        {
            this.Visual.localPosition = this.onHandPosition;
            this.Visual.localEulerAngles = this.onHandRotation;
        }

        public void OnDrop()
        {
            this.Visual.localPosition = new(0, 0, 0);
        }
    }
}