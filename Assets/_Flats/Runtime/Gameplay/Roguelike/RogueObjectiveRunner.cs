using Flats.Core.Roguelike;
using UnityEngine;

/// <summary>
/// Bridge between a stage's objective definition and the world. Visuals are built on every
/// client from the replicated plan anchors; only the authority owns the pure machine and
/// evaluates Succeeded. "obj.clear" needs no runner (the controller counts planned enemies).
/// </summary>
public abstract class RogueObjectiveRunner
{
    protected RoguelikeController Controller;
    protected EncounterPlan Plan;
    public bool Succeeded { get; protected set; }
    public bool Failed { get; protected set; }
    public string ProgressText { get; protected set; }
    public virtual double RewardFraction { get { return 1.0; } }

    public static RogueObjectiveRunner Create(RoguelikeController controller, EncounterPlan plan)
    {
        RogueObjectiveRunner runner = null;
        string id = plan.IsFinale ? plan.finaleId : plan.objectiveId;
        switch (id)
        {
            case "obj.capture": runner = new CaptureRunner(); break;
            case "obj.carry": runner = new CarryRunner(); break;
            case "obj.protect": runner = new ProtectRunner(); break;
            case "obj.breakout": runner = new BreakoutRunner(); break;
            case "fin.commander": runner = new CommanderRunner(); break;
            case "fin.vault": runner = new VaultRunner(); break;
            case "fin.convoy": runner = new ConvoyRunner(); break;
            case "obj.clear": return null;
            default: Debug.Log("FLATS_ROGUE_OBJECTIVE fallback to clear for " + id); return null;
        }
        runner.Controller = controller; runner.Plan = plan; runner.ProgressText = "";
        runner.Build(controller, plan);
        return runner;
    }

    /// <summary>Seconds the field may stay below the floor before a reinforcement squad arrives.</summary>
    public const float ReinforceDelay = (float)RogueMissionRules.ReinforceDelaySeconds;
    float thinFor;
    /// <summary>The answer to this objective's interactions (the first step into a zone, a pickup, a hold): RogueMissionPressure.</summary>
    protected readonly RogueMissionPressure Pressure = new RogueMissionPressure();
    /// <summary>Enemies (alive plus planned) a contested place keeps on the field (RogueMissionRules.PressureFloor: 4 to 9).</summary>
    protected int PressureFloor { get { return RogueMissionRules.PressureFloor(Controller.State.ConnectedPlayers, Controller.State.encounter.depth); } }
    /// <summary>
    /// Authority, for objectives that are a fight over a place (Hold the Zone, Break Out): while the objective runs the field never
    /// stays empty. When the enemies alive plus those still planned fall below the floor (4 to 9 by squad size and chapter) for
    /// <see cref="ReinforceDelay"/>, a squad paid from the bonus pool (never the stage budget) arrives, within the concurrent cap;
    /// from chapter 2 it brings elites (RogueMissionRules.EliteCount).
    /// </summary>
    protected void KeepPressure(float dt)
    {
        var c = Controller;
        if (c == null || !c.IsAuthority || c.State == null || Succeeded || Failed) return;
        int alive = c.AliveEnemies, floor = PressureFloor;
        if (alive + c.UnreleasedEnemies >= floor) { thinFor = 0f; return; }
        thinFor += dt;
        if (thinFor < ReinforceDelay) return;
        thinFor = 0f;
        var enc = c.State.encounter;
        int wanted = Mathf.Min(floor - alive - c.UnreleasedEnemies, enc.concurrentCap - alive);
        int elites = RogueMissionRules.EliteCount(wanted, enc.depth, enc.difficulty);
        for (int i = 0; i < wanted; i++) c.SpawnExtraEnemy(RogueMissionRules.PressureRole(i, enc.depth), i < elites, c.PickSpawnPosition(), true);
    }

    public abstract void Build(RoguelikeController controller, EncounterPlan plan);
    public virtual void Tick(float dt) { }
    public virtual void OnEnemyKilled(RogueEnemyRole role) { }
    public virtual void OnCommand(RogueCommandMessage cmd) { }
    public virtual void OnClientEvent(RogueEventMessage e) { }
    public virtual void Dispose() { }
}
