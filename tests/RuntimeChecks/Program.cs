using IcarusAtmosphericBoost;
using UnityEngine;

int checks = 0;
void Check(bool condition, string reason)
{
    checks++;
    if (!condition) throw new Exception(reason);
}
PlayerController New(int ability = 2)
{
    BoostRuntime.Reset();
    Time.realtimeSinceStartupAsDouble = 10;
    Time.fixedDeltaTime = 1f/60;
    VFInput._jump = VFInput._moveForward = VFInput._moveBackward = VFInput._moveLeft = VFInput._moveRight = false;
    VFInput.inputing = false;
    GameMain.isPaused = false;
    UIRealtimeTip.Messages.Clear();
    LDB.techs.dataArray = Array.Empty<TechProto>();
    var c = new PlayerController();
    c.mecha.thrusterLevel = ability;
    GameMain.mainPlayer = c.player;
    GameMain.gameScenario.Handoffs = 0;
    BoostRuntime.Refresh(c);
    return c;
}
void Input(PlayerController c, bool jump, double advance)
{
    Time.realtimeSinceStartupAsDouble += advance;
    VFInput._jump = jump;
    BoostRuntime.Refresh(c);
}
void Charge(PlayerController c, int layers)
{
    Input(c,true,0);
    Input(c,true,layers*.75);
    Input(c,false,0);
}
void Tick(PlayerController c, bool nativeClamp = true)
{
    BoostRuntime.BeginTick(c);
    c.movementStateInFrame = c.player.movementState;
    if (c.player.sailing)
    {
        BoostRuntime.BeforeSail(c);
        c.player.uVelocity += c.universalGravity * Time.fixedDeltaTime;
        c.player.uPosition += c.player.uVelocity * Time.fixedDeltaTime;
        c.player.position = (Vector3)c.player.uPosition;
    }
    else
    {
        // Deliberately emulate the competing native clamp and hover force to
        // verify the real patch restores momentum and applies gravity once.
        if (nativeClamp) c.velocity = new Vector3(0,Math.Min(50,c.velocity.y)*.8f,0);
        float thrust = Math.Max(0,(215-c.player.position.y)*10+49.5f);
        BoostRuntime.PreserveTakeoffCoast(c,ref thrust);
        VectorLF3 force = c.localGravity + new Vector3(0,thrust,0);
        bool sleeping = true;
        BoostRuntime.BeforeForce(c,ref force,ref sleeping);
        c.velocity += (Vector3)force * Mathf.Clamp(Time.fixedDeltaTime,1f/60,.05f);
        c.player.position += c.velocity * Time.fixedDeltaTime;
        // Simple floor, no claim of emulating Unity collision response.
        if (c.player.position.y < 201) { c.player.position = new(0,201,0); c.velocity.y = Math.Max(0,c.velocity.y); }
        c.player.uPosition = c.player.position;
        c.player.uVelocity = c.velocity;
        c.player.movementState = c.movementStateInFrame;
    }
    Time.realtimeSinceStartupAsDouble += Time.fixedDeltaTime;
}

var c = New();
Charge(c,2);
Check(UIRealtimeTip.Messages.Count == 2, "Exactly one floating tip for each completed layer.");
Check(c.mecha.Payments == 0, "Holding and releasing before physics never drains energy.");
Tick(c);
Check(c.mecha.Payments == 1, "Launch charges exactly once.");
Check(Math.Abs(c.velocity.y - (150 - 49.5f/60)) < .01f && !c.player.sailing,
    "Release immediately assigns the two-layer speed, followed by gravity, without ground-level sailing.");
double energy = c.mecha.coreEnergy;
for (int i=0;i<150;i++) Tick(c);
Check(c.mecha.coreEnergy == energy && c.mecha.Payments == 1, "Coasting does not repeat the launch fee.");
Check(GameMain.gameScenario.Handoffs == 1 && c.player.sailing, "Unlocked ascent hands off to sailing once.");
Check(c.player.position.y > 250, "Handoff follows an actual climb.");

c = New();
Input(c,true,0);
Input(c,true,.75);
VFInput._moveForward = VFInput._moveBackward = true;
Input(c,true,0);
Check(UIRealtimeTip.Messages.Count == 2 && UIRealtimeTip.Messages[1].Contains("打断"), "Opposite keys cancel even with a zero movement axis.");
VFInput._moveForward = VFInput._moveBackward = false;
Input(c,true,10);
Input(c,false,0);
Tick(c);
Check(c.mecha.Payments == 0, "Interrupted hold cannot silently rearm.");

c = New();
Input(c,true,0);
VFInput._moveLeft = true;
Input(c,true,.5);
Input(c,false,0);
Tick(c);
Check(UIRealtimeTip.Messages.Count == 0 && c.mecha.Payments == 0, "Partial-layer cancellation is silent and free.");

c = New();
Charge(c,1);
VFInput._moveRight = true;
Tick(c);
Check(c.mecha.Payments == 0, "Direction cancels a release queued between render and physics frames.");

c = New();
c.mecha.coreEnergy = c.mecha.coreEnergyCap*.05;
Charge(c,2);
for (int i=0;i<10;i++) Tick(c);
Check(c.mecha.Payments == 0 && UIRealtimeTip.Messages.Count == 3, "Low energy cancels once without a later automatic retry.");

foreach (float dt in new[] {1f/60, .05f, .2f})
{
    c = New(1);
    Time.fixedDeltaTime = dt;
    Charge(c,1);
    float top = 0;
    for (int i=0;i<400;i++)
    {
        Tick(c);
        top = Math.Max(top,c.player.position.y);
        Check(!c.player.sailing && c.player.position.magnitude <= 247.01f, "Locked flight remains inside the spherical ceiling.");
    }
    Check(top > 205 && c.mecha.Payments == 1, "A locked launch visibly lifts off without repeated fuel charges.");
}

c = New(3);
Charge(c,4);
for (int i=0;i<100 && !c.player.sailing;i++) Tick(c);
Check(c.player.sailing, "High-layer launch reaches sailing.");
c.gameData.localPlanet = null;
double previousSpeed = c.player.uVelocity.y;
for (int i=0;i<10;i++) Tick(c);
Check(c.player.uVelocity.y < previousSpeed, "No continued thrust or speed reset after the initial impulse.");
Check(c.mecha.Payments == 1, "Space coasting never pays again.");
Input(c,true,0);
Input(c,false,10);
Tick(c);
Check(c.mecha.Payments == 1, "Deep space cannot initiate another launch.");

foreach (float dt in new[] {1f/60, .05f, .2f})
{
    c = New(1);
    c.player.position = new(0,245,0);
    c.player.uPosition = c.player.position;
    Time.fixedDeltaTime = dt;
    Charge(c,1);
    for (int i=0;i<40;i++)
    {
        Tick(c);
        Check(c.player.position.magnitude <= 247.01f && !c.player.sailing, "Near-ceiling ignition cannot escape before unlock.");
    }
}
c = New();
c.player.position = new(0,240,0);
c.player.uPosition = c.player.position;
c.velocity = new(0,-80,0);
Charge(c,2);
Tick(c);
Check(c.velocity.y > 149, "Falling release immediately reverses radial velocity to the chosen launch speed.");
for (int i=0;i<60;i++) Tick(c);
Check(c.player.uVelocity.y > 0, "Falling release maintains outward momentum after handoff.");

c = New(3);
LDB.techs.dataArray = new[] {
    new TechProto { ID=1, Name="Drive", Level=1, UnlockFunctions=new[]{4}, UnlockValues=new[]{1.0} },
    new TechProto { ID=4, Name="Drive", Level=4 },
    new TechProto { ID=6, Name="Drive", Level=6 }
};
c.gameData.history.techStates[1] = new TechState { unlocked=true, curLevel=1 };
c.gameData.history.techStates[4] = new TechState { unlocked=true, curLevel=4 };
c.gameData.history.techStates[6] = new TechState { unlocked=false, curLevel=9 };
Check(DriveLevels.Read(c) == 8, "Repeatable tech counts completed levels, not the in-progress level or warp capability flag.");
c.gameData.history.techStates[6] = new TechState { unlocked=true, curLevel=20 };
Check(DriveLevels.Read(c) == 20, "Final researched level remains available at the tech cap.");
Console.WriteLine($"PASS {checks} runtime integration assertions using deterministic host doubles. No game or renderer was executed.");
