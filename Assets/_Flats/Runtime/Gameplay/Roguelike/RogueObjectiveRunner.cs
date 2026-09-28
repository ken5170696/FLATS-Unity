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

    public abstract void Build(RoguelikeController controller, EncounterPlan plan);
    public virtual void Tick(float dt) { }
    public virtual void OnEnemyKilled(RogueEnemyRole role) { }
    public virtual void OnCommand(RogueCommandMessage cmd) { }
    public virtual void OnClientEvent(RogueEventMessage e) { }
    public virtual void Dispose() { }
}
