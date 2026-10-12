using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Screen
{
    public class LoadingScreen : BaseScreen
    {
        [SerializeField]
        private Slider loadingSlider;

        private float fakeLoadTime = 7.0f;

        public bool IsDone { get; private set; }

        private void OnEnable()
        {
            this.IsDone = false;
            this.StartCoroutine(this.FakeLoadGameScreen(this.fakeLoadTime));
        }

        private IEnumerator FakeLoadGameScreen(float loadTime = 7.0f)
        {
            this.loadingSlider.value = 0f;

            var elapsedTime = 0.0f;

            while (elapsedTime < loadTime)
            {
                elapsedTime += Time.deltaTime;

                var progressValue = this.NonLinearProgress(elapsedTime / loadTime);

                this.loadingSlider.value = progressValue;

                yield return null;
            }

            this.loadingSlider.value = 1f;
            this.IsDone = true;
        }

        private float NonLinearProgress(float t)
        {
            return Mathf.Sin(t * Mathf.PI * 0.5f);
        }

        public void SetFakeLoadTime(float loadTime)
        {
            this.fakeLoadTime = loadTime;
        }
    }
}