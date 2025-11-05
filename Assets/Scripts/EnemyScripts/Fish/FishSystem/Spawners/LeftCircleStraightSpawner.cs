using UnityEngine;

public class LeftCircleStraightSpawner : SideSpawnerBase
{
    protected override void Awake() { side = Side.Left; }
    protected override BaseFishMover CreateAndConfigureMover(GameObject go)
    {
        var m = go.GetComponent<FishMover_CircleThenStraight>() ?? go.AddComponent<FishMover_CircleThenStraight>();
        return m;
    }
}