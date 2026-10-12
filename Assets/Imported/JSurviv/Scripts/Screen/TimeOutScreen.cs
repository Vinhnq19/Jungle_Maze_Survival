using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Screen
{
    public class TimeOutScreen : BaseScreen
    {
        [SerializeField]
        private TextMeshProUGUI timeOutText;

        [SerializeField]
        private TextMeshProUGUI skipText;

        [SerializeField]
        private TextMeshProUGUI autoSkipText;

        private bool canAutoSkip;

        private bool   canSkipping;
        public  Action OnSkip;
        private float  timer;

        private void Awake()
        {
            this.skipText.gameObject.SetActive(false);
            this.autoSkipText.gameObject.SetActive(false);
            this.timer = 5f;
        }

        private void Update()
        {
            this.AnimTimeOutText();
            this.timer -= Time.deltaTime;

            if (this.timer <= 0f && !this.canSkipping)
            {
                this.skipText.gameObject.SetActive(true);
                this.StartCoroutine(this.ChangeTextSkipping());
                this.canSkipping = true;
                this.timer = 5f;
            }

            if (this.canSkipping)
            {
                if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Mouse0))
                {
                    this.OnSkip?.Invoke();
                }

                if (this.timer <= 0f && !this.canAutoSkip)
                {
                    this.canAutoSkip = true;
                    this.autoSkipText.gameObject.SetActive(true);
                    this.timer = 2.9f;
                }
                else if (this.canAutoSkip)
                {
                    this.autoSkipText.text = $"Auto skip after {Mathf.Ceil(this.timer)}...";

                    if (this.timer <= 0f)
                    {
                        this.OnSkip?.Invoke();
                    }
                }
            }
        }

        private void AnimTimeOutText()
        {
            this.timeOutText
                .transform
                .DOScale(
                    new Vector3(
                        1 + Mathf.Sin(Time.time * 10) * 0.1f,
                        1 + Mathf.Sin(Time.time * 10) * 0.1f,
                        1
                    ),
                    0.1f
                );
        }

        private IEnumerator ChangeTextSkipping()
        {
            const string initialText = "Enter to skip";

            do
            {
                this.skipText.text += ".";

                if (this.skipText.text == $"{initialText}....")
                {
                    this.skipText.text = initialText;
                }

                yield return new WaitForSeconds(0.2f);
            } while (this.enabled);
        }
    }
}