using Flats.Core.Roguelike;
using UnityEngine;

/// <summary>
/// Authority-side bridge between a stage's objective definition and the world. Each runner
/// owns the world objects it creates and releases them in Dispose. The controller only asks
/// Succeeded/ProgressText and forwards enemy deaths and player commands.
/// "obj.clear" needs no runner (the controller counts the planned enemies). Other objectives,
/// events, emergencies and finales register their runners here as they are implemented.
/// </summary>
public abstract class RogueObjectiveRunner
{
    protected RoguelikeController Controller;
    protected EncounterPlan Plan;
    public bool Succeeded { get; protected set; }
    public bool Failed { get; protected set; }
    public string ProgressText { get; protected set; }

    public static RogueObjectiveRunner Create(RoguelikeController controller, EncounterPlan plan)
    {
        RogueObjectiveRunner runner = null;
        string id = plan.IsFinale ? plan.finaleId : plan.objectiveId;
        switch (id)
        {
            case "obj.clear": return null;
            default:
                // objectives without a runner yet degrade to Clear so a stage can never get stuck
                Debug.Log("FLATS_ROGUE_OBJECTIVE fallback to clear for " + id);
                return null;
        }
    }

    public virtual void Tick(float dt) { }
    public virtual void OnEnemyKilled(RogueEnemyRole role) { }
    public virtual void OnCommand(RogueCommandMessage cmd) { }
    public virtual void Dispose() { }
}
