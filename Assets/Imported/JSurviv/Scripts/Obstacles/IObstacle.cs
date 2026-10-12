using _Codebase_.Entity.Player;
using UnityEngine;

public interface IPlayerEnter {
    void OnPlayerEnter(PlayerEntity player);
}

public interface IPlayerExit {
    void OnPlayerExit(PlayerEntity player);
}

public interface IPlayerStay {
    void OnPlayerStay(PlayerEntity player);
}