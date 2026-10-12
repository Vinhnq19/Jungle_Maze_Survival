using System.Collections;
using _Codebase_.Entity.Player;
using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(_Codebase_.Entity.Player.PlayerEntity))]
[RequireComponent(typeof(Animator))]
public class PlayerAnimation : MonoBehaviour
{

    #region Static Constant
    public readonly static string[] BASE_CLIP_NAME = new string[] {
        "idle",
        "move",
        "attack"
    };

    public readonly static string[] BASE_CLIP_DIRECTION = new string[] {
        "back",
        "side",
        "front"
    };

    #endregion

    private Animator playerAnimator;
    private PlayerEntity playerEntity;

    public bool IsAttacking { get; private set; } = false;

    [SerializeField] private GameObject leftHand;
    [SerializeField] private GameObject rightHand;

    public SkinConfig AnimationSkinData;

    private Vector3 originHandPos;

    private Sequence idleSequence;


    private void Start()
    {
        playerAnimator = GetComponent<Animator>();
        playerEntity = GetComponent<PlayerEntity>();

        originHandPos = leftHand.transform.localPosition;

        var overrideAnimation = new AnimatorOverrideController(playerAnimator.runtimeAnimatorController);
        PlayerAnimationClip[] clips = { AnimationSkinData.idleClip, AnimationSkinData.walkClip, AnimationSkinData.attackClip };

        for (int i = 0; i < BASE_CLIP_NAME.Length; i++)
        {
            for (int j = 0; j < BASE_CLIP_DIRECTION.Length; j++)
            {
                string clipName = $"{BASE_CLIP_NAME[i]}_{BASE_CLIP_DIRECTION[j]}";
                AnimationClip newClip = clips[i].GetClipByIndex(j);

                Debug.Log("vvv : " + clipName + " - " + newClip.name);
                if (newClip != null)
                {
                    overrideAnimation[clipName] = newClip;
                }
            }
        }
        playerAnimator.runtimeAnimatorController = overrideAnimation;

        //IdleAnimating();
    }

    private void Update()
    {
        playerAnimator.SetInteger("Direction", GetDicrection());
        playerAnimator.SetBool("IsMoving", playerEntity.IsMoving);
    }

    private void IdleAnimating()
    {
        if (idleSequence != null && idleSequence.IsActive())
        {
            idleSequence.Kill();
        }
        idleSequence = DOTween.Sequence();

        float moveDistance = 0.15f;
        float duration = 0.2f;

        idleSequence.Join(leftHand.transform.DOLocalMoveY(
            leftHand.transform.localPosition.y + moveDistance,
            duration
        ).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine));

        idleSequence.Join(rightHand.transform.DOLocalMoveY(
            rightHand.transform.localPosition.y + moveDistance,
            duration
        ).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine));
    }

    public void PlayAttackAnim()
    {
        if (IsAttacking) return;
        StartCoroutine(IEBodyAttackAnim());

        IsAttacking = true;

        //if swipe attack

        var holdPoint = this.playerEntity.holdPoint;

        Vector3 handPos = leftHand.transform.position;
        float pointAngle = holdPoint.transform.rotation.z;

        float attackAngle = 0f;
        switch (playerEntity.playerDirection)
        {
            case Direction.Up:
                attackAngle = 350;
                break;
            case Direction.Down:
                attackAngle = -280;
                break;
            case Direction.Left:
            case Direction.Right:
                attackAngle = -179f;
                break;
        }

        Vector3 handAttackOffset = Vector3.zero;
        switch (playerEntity.playerDirection)
        {
            case Direction.Up:
                handAttackOffset = new Vector3(-0.5f, -1.2f, 0f);
                break;
            case Direction.Down:
                handAttackOffset = new Vector3(0.2f, -2.1f, 0f);
                break;
            case Direction.Left:
                handAttackOffset = originHandPos + new Vector3(0.2f, 0f, 0f);
                break;
            case Direction.Right:
                handAttackOffset = originHandPos + new Vector3(0.2f, 0f, 0f);
                break;
        }


        leftHand.transform.DOMove(handPos + new Vector3(-0.05f, 0.05f, 0f), 0.1f);
        leftHand.transform.DOLocalRotate(new Vector3(0f, 0f, 10f), 0.1f)
        .OnComplete(() =>
        {
            if (playerEntity.playerDirection == Direction.Up)
            {
                var weapon = playerEntity.GetCurrentWeapon();
                if (weapon)
                {
                    weapon.GetComponentInChildren<SpriteRenderer>().sortingOrder = 2;
                }
            }
            leftHand.transform.DOLocalMove(handAttackOffset, 0.1f);
            leftHand.transform.DOLocalRotate(new Vector3(0f, 0f, 0f), 0.1f)
                .OnComplete(() =>
            {
                holdPoint.transform.DOLocalRotate(new Vector3(0f, 0f, attackAngle), 0.1f, RotateMode.FastBeyond360)
                    .OnComplete(() =>
                {
                    DOVirtual.DelayedCall(0.1f, () =>
                    {
                        holdPoint.transform.DOLocalRotate(
                            new Vector3(0f, 0f, pointAngle),
                            0.1f,
                            RotateMode.Fast
                        );
                        leftHand.transform.DOLocalMove(originHandPos, 0.1f);
                        IsAttacking = false;
                        if (playerEntity.playerDirection == Direction.Up)
                        {
                            playerEntity.GetCurrentWeapon().GetComponentInChildren<SpriteRenderer>().sortingOrder = 3;
                        }

                    });
                });
            });
        }).OnKill(() =>
        {
            if (playerEntity.playerDirection == Direction.Up)
            {
                var weapon = playerEntity.GetCurrentWeapon();
                if (weapon)
                {
                    weapon.GetComponentInChildren<SpriteRenderer>().sortingOrder = 3;
                }
            }
            // leftHand.transform.DOMove(handPos, 0.2f);
            leftHand.transform.localPosition = originHandPos;
            IsAttacking = false;
        });
    }

    public void PlayShootAnim()
    {
        Vector3 startPos = leftHand.transform.localPosition;
        Vector3 startRot = leftHand.transform.localEulerAngles;

        Vector3 recoilPos = startPos;

        switch (playerEntity.playerDirection)
        {
            case Direction.Up:
                recoilPos += new Vector3(0f, -0.1f, 0f);            // Giật lên trên
                break;

            case Direction.Down:
                recoilPos += new Vector3(0f, 0.1f, 0f);           // Giật xuống dưới
                break;

            case Direction.Left:
                recoilPos += new Vector3(0.1f, 0f, 0f);           // Giật về trái

                break;

            case Direction.Right:
                recoilPos += new Vector3(-0.1f, 0f, 0f);            // Giật về phải
                break;
        }

        leftHand.transform.DOLocalMove(recoilPos, 0.1f).SetEase(Ease.OutQuad)
            .OnComplete(() =>
        {
            leftHand.transform.DOLocalMove(startPos, 0.15f).SetEase(Ease.OutSine);
        });
    }

    private IEnumerator IEBodyAttackAnim()
    {
        this.playerAnimator.SetBool("IsAttacking", true);
        this.playerAnimator.SetTrigger("Attack");

        yield return new WaitForSeconds(0.45f);

        this.playerAnimator.SetBool("IsAttacking", false);
    }


    private int GetDicrection()
    {
        return playerEntity.playerDirection switch
        {
            Direction.Left => 1,
            Direction.Right => 1,
            Direction.Up => 0,
            Direction.Down => 2,
            _ => 1,
        };
    }
}

[System.Serializable]
public class PlayerAnimationClip
{
    public AnimationClip Back;
    public AnimationClip Side;
    public AnimationClip Front;

    public AnimationClip GetClipByIndex(int index)
    {
        return index switch
        {
            0 => Back,
            1 => Side,
            2 => Front,
            _ => null
        };
    }
}