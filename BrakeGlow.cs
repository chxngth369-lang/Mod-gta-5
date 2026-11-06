using System;
using GTA;
using GTA.Math;
using GTA.Native;

// BrakeGlow v2: hard braking heats each wheel's brake; hot brakes glow red-orange.
// Debug = true shows a "loaded" message and live heat on screen, to find out why nothing glows.
public class BrakeGlow : Script
{
    const bool Debug = true;

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
    bool shownLoaded;

    public BrakeGlow()
    {
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
        if (Debug && !shownLoaded)
        {
            shownLoaded = true;
            GTA.UI.Screen.ShowSubtitle("BrakeGlow loaded", 4000);
        }

        Ped player = Game.Player.Character;
        Vehicle veh = player.CurrentVehicle;

        if (veh == null || !veh.Exists() || player.SeatIndex != VehicleSeat.Driver)
        {
            if (lastHandle != 0) Reset();
            return;
        }

        if (veh.Handle != lastHandle)
        {
            Reset();
            lastHandle = veh.Handle;
            lastSpeed = veh.Speed;
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

        if (Debug)
        {
            GTA.UI.Screen.ShowSubtitle(
                "heat " + heat[0].ToString("0.00") + " | speed " + speed.ToString("0")
                + " | decel " + decel.ToString("0.0") + " | brake " + braking, 200);
        }

        if (!anyGlow) return;

        for (int i = 0; i < heat.Length; i++)
        {
            if (heat[i] <= GlowStart) continue;

            EntityBone bone = veh.Bones[WheelBones[i]];
            if (!bone.IsValid) continue;

            float glow = (heat[i] - GlowStart) / (1f - GlowStart);
            int r = 255;
            int g = (int)(40f + 110f * glow);
            int b = (int)(10f + 20f * glow);
            float range = 1.5f + 2.5f * glow;
            float intensity = 6f + 14f * glow;

            Vector3 pos = bone.Position;
            Function.Call(Hash.DRAW_LIGHT_WITH_RANGE,
                pos.X, pos.Y, pos.Z, r, g, b, range, intensity);
        }
    }
}
