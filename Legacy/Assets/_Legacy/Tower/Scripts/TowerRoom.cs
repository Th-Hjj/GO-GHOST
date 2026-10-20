using UnityEngine;

/// <summary>One authored S02 room root. No navigation, scene loading or narrative language.</summary>
public sealed class TowerRoom : MonoBehaviour
{
    public enum RoomKind { Passage, Gun, Blade, DevelopmentFinal, DevelopmentDisposition, Security }
    [SerializeField] private string stageId;
    [SerializeField] private RoomKind kind;
    [SerializeField] private Bounds localBounds = new Bounds(new Vector3(0, 3.25f, 0), new Vector3(30, 11.5f, 1));
    [SerializeField] private ChandelierEncounter prop;
    [SerializeField] private GameObject exitBarrier;
    [SerializeField] private string propEventInstanceId;
    [Header("50F local encounter")]
    [SerializeField] private Enemy securityTemplate;
    [SerializeField] private Floor50Receptionist receptionist;
    [Min(.1f)] [SerializeField] private float securityDelay = 2f; // Retained serialized field; the radio-call/death event now owns activation.
    [SerializeField] private TowerCharacter character;
    [SerializeField] private Transform resetUnit;
    public TowerCharacter Character => character;
    public Transform ResetUnit => resetUnit;
    [System.Serializable] public sealed class FinalProfile
    {
        [Min(.2f)] public float cadence = 1f;
        [Min(.2f)] public float recovery = 1f;
        [Min(1)] public int burstCount = 3;
    }
    [Header("P3 shared attacks: provisional editable profiles")]
    [SerializeField] private FinalProfile restrained = new FinalProfile {cadence=1.35f,recovery=1.6f,burstCount=2};
    [SerializeField] private FinalProfile fullForce = new FinalProfile {cadence=1f,recovery=1f,burstCount=3};
    [SerializeField] private FinalProfile overdrive = new FinalProfile {cadence=.65f,recovery=.6f,burstCount=5};
    public FinalProfile ProfileFor(TowerNarrativeState.Mode mode) => mode == TowerNarrativeState.Mode.Restrained ? restrained
        : mode == TowerNarrativeState.Mode.Overdrive ? overdrive : fullForce;
    public Enemy SecurityTemplate => securityTemplate;
    public Floor50Receptionist Receptionist => receptionist;
    public float SecurityDelay => securityDelay;
    public string StageId => stageId;
    public RoomKind Kind => kind;
    public ChandelierEncounter Prop => prop;
    public string PropEventInstanceId => propEventInstanceId;
    public Vector2 Offset => transform.position;
    public Vector3 BossSpawn => transform.position + (kind == RoomKind.DevelopmentFinal ? new Vector3(0,1,0) : new Vector3(9,-1.71f,0));
    public Bounds PlayableBounds => new Bounds(transform.position + localBounds.center, localBounds.size);
    public bool ExitOpen { get; private set; }
    public GameSessionFlow Flow { get; private set; }

    public void Bind(GameSessionFlow flow) => Flow = flow;
    public void SetExitOpen(bool open)
    {
        ExitOpen = open;
        if (exitBarrier != null) exitBarrier.SetActive(!open);
    }

    // Compatibility property: the old unopposed DEV opportunity is retired.
    public bool DevelopmentOpportunity => false;
}
