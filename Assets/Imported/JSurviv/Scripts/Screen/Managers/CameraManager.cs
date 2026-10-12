using System.Collections.Generic;
using _Codebase_.Camera;
using _Codebase_.Entity.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Managers
{
    public class CameraManager : MonoBehaviour
    {
        [SerializeField]
        private GameObject playerCamUI;

        [SerializeField]
        private Transform leftCams;

        [SerializeField]
        private Transform rightCams;

        private List<PlayerEntity> _players;
        public List<PlayerCamUI> PlayerCams { get; set; }
        public static CameraManager I { get; set; }

        private void Awake()
        {
            I = this;
            this.PlayerCams = new();
        }

        private void Update()
        {
            foreach (var cam in this.PlayerCams)
            {
                if (cam != null && cam.PlayerInfo != null)
                {
                    cam.ChangeInfo();
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void RuntimeInitializeOnLoadMethod()
        {
            I = null;
        }

        public void ShowCams()
        {
            DeSpawnAllCams();
            
            this._players = PlayerManager.I.Players;

            for (var i = 0; i < this._players.Count; i++)
            {
                var tex = new RenderTexture(240, 180, 16);
                this._players[i].followCamera.targetTexture = tex;
                var pcu = Instantiate(this.playerCamUI).GetComponent<PlayerCamUI>();
                pcu.transform.GetChild(0).GetComponent<RawImage>().texture = tex;
                pcu.PlayerInfo = this._players[i];
                this.PlayerCams.Add(pcu);

                if (i % 2 == 0)
                    pcu.transform.SetParent(this.leftCams);
                else
                    pcu.transform.SetParent(this.rightCams);
            }
        }


        public void DeSpawnAllCams()
        {
            for (int i = PlayerCams.Count - 1; i >= 0; i--)
            {
                if (PlayerCams[i] != null)
                {
                    Destroy(PlayerCams[i].gameObject);
                }
            }

            this.PlayerCams.Clear();

            if (CameraController.I != null)
            {
                CameraController.I.UpdateCams();
            }
            
            Debug.Log("CameraManager: All player cams despawned, list cleared, and main camera target updated.");
        }

        public void SelectAll()
        {
            foreach (var cam in this.PlayerCams)
            {
                cam.Selected = true;
            }
        }

        public void DeselectAll()
        {
            foreach (var cam in this.PlayerCams)
            {
                cam.Selected = false;
            }
        }
    }
}