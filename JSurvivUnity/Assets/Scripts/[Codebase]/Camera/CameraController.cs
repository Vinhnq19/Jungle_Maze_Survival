using System.Linq;
using Managers;
using Unity.Cinemachine;
using UnityEngine;

namespace _Codebase_.Camera
{

    public class CameraController : MonoBehaviour
    {

        [SerializeField]
        private CinemachineTargetGroup targetGroup;

        [SerializeField]
        private CinemachineCamera virtualCamera;

        [SerializeField]
        private PlayerCamUI _target;

        [SerializeField]
        private float _scrollSpeed;

        [SerializeField]
        private Transform _dragFollower;

        private int   _numberOfSelected;
        private float _ortho;

        private float _radius;

        public float Ortho
        {
            get => this._ortho;
            set
            {
                if (value < 0.5) value = 0.5f;
                this._ortho = value;
            }
        }

        private void Update()
        {
            var scroll = Input.GetAxis("Mouse ScrollWheel");

            if (scroll != 0)
            {
                this.Ortho -= scroll * this._scrollSpeed;
                this.virtualCamera.Lens.OrthographicSize = this.Ortho;
            }
        }

        private void OnEnable()
        {
            this.Ortho = 5;
        }

        public void UpdateCams()
        {
            this._numberOfSelected = CameraManager.I.PlayerCams.Count(s => s.Selected);

            if (this._numberOfSelected == 0)
            {
                this._dragFollower.position = this.transform.position;
            }

            this._target = CameraManager.I.PlayerCams.FirstOrDefault(c => c.Selected);

            // Follow
            this.virtualCamera.Follow = this._numberOfSelected > 0
                ? this._target?.PlayerInfo.transform
                : this._dragFollower;
        }

        #region Singleton

        public static CameraController I { get; private set; }

        private void Awake()
        {
            I = this;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void RuntimeInitializeOnLoadMethod()
        {
            I = null;
        }

        #endregion

    }
}