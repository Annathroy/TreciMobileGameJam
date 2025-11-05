using UnityEngine;

public class LeftStraightSpawner : SideSpawnerBase
{
    protected override void Awake() { side = Side.Left; }
    protected override BaseFishMover CreateAndConfigureMover(GameObject go)
    {
        var m = go.GetComponent<FishMover_Straight>() ?? go.AddComponent<FishMover_Straight>();
        return m;
    }
}