using _Codebase_.Entity.Destructible;
using UnityEngine;

namespace _Concrete_.Entities.Destructible
{
    public class FlowerVase : DestructibleEntity
    {
        [SerializeField]
        private GameObject _brokenGlass;

        public override void OnBroken()
        {
            Instantiate(this._brokenGlass, new(this.transform.position.x, this.transform.position.y, 0), Quaternion.identity);
        }
    }
}