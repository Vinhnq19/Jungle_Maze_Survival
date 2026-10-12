using UnityEngine;

namespace _Concrete_.Entities.Projectile
{
    public class ProjectileOnDestroyVfx : MonoBehaviour
    {
        public void DestroySelf()
        {
            Destroy(this.gameObject);
        }
    }
}