using JungleMaze.App.Hud.Feedback;
using UnityEngine;

namespace JungleMaze.Presentation.UI.Hud
{
    /// <summary>Camera adapter for offscreen checks; no gameplay visibility inference.</summary>
    public sealed class CameraWorldViewport:IWorldViewport
    {
        private Camera _camera;
        public CameraWorldViewport(Camera camera=null){_camera=camera;}
        public bool Contains(float worldX,float worldY)
        {
            if(_camera==null)_camera=Camera.main;if(_camera==null)return true;
            var p=_camera.WorldToViewportPoint(new Vector3(worldX,worldY,0));return p.z>=0&&p.x>=0&&p.x<=1&&p.y>=0&&p.y<=1;
        }
    }
}
