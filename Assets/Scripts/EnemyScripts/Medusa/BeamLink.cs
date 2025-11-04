using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class BeamLink : MonoBehaviour
{
    [Header("References")]
    public Transform a;
    public Transform b;

    [Header("Beam")]
    [SerializeField] float beamRadius = 0.15f;
    [SerializeField] float lineWidth = 0.08f;
    [SerializeField] LayerMask playerMask;
    [SerializeField] int damagePerTick = 1;
    [SerializeField] float tickInterval = 0.2f;

    LineRenderer line;
    float nextTick;
    readonly HashSet<PlayerHealth> hitThisTick = new();

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.enabled = false;
    }

    public void Enable(bool on)
    {
        line.enabled = on && a && b;
        nextTick = 0f;
    }

    void Update()
    {
        if (!line.enabled || !a || !b) return;

        Vector3 pa = a.position, pb = b.position;
        line.SetPosition(0, pa);
        line.SetPosition(1, pb);

        if (Time.time < nextTick) return;
        nextTick = Time.time + tickInterval;
        hitThisTick.Clear();

        var cols = Physics.OverlapCapsule(pa, pb, beamRadius, playerMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < cols.Length; i++)
        {
            PlayerHealth ph = cols[i].GetComponent<PlayerHealth>();
            if (ph == null)
                ph = cols[i].GetComponentInParent<PlayerHealth>();

            if (ph != null && hitThisTick.Add(ph))
                ph.ApplyDamage(damagePerTick);
        }
    }
}

