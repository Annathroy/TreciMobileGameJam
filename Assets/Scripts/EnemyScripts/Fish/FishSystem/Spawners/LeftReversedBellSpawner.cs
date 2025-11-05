using UnityEngine;

public class LeftReversedBellSpawner : SideSpawnerBase
{
    protected override void Awake() { side = Side.Left; }
    protected override BaseFishMover CreateAndConfigureMover(GameObject go)
    {
        var m = go.GetComponent<FishMover_ReversedBell>() ?? go.AddComponent<FishMover_ReversedBell>();
        return m;
    }
}