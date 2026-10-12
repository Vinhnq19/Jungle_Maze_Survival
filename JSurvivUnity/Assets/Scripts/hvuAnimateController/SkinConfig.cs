using UnityEngine;


[CreateAssetMenu(fileName = "Skin_", menuName = "ScriptableObjects/AnimationSkin")]
public class SkinConfig : ScriptableObject
{
    public PlayerAnimationClip idleClip;
    public PlayerAnimationClip walkClip;
    public PlayerAnimationClip attackClip;
}
