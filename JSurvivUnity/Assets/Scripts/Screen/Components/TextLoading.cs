using System.Collections;
using TMPro;
using UnityEngine;

namespace Screen.Components
{
    public class TextLoading : MonoBehaviour
    {
        private TextMeshProUGUI textLoading;

        private void Awake()
        {
            this.textLoading = this.GetComponent<TextMeshProUGUI>();
        }

        private void OnEnable()
        {
            this.StartCoroutine(this.ChangeTextLoading());
        }

        private void OnDisable()
        {
            this.StopCoroutine(this.ChangeTextLoading());
        }

        private IEnumerator ChangeTextLoading()
        {
            this.textLoading.text = "Loading.";
            yield return new WaitForSeconds(0.2f);
            this.textLoading.text = "Loading..";
            yield return new WaitForSeconds(0.2f);
            this.textLoading.text = "Loading...";
            yield return new WaitForSeconds(0.2f);
            this.textLoading.text = "Loading";
            yield return new WaitForSeconds(0.2f);

            this.StartCoroutine(this.ChangeTextLoading());
        }
    }
}