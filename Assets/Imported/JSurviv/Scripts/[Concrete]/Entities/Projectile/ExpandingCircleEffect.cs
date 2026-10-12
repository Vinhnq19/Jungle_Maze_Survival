using UnityEngine;

namespace _Concrete_.Entities.Projectile
{
    public class ExpandingCircleEffect : MonoBehaviour
    {
        private readonly int  segments = 100;
        private          bool isExpanding;

        private LineRenderer lineRenderer;
        private float        radius;
        public float ExpansionSpeed { get; set; } = 10f;

        public float TargetRadius { get; set; }

        private void Update()
        {
            if (this.isExpanding)
            {
                if (this.radius < this.TargetRadius)
                {
                    this.radius += this.ExpansionSpeed * Time.deltaTime;
                    this.DrawCircle(this.radius);
                }
                else
                {
                    this.isExpanding = false;
                    Destroy(this.gameObject);
                }
            }
        }

        private void OnEnable()
        {
            this.lineRenderer = this.GetComponent<LineRenderer>();
            this.radius = 0f;
            this.lineRenderer.positionCount = this.segments + 1;
            this.isExpanding = true;

            var lineMaterial = new Material(Shader.Find("Sprites/Default"))
            {
                color = Color.white,
            };

            this.lineRenderer.material = lineMaterial;

            this.lineRenderer.startWidth = 0.1f;
            this.lineRenderer.endWidth = 2f;
        }

        private void DrawCircle(float radius)
        {
            var angle = 2 * Mathf.PI / this.segments;

            for (var i = 0; i <= this.segments; i++)
            {
                var x = Mathf.Cos(angle * i) * radius;
                var y = Mathf.Sin(angle * i) * radius;
                this.lineRenderer.SetPosition(i, new(x, y, 0));
            }
        }

        public void Initialize(float targetRadius, float expansionSpeed)
        {
            this.TargetRadius = targetRadius;
            this.ExpansionSpeed = expansionSpeed;
        }
    }
}