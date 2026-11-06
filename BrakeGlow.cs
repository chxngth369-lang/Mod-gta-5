using System;
using System.IO;
using GTA;
using GTA.Math;
using GTA.Native;

// BrakeGlow v3: same brake-heat glow, but writes BrakeGlow.log next to the dll
// (so we can see if the script runs and what it sees) and uses natives for wheel bones.
public class BrakeGlow : Script
{
    static readonly string[] WheelBones = { "wheel_lf", "wheel_rf", "wheel_lr", "wheel_rr" };
    static readonly float[] BrakeShare = { 1.3f, 1.3f, 0.7f, 0.7f };

    const float HeatGain = 0.002f;
    const float BaseCooling = 0.12f;
    const float AirCooling = 0.012f;
    const float GlowStart = 0.10f;
    const float MinSpeed = 2.0f;
    const float MaxDecel = 25.0f;

    readonly float[] heat = new float[4];
    float lastSpeed;
    int lastHandle;
    bool firstTick;
    int nextLog;
    int lightsDrawn;
    static string logPath;

    static void Log(string msg)
    {
        try
        {
            if (logPath == null)
            {
                string dir = null;
                try { dir = Path.GetDirectoryName(typeof(BrakeGlow).Assembly.Location); } catch { }
                if (string.IsNullOrEmpty(dir))
                    dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                logPath = Path.Combine(dir, "BrakeGlow.log");
            }
            File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg + "\n");
        }
        catch { }
    }

    public BrakeGlow()
    {
        Log("constructor ran");
        Tick += OnTick;
    }

    void Reset()
    {
        for (int i = 0; i < heat.Length; i++) heat[i] = 0f;
        lastHandle = 0;
        lastSpeed = 0f;
    }

    void OnTick(object sender, EventArgs e)
    {
        try
        {
            Run();
        }
        catch (Exception ex)
        {
            Log("ERROR in tick: " + ex);
        }
    }

    void Run()
    {
        if (!firstTick)
        {
            firstTick = true;
            Log("first tick, logging to " + logPath);
            GTA.UI.Screen.ShowSubtitle("BrakeGlow loaded", 4000);
        }

        Ped player = Game.Player.Character;
        Vehicle veh = player.CurrentVehicle;

        if (veh == null || !veh.Exists() || player.SeatIndex != VehicleSeat.Driver)
        {
            if (lastHandle != 0) { Log("left vehicle"); Reset(); }
            return;
        }

        if (veh.Handle != lastHandle)
        {
            Reset();
            lastHandle = veh.Handle;
            lastSpeed = veh.Speed;
            Log("entered vehicle " + veh.Handle);
        }

        float dt = Game.LastFrameTime;
        if (dt <= 0f) return;

        float speed = veh.Speed;
        float decel = (lastSpeed - speed) / dt;
        lastSpeed = speed;

        bool braking = Game.IsControlPressed(Control.VehicleBrake) || decel > 3f;
        if (decel < 0f) decel = 0f;
        if (decel > MaxDecel) decel = MaxDecel;

        float cooling = BaseCooling + speed * AirCooling;
        bool anyGlow = false;

        for (int i = 0; i < heat.Length; i++)
        {
            if (braking && speed > MinSpeed)
                heat[i] += decel * speed * HeatGain * BrakeShare[i] * dt;

            heat[i] -= heat[i] * cooling * dt;

            if (heat[i] > 1f) heat[i] = 1f;
            if (heat[i] < 0f) heat[i] = 0f;
            if (heat[i] > GlowStart) anyGlow = true;
        }

        GTA.UI.Screen.ShowSubtitle(
            "heat " + heat[0].ToString("0.00") + " | speed " + speed.ToString("0")
            + " | decel " + decel.ToString("0.0") + " | brake " + braking, 200);

        int now = Game.GameTime;
        if (now >= nextLog)
        {
            nextLog = now + 500;
            Log("speed " + speed.ToString("0.0") + " decel " + decel.ToString("0.0")
                + " braking " + braking + " heat " + heat[0].ToString("0.00") + "/" + heat[2].ToString("0.00")
                + " lights " + lightsDrawn);
        }

        if (!anyGlow) return;

        for (int i = 0; i < heat.Length; i++)
        {
            if (heat[i] <= GlowStart) continue;

            int boneIndex = Function.Call<int>(Hash.GET_ENTITY_BONE_INDEX_BY_NAME, veh.Handle, WheelBones[i]);
            if (boneIndex < 0)
            {
                if (nextLog - now > 400) Log("bone not found: " + WheelBones[i]);
                continue;
            }

            Vector3 pos = Function.Call<Vector3>(Hash.GET_WORLD_POSITION_OF_ENTITY_BONE, veh.Handle, boneIndex);

            float glow = (heat[i] - GlowStart) / (1f - GlowStart);
            int r = 255;
            int g = (int)(40f + 110f * glow);
            int b = (int)(10f + 20f * glow);
            float range = 1.5f + 2.5f * glow;
            float intensity = 6f + 14f * glow;

            Function.Call(Hash.DRAW_LIGHT_WITH_RANGE,
                pos.X, pos.Y, pos.Z, r, g, b, range, intensity);
            lightsDrawn++;
        }
    }
}
