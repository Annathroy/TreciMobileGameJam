using UnityEngine;

public class RightReversedBellSpawner : SideSpawnerBase
{
    protected override void Awake() { side = Side.Right; }
    protected override BaseFishMover CreateAndConfigureMover(GameObject go)
    {
        var m = go.GetComponent<FishMover_ReversedBell>() ?? go.AddComponent<FishMover_ReversedBell>();
        return m;
    }
}