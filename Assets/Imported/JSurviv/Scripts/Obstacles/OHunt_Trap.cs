using _Codebase_.EffectSystem;
using _Codebase_.Entity.Player;
using _Concrete_.Effects;
using System;
using UnityEngine;

public class OHunt_Trap : ObstacleBase
{
    [SerializeField] private float stunCooldown = 10f;
    [SerializeField] private int stunTime = 3;

    // animation
    //[SerializeField] private Animator trapAnimator;
    //[SerializeField] private string closeAnimTrigger = "Close";
    //[SerializeField] private string openAnimTrigger = "Open";

    private bool canTrigger = true;
    private Animator _anim;

    private void Start()
    {
        base.Start();
        this._anim = GetComponent<Animator>();
    }

    protected override void OnPlayerEnter(PlayerEntity playerEntity)
    {
        if (!canTrigger) return;

        this._anim.SetBool("isAttack", true);
        playerEntity.AddEffect(new StunEffect
        {
            Time = this.stunTime
        });

        ActivateTrap();
    }

    private void ActivateTrap()
    {
        canTrigger = false;
        //if (trapAnimator != null)
        //    trapAnimator.SetTrigger(closeAnimTrigger);

        Invoke(nameof(ResetTrap), stunCooldown);
    }

    private void ResetTrap()
    {
        this._anim.SetBool("isAttack", false);
        canTrigger = true;
        //if (trapAnimator != null)
        //    trapAnimator.SetTrigger(openAnimTrigger);
    }
}
