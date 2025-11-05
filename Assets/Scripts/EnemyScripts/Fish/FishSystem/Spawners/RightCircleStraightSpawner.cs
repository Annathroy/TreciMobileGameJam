using UnityEngine;

public class RightCircleStraightSpawner : SideSpawnerBase
{
    protected override void Awake() { side = Side.Right; }
    protected override BaseFishMover CreateAndConfigureMover(GameObject go)
    {
        var m = go.GetComponent<FishMover_CircleThenStraight>() ?? go.AddComponent<FishMover_CircleThenStraight>();
        return m;
    }
}