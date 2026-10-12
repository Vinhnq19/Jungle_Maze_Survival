using _Codebase_.Configs;
using DG.Tweening;
using UnityEngine;

namespace _Codebase_.Entity._Abstract_
{
    public abstract class BaseMovableEntity : MonoBehaviour
    {
        [field: SerializeField]
        public Transform Visual { get; set; }

        public Vector2 Position { get; set; }

        public bool IsMoving { get; private set; }

        private void Awake()
        {
            this.Position = new(this.transform.position.x - 0.5f, this.transform.position.y - 0.5f);
        }

        public virtual void Move(Vector2 direction, bool canLook = true)
        {
            this.IsMoving = true;

            float x = direction.x, y = direction.y;
            var newPosition = new Vector2(this.Position.x + x, this.Position.y + y);

            if (canLook) this.Look(new(x, y), GameConfig.I.stepTime);

            if (!this.CanMove(newPosition))
            {
                //cant move logic
                this.CantMoveAction();
                return;
            }

            this.Position = new(
                newPosition.x,
                newPosition.y
            );

            this.transform.DOMove(new Vector2(
                    newPosition.x + 0.5f,
                    newPosition.y + 0.5f
                ),
                GameConfig.I.stepTime - 0.01f
            ).OnComplete(() =>
                this.IsMoving = false
            );
        }


        public virtual void CantMoveAction()
        {
            this.Visual.DOShakePosition(0.4f, 0.6f).OnComplete(() =>
            {
                this.Visual.localPosition = Vector3.zero;
            });

            IsMoving = false;
        }

        public virtual void Look(Vector2 direction, float duration)
        {
            if (direction == Vector2.zero) return;

            Vector3 scale = this.Visual.localScale;

            // Flip theo trục X: nhìn sang trái thì scale.x = -|scale.x|, phải thì = +|scale.x|
            if (direction.x < 0)
                scale.x = -Mathf.Abs(scale.x);
            else if (direction.x > 0)
                scale.x = Mathf.Abs(scale.x);

            // Không thay đổi nếu đi lên hoặc xuống (chỉ flip trái/phải)
            this.Visual.localScale = scale;
        }



        public abstract bool CanMove(Vector2 newPosition);
    }
}