using UnityEngine;
using Managers; 

public class ReloadButtonController : MonoBehaviour
{

    public void OnReloadButtonPressed()
    {
        if (GameManager.I != null)
        {
            Debug.Log("Reload button pressed. Calling ReloadToLobby...");
            GameManager.I.ReloadToLobby();
        }
        else
        {
            Debug.LogError("GameManager instance not found!");
        }
    }
}