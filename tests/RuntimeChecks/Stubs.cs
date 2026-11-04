// Minimal deterministic host for the actual runtime code. These doubles do not
// emulate Unity collisions, rendering, Harmony execution, or native flight AI.
using UnityEngine;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public float sqrMagnitude => x*x+y*y+z*z;
        public float magnitude => MathF.Sqrt(sqrMagnitude);
        public Vector3 normalized => magnitude > 0 ? this / magnitude : zero;
        public static Vector3 zero => default;
        public static float Dot(Vector3 a, Vector3 b) => a.x*b.x+a.y*b.y+a.z*b.z;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a, float b) => new(a.x*b,a.y*b,a.z*b);
        public static Vector3 operator /(Vector3 a, float b) => a*(1/b);
    }
    public static class Mathf
    {
        public static float Max(float a, float b) => Math.Max(a,b);
        public static float Clamp(float v, float a, float b) => Math.Clamp(v,a,b);
    }
    public static class Time
    {
        public static double realtimeSinceStartupAsDouble;
        public static float unscaledTime => (float)realtimeSinceStartupAsDouble;
        public static float fixedDeltaTime = 1f/60;
    }
    public static class Application { public static bool isFocused = true; }
    public class GameObject { public bool activeInHierarchy = true; }
    public class ParticleSystem
    {
        public GameObject gameObject = new();
        public int Emitted;
        public void Emit(int n) { Emitted += n; }
    }
    public class Rigidbody { public Vector3 position; public void WakeUp() { } }
}
public struct VectorLF3
{
    public double x, y, z;
    public VectorLF3(double x, double y, double z) { this.x=x; this.y=y; this.z=z; }
    public double magnitude => Math.Sqrt(x*x+y*y+z*z);
    public VectorLF3 normalized => magnitude > 0 ? this*(1/magnitude) : default;
    public static double Dot(VectorLF3 a, VectorLF3 b) => a.x*b.x+a.y*b.y+a.z*b.z;
    public static VectorLF3 operator +(VectorLF3 a, VectorLF3 b) => new(a.x+b.x,a.y+b.y,a.z+b.z);
    public static VectorLF3 operator -(VectorLF3 a, VectorLF3 b) => new(a.x-b.x,a.y-b.y,a.z-b.z);
    public static VectorLF3 operator *(VectorLF3 a, double b) => new(a.x*b,a.y*b,a.z*b);
    public static implicit operator VectorLF3(Vector3 a) => new(a.x,a.y,a.z);
    public static explicit operator Vector3(VectorLF3 a) => new((float)a.x,(float)a.y,(float)a.z);
}
public static class Maths { public static VectorLF3 QInvRotateLF(int r, VectorLF3 p) => p; }
public enum EMovementState { Walk, Drift, Fly, Sail }
public enum ECommand { None, Build }
public enum EBlueprintMode { None, Copy }
public enum EViewMode { Normal, Globe }
public class Player
{
    public PlayerController controller;
    public bool isAlive = true, respawning, warping;
    public EMovementState movementState;
    public bool sailing => movementState == EMovementState.Sail;
    public Vector3 position = new(0,201,0);
    public VectorLF3 uPosition = new(0,201,0), uVelocity;
    public PlayerEffect effect = new();
}
public class PlayerEffect { public ParticleSystem[] backEngineEffect = { new() }; }
public class PlanetData
{
    public float realRadius = 200;
    public VectorLF3 uPosition;
    public int runtimeRotation;
    public VectorLF3 GetUniversalVelocityAtLocalPoint(double t, VectorLF3 p) => default;
}
public class GameData
{
    public PlanetData localPlanet = new();
    public bool disableController, guideRunning;
    public History history = new();
}
public class History { public Dictionary<int,TechState> techStates = new(); }
public struct TechState { public bool unlocked; public int curLevel; }
public class TechProto
{
    public int ID, Level;
    public string Name;
    public int[] UnlockFunctions = Array.Empty<int>();
    public double[] UnlockValues = Array.Empty<double>();
}
public class Techs { public TechProto[] dataArray = Array.Empty<TechProto>(); }
public static class LDB { public static Techs techs = new(); }
public class Mecha
{
    public int thrusterLevel = 2, Payments;
    public double thrustPowerPerAcc = 6000, reactorPowerConsRatio = 1, coreEnergyCap = 30000000, coreEnergy = 30000000;
    public void QueryEnergy(double e, out double available, out double ratio) { available = Math.Min(e,coreEnergy); ratio=1; }
    public void UseEnergy(double e) { coreEnergy -= e; Payments++; }
    public void MarkEnergyChange(int category, double e) { }
}
public class Command { public ECommand type; public void SetNoneCommand() { type=ECommand.None; } }
public class Build { public EBlueprintMode blueprintMode; }
public class Walk(PlayerController c)
{
    public void SwitchToFly() { c.movementStateInFrame = EMovementState.Fly; }
}
public class PlayerMove_Sail(PlayerController c)
{
    public double input_aff_1, input_aff_2;
    public void ResetSailState() { c.player.uVelocity=c.velocity; }
}
public class PlayerController
{
    public static bool operationWhenBuild;
    public Player player;
    public GameData gameData = new();
    public Mecha mecha = new();
    public Command cmd = new();
    public Build actionBuild = new();
    public Walk actionWalk;
    public PlayerMove_Sail actionSail;
    public Rigidbody rigid = new();
    public Vector3 velocity, velocityOnLanding, input1, localGravity = new(0,-49.5f,0);
    public VectorLF3 universalGravity = new(0,-49.5,0);
    public EMovementState movementStateInFrame;
    public PlayerController()
    {
        player = new Player { controller=this };
        actionWalk = new Walk(this);
        actionSail = new PlayerMove_Sail(this);
    }
}
public static class VFInput
{
    public struct InputValue { public bool pressing; public static implicit operator InputValue(bool v) => new() { pressing=v }; }
    public static InputValue _jump, _moveForward, _moveBackward, _moveLeft, _moveRight;
    public static bool inputing, inFullscreenGUI, inCombatScreenGUI, inScreenshotMode, _godModeMechaMove;
}
public static class GameMain
{
    public static Player mainPlayer;
    public static bool isRunning = true, isPaused, isFullscreenPaused;
    public static double gameTime;
    public static Scenario gameScenario = new();
}
public class Scenario { public int Handoffs; public void NotifyOnSailModeEnter() { Handoffs++; } }
public static class DSPGame { public static bool IsMenuDemo; }
public class GameCamera
{
    public static GameCamera instance = new(), main = instance;
    public void SyncForSailMode() { }
}
public class UIGame { public static EViewMode viewMode; public bool active=true; public object generalTips = new(); }
public class UIRoot { public static UIRoot instance = new(); public UIGame uiGame = new(); }
public static class Localization { public static bool isZHCN = true; }
public static class UIRealtimeTip
{
    public static List<string> Messages = new();
    public static void Popup(string text, Vector3 p, bool sound, int id) { Messages.Add(text); }
}
namespace IcarusAtmosphericBoost
{
    public class Setting<T>(T value) { public T Value = value; }
    internal static class Plugin
    {
        internal static Setting<bool> Enabled = new(true), ShowTips = new(true);
        internal static Setting<float> ChargeSecondsPerLayer = new(.75f), SpeedPerLayer = new(75),
            LaunchRange = new(300), BoostPowerMultiplier = new(2);
    }
}
