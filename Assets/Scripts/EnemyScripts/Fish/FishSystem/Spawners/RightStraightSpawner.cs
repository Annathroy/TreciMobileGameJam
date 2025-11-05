using UnityEngine;

public class RightStraightSpawner : SideSpawnerBase
{
    protected override void Awake() { side = Side.Right; }
    protected override BaseFishMover CreateAndConfigureMover(GameObject go)
    {
        var m = go.GetComponent<FishMover_Straight>() ?? go.AddComponent<FishMover_Straight>();
        return m;
    }
}