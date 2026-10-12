using System.Collections.Generic;
using System.Linq;
using _Codebase_.EffectSystem;
using _Codebase_.Utils;
using _Concrete_.Effects;
using Managers;
using UnityEngine;

public class StoneDoor : MonoBehaviour
{
    [SerializeField]
    private float waitDuration = 20f; // Thời gian chờ khi mở hoặc đóng (20 giây)

    [SerializeField]
    private Vector2[] _tookPositions;

    [SerializeField]
    private string ID = "STONE_DOOR";

    private readonly HashSet<Vector2> tookPositions = new();

    private Animator animator;
    private bool     isOpen; // Trạng thái cửa
    private float    timer; // Bộ đếm thời gian


    private void Start()
    {
        this.animator = this.GetComponent<Animator>();
        // Bắt đầu ở trạng thái mở (theo Entry node)
        this.animator.SetBool("isOpen", this.isOpen);

        var originX = this.transform.position.x - 0.5f;
        var originY = this.transform.position.y - 0.5f;

        foreach (var tookPosition in this._tookPositions)
        {
            var pos = new Vector2(
                originX + tookPosition.x,
                originY + tookPosition.y
            );

            this.tookPositions.Add(pos);
        }
    }

    private void Update()
    {
        this.timer += Time.deltaTime;

        // Nếu đủ 20 giây, chuyển trạng thái
        if (this.timer >= this.waitDuration)
        {
            this.isOpen = !this.isOpen; // Chuyển trạng thái mở/đóng
            this.timer = 0f; // Reset timer

            // Cập nhật parameter trong Animator
            this.animator.SetBool("isOpen", this.isOpen);
        }
    }

    private void OnDrawGizmosSelected()
    {
        this.tookPositions.Clear();

        var originX = this.transform.position.x - 0.5f;
        var originY = this.transform.position.y - 0.5f;

        foreach (var tookPosition in this._tookPositions)
        {
            var pos = new Vector2(
                originX + tookPosition.x,
                originY + tookPosition.y
            );

            this.tookPositions.Add(pos);
        }

        Gizmos.color = Color.yellow;

        foreach (var pos in this.tookPositions)
        {
            Gizmos.DrawWireCube(pos, Vector3.one);
        }
    }

    public void OnDoorClosed()
    {
        MapManager.I.IndestructiblePositions.AddRange(this.tookPositions);

        PlayerManager.I.Players
            .Where(e => this.tookPositions.Contains(e.Position))
            .ForEach(e => e.AddEffect(new KnockBackEffect
            {
                Direction = Vector2.down,
                KnockbackPower = 1,
                StunOnBlockedDuration = 2.0f,
                Time = 1,
            }));
    }

    public void OnDoorOpened()
    {
        MapManager.I.IndestructiblePositions.RemoveAll(e => this.tookPositions.Contains(e));
    }
}