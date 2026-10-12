using UnityEngine;
using UnityEngine.EventSystems;

namespace Utils
{
    public class CameraDummyDrag : MonoBehaviour, IDragHandler
    {
        [SerializeField]
        private Transform _dragFollower;

        [SerializeField]
        private float _dragSpeed = .03f;

        public void OnDrag(PointerEventData eventData)
        {
            Vector3 delta = eventData.delta;
            var move = new Vector3(-delta.x * this._dragSpeed, -delta.y * this._dragSpeed, 0);
            this._dragFollower.Translate(move, Space.World);
        }
    }
}